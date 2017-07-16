using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public partial class TLRPCError
    {
        public override string ToString()
        {
            return string.Format("{0} {1}", ErrorCode, ErrorMessage);
        }
    }
}
