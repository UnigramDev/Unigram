using System;
using System.Text;
using Telegram.Api.Native.TL;
using Telegram.Api.TL;

namespace Telegram.Api.Services
{
    class DelayedItem
    {
        public string Caption { get; set; }
        public DateTime SendTime { get; set; }
        //public DateTime? SendBeforeTime { get; set; }
        public TLObject Object { get; set; }
        public Action<object> Callback { get; set; }
        public Action<TLRPCError> FaultCallback { get; set; }
        public Action<int> AttemptFailed { get; set; }
        public int? MaxAttempt { get; set; }
        public int CurrentAttempt { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendLine("DelayedItem");
            sb.AppendLine("Caption " + Caption);
            sb.AppendLine("MaxAttempt " + MaxAttempt);
            sb.AppendLine("CurrentAttempt " + CurrentAttempt);

            return sb.ToString();
        }
    }
}
