using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public enum TLErrorCode
    {
        ERROR_SEE_OTHER = 303,
        BAD_REQUEST = 400,
        PEER_FLOOD = 400,
        UNAUTHORIZED = 401,
        FORBIDDEN = 403,
        NOT_FOUND = 404,
        FLOOD = 420,
        INTERNAL = 500,

        #region Additional
        TIMEOUT = 408,
        #endregion
    }
}
