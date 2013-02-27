using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using Microsoft.AspNet.SignalR.Json;
using Newtonsoft.Json;


namespace SignalR.Reactive
{
    public class RxJsProxyGenerator : IJavaScriptProxyGenerator
    {
         private static readonly Lazy<string> _templateFromResource = new Lazy<string>(GetTemplateFromResource);

        private static readonly Type[] _numberTypes = new[] { typeof(byte), typeof(short), typeof(int), typeof(long), typeof(float), typeof(decimal), typeof(double) };
        private static readonly Type[] _dateTypes = new[] { typeof(DateTime), typeof(DateTimeOffset) };

        private const string ScriptResource = "Microsoft.AspNet.SignalR.Scripts.hubs.js";

        private readonly IHubManager _manager;
        private readonly IJavaScriptMinifier _javaScriptMinifier;
        private readonly Lazy<string> _generatedTemplate;

        public RxJsProxyGenerator(IDependencyResolver resolver) :
            this(resolver.Resolve<IHubManager>(),
                 resolver.Resolve<IJavaScriptMinifier>())
        {
        }

        public RxJsProxyGenerator(IHubManager manager, IJavaScriptMinifier javaScriptMinifier)
        {
            _manager = manager;
            _javaScriptMinifier = javaScriptMinifier ?? NullJavaScriptMinifier.Instance;
            _generatedTemplate = new Lazy<string>(() => GenerateProxy(_manager, _javaScriptMinifier, includeDocComments: false));
        }

        public string GenerateProxy(string serviceUrl)
        {
            serviceUrl = JavaScriptEncode(serviceUrl);

            var generateProxy = _generatedTemplate.Value;

            return generateProxy.Replace("{serviceUrl}", serviceUrl);
        }

        public string GenerateProxy(string serviceUrl, bool includeDocComments)
        {
            serviceUrl = JavaScriptEncode(serviceUrl);

            string generateProxy = GenerateProxy(_manager, _javaScriptMinifier, includeDocComments);

            return generateProxy.Replace("{serviceUrl}", serviceUrl);
        }

        private static string GenerateProxy(IHubManager hubManager, IJavaScriptMinifier javaScriptMinifier, bool includeDocComments)
        {
            string script = _templateFromResource.Value;

            var hubs = new StringBuilder();
            var first = true;
            foreach (var descriptor in hubManager.GetHubs().OrderBy(h => h.Name))
            {
                if (!first)
                {
                    hubs.AppendLine(";");
                    hubs.AppendLine();
                    hubs.Append("    ");
                }
                GenerateType(hubManager, hubs, descriptor, includeDocComments);
                first = false;
            }

            if (hubs.Length > 0)
            {
                hubs.Append(";");
            }

            script = script.Replace("/*hubs*/", hubs.ToString());

            javaScriptMinifier.Minify(script);

            return script;
        }

        private static void GenerateType(IHubManager hubManager, StringBuilder sb, HubDescriptor descriptor, bool includeDocComments)
        {
            // Get only actions with minimum number of parameters.
            var methods = GetMethods(hubManager, descriptor);
            var hubName = GetDescriptorName(descriptor);

            sb.AppendFormat("    proxies.{0} = this.createHubProxy('{1}'); ", hubName, hubName).AppendLine();
            sb.AppendFormat("        proxies.{0}.client = {{ }};", hubName).AppendLine();
            sb.AppendFormat("        proxies.{0}.server = {{", hubName);

            bool first = true;

            foreach (var method in methods)
            {
                if (!first)
                {
                    sb.Append(",").AppendLine();
                }
                GenerateMethod(sb, method, includeDocComments, hubName);
                first = false;
            }

            GenerateRxSubject(sb, descriptor);

            sb.AppendLine();
            sb.Append("        }");
        }

        private static string GetDescriptorName(Descriptor descriptor)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException("descriptor");
            }

            string name = descriptor.Name;

            // If the name was not specified then do not camel case
            if (!descriptor.NameSpecified)
            {
                name = JsonUtility.CamelCase(name);
            }

            return name;
        }

        private static IEnumerable<MethodDescriptor> GetMethods(IHubManager manager, HubDescriptor descriptor)
        {
            return from method in manager.GetHubMethods(descriptor.Name)
                   group method by method.Name into overloads
                   let oload = (from overload in overloads
                                orderby overload.Parameters.Count
                                select overload).FirstOrDefault()
                   orderby oload.Name
                   select oload;
        }

        private static void GenerateMethod(StringBuilder sb, MethodDescriptor method, bool includeDocComments, string hubName)
        {
            var parameterNames = method.Parameters.Select(p => p.Name).ToList();
            sb.AppendLine();
            sb.AppendFormat("            {0}: function ({1}) {{", GetDescriptorName(method), Commas(parameterNames)).AppendLine();
            if (includeDocComments)
            {
                sb.AppendFormat("<summary>Calls the {0} method on the server-side {1} hub.&#10;Returns a jQuery.Deferred() promise.</summary>", method.Name, method.Hub.Name).AppendLine();
                var parameterDoc = method.Parameters.Select(p => String.Format(CultureInfo.CurrentCulture, " /// <param name=\"{0}\" type=\"{1}\">Server side type is {2}</param>", p.Name, MapToJavaScriptType(p.ParameterType), p.ParameterType)).ToList();
                if (parameterDoc.Any())
                {
                    sb.AppendLine(String.Join(Environment.NewLine, parameterDoc));
                }
            }
            sb.AppendFormat("                return proxies.{0}.invoke.apply(proxies.{0}, $.merge([\"{1}\"], $.makeArray(arguments)));", hubName, method.Name).AppendLine();
            sb.Append("             }");
        }

        private static void GenerateRxSubject(StringBuilder sb, HubDescriptor descriptor)
        {
            var hubName = JsonUtility.CamelCase(descriptor.Name);
            sb.AppendFormat(",").AppendLine();
            sb.AppendFormat("            subject : $.extend(new Rx.Subject(), {{toJSON: function() {{}}}}),").AppendLine();
            sb.AppendFormat("            subjectOnNext: function(value) {{ signalR.{0}.subject.onNext(value); }},", hubName).AppendLine();

            sb.AppendFormat("            observe: function (eventName) {{ ").AppendLine();
            sb.AppendFormat("                                return Rx.Observable.createWithDisposable(function (obs) {{ ").AppendLine();
            sb.AppendFormat("                                                var disposable = signalR.{0}.subject ", hubName).AppendLine();
            sb.AppendFormat("                                                    .asObservable() ").AppendLine();
            sb.AppendFormat("                                                    .where(function (x) {{ return x.EventName.toLowerCase() === eventName.toLowerCase(); }}) ").AppendLine();
            sb.AppendFormat("                                                    .subscribe(function (x) {{ ").AppendLine();
            sb.AppendFormat("                                                        if (x.Type === 'onNext') obs.onNext(x.Data); ").AppendLine();
            sb.AppendFormat("                                                        if (x.Type === 'onError') obs.onError(x.Data); ").AppendLine();
            sb.AppendFormat("                                                        if (x.Type === 'onCompleted') obs.onCompleted(); ").AppendLine();
            sb.AppendFormat("                                                    }}); ").AppendLine();
            sb.AppendFormat("                                                return disposable; ").AppendLine();
            sb.AppendFormat("                                 }}); ").AppendLine();
            sb.AppendFormat("                             }} ").AppendLine();
        }


        private static string MapToJavaScriptType(Type type)
        {
            if (!type.IsPrimitive && !(type == typeof(string)))
            {
                return "Object";
            }
            if (type == typeof(string))
            {
                return "String";
            }
            if (_numberTypes.Contains(type))
            {
                return "Number";
            }
            if (typeof(IEnumerable).IsAssignableFrom(type))
            {
                return "Array";
            }
            if (_dateTypes.Contains(type))
            {
                return "Date";
            }
            return String.Empty;
        }

        private static string Commas(IEnumerable<string> values)
        {
            return Commas(values, v => v);
        }

        private static string Commas<T>(IEnumerable<T> values, Func<T, string> selector)
        {
            return String.Join(", ", values.Select(selector));
        }

        private static string GetTemplateFromResource()
        {
            using (Stream resourceStream = typeof(DefaultJavaScriptProxyGenerator).Assembly.GetManifestResourceStream(ScriptResource))
            {
                var reader = new StreamReader(resourceStream);
                return reader.ReadToEnd();
            }
        }

        private static string JavaScriptEncode(string value)
        {
            value = JsonConvert.SerializeObject(value);

            // Remove the quotes
            return value.Substring(1, value.Length - 2);
        }
    }
}
