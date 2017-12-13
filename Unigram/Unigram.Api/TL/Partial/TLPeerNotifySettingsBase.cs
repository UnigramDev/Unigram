using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services;

namespace Telegram.Api.TL
{
    public partial class TLPeerNotifySettingsBase
    {
        public virtual bool IsMuted
        {
            get
            {
                return false;
            }
        }
    }

    public partial class TLPeerNotifySettings
    {
        public override bool IsMuted
        {
            get
            {
                var clientDelta = MTProtoService.Current.ClientTicksDelta;
                var utc0SecsInt = MuteUntil - clientDelta / 4294967296.0;

                var muteUntilDateTime = Utils.UnixTimestampToDateTime(utc0SecsInt);
                return muteUntilDateTime > DateTime.Now;
            }
        }
    }
}
