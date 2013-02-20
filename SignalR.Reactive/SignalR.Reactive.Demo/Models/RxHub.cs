using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using SignalR.Hubs;

namespace SignalR.Reactive.Demo.Models
{

    public class RxHub : Hub
    {

        public Task Join(string group)
        {
            return Groups.Add(Context.ConnectionId, group);
        }

        public void MoveShape(int x, int y)
        {
            this.RaiseOnNext("ShapeMoved", new
            {
                Cid = Context.ConnectionId,
                X = x,
                Y = y
            });
        }

        public void SendMessage(string message, string group)
        {

            RxGroupHelper g = new RxGroupHelper() {Grouname = group};
            RxHelper
                .RaiseOn<RxHub>()
                .OnNext("NewMessage", message, g);
        }

    }
}