using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public interface ITLMessageMediaCaption
    {
        String Caption { get; set; }
        //Boolean HasCaption { get; set; }
    }
}
