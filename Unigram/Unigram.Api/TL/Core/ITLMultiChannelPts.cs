using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public interface ITLMultiChannelPts
    {
        int Pts { get; set; }

        int PtsCount { get; set; }
    }
}
