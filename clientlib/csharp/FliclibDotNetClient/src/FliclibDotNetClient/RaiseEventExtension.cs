using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FliclibDotNetClient
{
    internal static class EventExtensions
    {
        internal static void RaiseEvent(this EventHandler @event, object sender, EventArgs e)
        {
            if (@event != null)
                @event(sender, e);
        }
        internal static void RaiseEvent<T>(this EventHandler<T> @event, object sender, T e)
            where T : EventArgs
        {
            if (@event != null)
                @event(sender, e);
        }
    }
}
