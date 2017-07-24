using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public interface ITLMessageMediaDestruct
    {
        Int32? TTLSeconds { get; set; }
        Boolean HasTTLSeconds { get; set; }

        Int64? DestructDate { get; set; }
    }
}
