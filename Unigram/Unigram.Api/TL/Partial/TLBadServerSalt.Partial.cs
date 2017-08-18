using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public partial class TLBadServerSalt
    {
        public override string ToString()
        {
            return string.Format("TLBadServerSalt msg_id={0} msg_seq_no={1} error_code={2} new_salt={3}", BadMsgId, BadMsgSeqNo, ErrorCode, NewServerSalt);
        }
    }
}
