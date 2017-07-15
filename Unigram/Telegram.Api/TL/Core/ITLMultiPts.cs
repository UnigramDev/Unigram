using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public interface ITLMultiPts
    {
        int Pts { get; set; }

        int PtsCount { get; set; }
    }
}
