using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SignalR.Hubs;
using SignalR.Infrastructure;

namespace SignalR.Reactive
{
    public class RxGroupHelper
    {
        public string Grouname { get; set; }
    }

    public static class RxHelper
    {
        public static dynamic GetHubClients<THub>() where THub : Hub, new()
        {
            var connectionManager = DependencyResolverContext.Instance.Resolve<IConnectionManager>();
            return connectionManager.GetHubContext<THub>().Clients;
        }


        public static dynamic GetHubClients<THub>(string clientName) where THub : Hub, new()
        {
            var clients = GetHubClients<THub>();
            return GetHubClients(clients, clientName);
        }

        public static dynamic GetHubClientsByGroup<THub>(string groupName) where THub : Hub, new()
        {
            var connectionManager = DependencyResolverContext.Instance.Resolve<IConnectionManager>();
            return connectionManager.GetHubContext<THub>().Clients.Group(groupName);
        }

        public static dynamic GetHubClients(Hub hub, string clientName)
        {
            return GetHubClients(hub.Clients, clientName);
        }

        public static dynamic GetHubClients(dynamic clients, string clientName)
        {
            return string.IsNullOrEmpty(clientName) ? clients : clients[clientName];
        }

        public static void WithClient(Hub hub, string clientName, Action<dynamic> continueWith)
        {
            var clients = GetHubClients(hub, clientName);
            continueWith(clients);
        }

        public static void WithClient<THub>(string clientName, Action<dynamic> continueWith) where THub : Hub, new()
        {
            var clients = GetHubClients<THub>(clientName);
            continueWith(clients);
        }

        public static void WithClientGroup<THub>(string clientGroup, Action<dynamic> continueWith) where THub : Hub, new()
        {
            var clients = GetHubClientsByGroup<THub>(clientGroup);
            continueWith(clients);
        }

        public static void RaiseOnNext<T>(string eventName, dynamic clients, T payload)
        {
            clients.subjectOnNext(new { Data = payload, EventName = eventName, Type = ClientsideConstants.OnNextType });
        }

        public static void RaiseOnError(string eventName, dynamic clients, Exception payload)
        {
            clients.subjectOnNext(new { Data = payload, EventName = eventName, Type = ClientsideConstants.OnErrorType });
        }

        public static void RaiseOnCompleted(string eventName, dynamic clients)
        {
            clients.subjectOnNext(new { EventName = eventName, Type = ClientsideConstants.OnCompletedType });
        }

        public static RxHubRaiser<T> RaiseOn<T>() where T : Hub, new()
        {
            return new RxHubRaiser<T>();
        } 
    }

    public class RxHubRaiser<THub> where THub : Hub, new()
    {
        public void OnNext<T>(string eventName, T payload)
        {
            OnNext(eventName, null, payload);
        }

        public void OnNext<T>(string eventName, T payload, RxGroupHelper group)
        {
            OnNext(eventName, null, payload, group);
        }

        public void OnNext<T>(string eventName, string clientName, T payload)
        {
            RxHelper.WithClient<THub>(clientName, clients => RxHelper.RaiseOnNext(eventName, clients, payload));
        }

        public void OnNext<T>(string eventName, string clientName, T payload, RxGroupHelper group)
        {
            RxHelper.WithClientGroup<THub>(group.Grouname, clients => RxHelper.RaiseOnNext(eventName, clients, payload));
        }

        public void OnError(string eventName, Exception exception)
        {
            OnError(eventName, null, exception);
        }

        public void OnError(string eventName, string clientName, Exception exception)
        {
            RxHelper.WithClient<THub>(clientName, clients => RxHelper.RaiseOnError(eventName, clients, exception));
        }

        public void OnError(string eventName, string clientName, Exception exception, string group)
        {
            RxHelper.WithClientGroup<THub>(group, clients => RxHelper.RaiseOnError(eventName, clients, exception));
        }

        public void OnCompleted(string eventName)
        {
            OnCompleted(eventName, null);
        }

        public void OnCompleted(string eventName, string clientName)
        {
            RxHelper.WithClient<THub>(clientName, clients => RxHelper.RaiseOnCompleted(eventName, clients));
        }

        public void OnCompleted(string eventName, string clientName, string group)
        {
            RxHelper.WithClientGroup<THub>(group, clients => RxHelper.RaiseOnCompleted(eventName, clients));
        }
    }
}
