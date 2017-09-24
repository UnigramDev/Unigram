using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL.Channels;

namespace Telegram.Api.TL
{
    public partial class TLChannelFull
    {
        public TLChannelsChannelParticipantsBase Participants { get; set; }
    }
}
