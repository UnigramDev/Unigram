using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public partial class TLMessageMediaDocument : ITLMessageMediaCaption, ITLMessageMediaDestruct
    {
        public Int64? DestructDate { get; set; }
    }
}
