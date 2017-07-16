using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;

namespace Telegram.Api.TL
{
    public abstract partial class TLInputPeerBase
    {
        public TLPeerBase ToPeer()
        {
            return TLUtils.InputPeerToPeer(this, SettingsHelper.UserId);
        }
    }
}
