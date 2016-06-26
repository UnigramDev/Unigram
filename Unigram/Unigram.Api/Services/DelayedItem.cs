﻿namespace Telegram.Api.Services
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using Telegram.Api.TL;

    internal class DelayedItem
    {
        public string Caption { get; set; }

        public DateTime SendTime { get; set; }
        
        public TLObject Object { get; set; }

        public TaskCompletionSource<MTProtoResponse> Callback { get; set; }

        public TLRPCError FaultCallback { get; set; }

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
