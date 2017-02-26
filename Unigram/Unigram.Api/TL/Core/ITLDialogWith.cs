using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public interface ITLDialogWith
    {
        object PhotoSelf { get; }
        string DisplayName { get; }
    }
}
