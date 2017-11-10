using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public partial class TLBadMsgNotification
    {
        public override string ToString()
        {
            return string.Format("TLBadMsgNotification msg_id={0} msg_seq_no={1} error_code={2}", BadMsgId, BadMsgSeqNo, ErrorCode);
        }
    }
}
