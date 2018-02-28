using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public class TLRPCError : TLObject
    {
        private int status;
        private string v;

        public TLRPCError(int status, string v)
        {
            this.status = status;
            this.v = v;
        }

        public int ErrorCode
        {
            get;
        }

        public string ErrorMessage
        {
            get;
        }
    }
}
