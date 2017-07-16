using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public interface ITLChannelInviter
    {
        Int32 InviterId { get; set; }

        Int32 Date { get; set; }
    }
}
