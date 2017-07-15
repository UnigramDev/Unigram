using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public interface ITLReadMaxId
    {
        Int32 ReadInboxMaxId { get; set; }

        Int32 ReadOutboxMaxId { get; set; }
    }
}
