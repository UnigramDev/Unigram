using System;
using System.Threading;
using Telegram.Api.Helpers;
using Telegram.Api.TL;

namespace Telegram.Api.Services
{
    public enum TransportType
    {
        Http,
		Tcp
    }

    public partial class MTProtoService
    {
        private volatile bool _isLongPollStopped;

        private const int ReattemptDelay = Constants.LongPollReattemptDelay;

        public void StartLongPollRequestAsync()
        {
			if (_isLongPollStopped || _type != TransportType.Http) return;
            TLUtils.WriteLongPoll("Send " + DateTime.Now);
            try
            {
				HttpWaitAsync(0, 0, 25000,
				    () =>
				    {
                        TLUtils.WriteLongPoll("Receive " + DateTime.Now);
				        StartLongPollRequestAsync();
				    },
				    () =>
				    {
                        TLUtils.WriteLongPoll("Receive failed " + DateTime.Now);
				        StartLongPollRequestAsync();
				    });
            }
            catch (Exception)
            {
                TLUtils.WriteLongPoll("Receive failed " + DateTime.Now);
                Execute.BeginOnThreadPool(TimeSpan.FromSeconds(5.0), StartLongPollRequestAsync);
            }
        }

        public void StartLongPoll()
        {
            TLUtils.WriteLongPoll("Start long poll " + DateTime.Now);
            _isLongPollStopped = false;
			StartLongPollRequestAsync();
        }

        public void StopLongPoll()
        {
            TLUtils.WriteLongPoll("Stop long poll " + DateTime.Now);
            _isLongPollStopped = true;
        }
    }
}
