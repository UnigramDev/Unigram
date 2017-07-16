using System;
using Telegram.Api.Native.TL;
using Telegram.Api.TL;

namespace Telegram.Api.Services
{
    public enum RequestStatus
    {
        Sent,
        Confirmed,
        Failed,
        ReadyToSend
    }

    public class HistoryItem
    {
        //public long Hash { get { return Message != null ? Message.MsgId : 0; } }
        //public TLTransportMessageBase Message { get; set; }
        public TLObject Object { get; set; }
        public string Caption { get; set; }
        public DateTime SendTime { get; set; }
        public DateTime? SendBeforeTime { get; set; }
        public RequestStatus Status { get; set; }
        public Action<object> Callback { get; set; }
        public Action FastCallback { get; set; }
        public Action<int> AttemptFailed { get; set; }
        public Action<TLRPCError> FaultCallback { get; set; }
        public Action<TLRPCError> FaultQueueCallback { get; set; }
        public long ClientTicksDelta { get; set; }
        public HistoryItem InvokeAfter { get; set; }
        public TLRPCError LastError { get; set; }

        public int DCId { get; set; }

        //public volatile bool IsSending;

        public override string ToString()
        {
            return string.Format("{0}: {1} {2}", SendTime.ToString("HH:mm:ss.fff"), Caption, GetHashCode());
        }
    }
}