using System;
using System.Web;
using Microsoft.AspNet.SignalR;


[assembly: PreApplicationStartMethod(typeof(SignalR.Reactive.Configuration), "EnableRxSupport")]
namespace SignalR.Reactive
{
    public static class Configuration
    {
        public static void EnableRxSupport()
        {
            DependencyResolverContext.Instance = GlobalHost.DependencyResolver;
            
            if (DependencyResolverContext.Instance == null)
                throw new InvalidOperationException("DependenyResolver must be set to an instance of IDependencyResolver");

            DependencyResolverContext.Instance.EnableRxSupport();
            //ToDo 
            //AspNetBootstrapper.Initialize();
            
        }
    }
}
