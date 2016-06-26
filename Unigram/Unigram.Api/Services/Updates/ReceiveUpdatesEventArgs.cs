using System;
using Telegram.Api.TL;

namespace Telegram.Api.Services.Updates
{
    public class ReceiveUpdatesEventArgs : EventArgs
    {
        public TLUpdates Updates { get; protected set; }

        public ReceiveUpdatesEventArgs(TLUpdates updates)
        {
            Updates = updates;
        }
    }
}