using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AspNet.SignalR;


namespace SignalR.Reactive
{
    public static class DependencyResolverContext
    {
        public static IDependencyResolver Instance { get; set; }
    }
}
