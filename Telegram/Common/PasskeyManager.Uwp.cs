//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Threading.Tasks;
using Telegram.Navigation;
using Telegram.Services;

namespace Telegram.Common
{
    public static partial class PasskeyManager
    {
        // Over the app service to Telegram.Stub, which owns the window handle it needs and is
        // outside the container. Deliberately thin: this path ships, and it keeps behaving as it did.
        public static partial Task<Object> AddLoginAsync(WindowContext window, IClientService clientService)
        {
            return BridgeApplicationContext.AddLoginPasskeyAsync(window, clientService);
        }

        public static partial Task<Object> CheckAuthenticationAsync(WindowContext window, IClientService clientService)
        {
            return BridgeApplicationContext.CheckAuthenticationPasskeyAsync(window, clientService);
        }
    }
}
