using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public partial class TLAuthorization
    {
        public bool IsCurrent
        {
            get
            {
                return (Flags & 1) == 1;
            }
        }

        public bool IsOfficialApp
        {
            get
            {
                return (Flags & 2) == 2;
            }
        }
    }
}
