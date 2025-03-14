//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
namespace Telegram.Td.Api
{
    public enum ErrorCode
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
