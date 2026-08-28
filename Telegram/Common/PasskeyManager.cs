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
    /// <summary>
    /// Passkeys, as the app asks for them rather than as either host can provide them.
    ///
    /// The work itself is <see cref="WebAuthn"/> in both cases. What differs is who runs it: UWP
    /// cannot call webauthn.dll from inside the container, so the request goes over the app service
    /// to Telegram.Stub and the answer comes back; the Win32 flavour has no stub and calls it here.
    ///
    /// Either way the shape is the same - ask TDLib for the parameters, hand them to WebAuthn, give
    /// TDLib what comes back - which is why the seam is here and not at the call sites.
    /// </summary>
    public static partial class PasskeyManager
    {
        /// <summary>
        /// Whether this machine can do passkeys at all. Answered without asking the other process:
        /// it is the same webauthn.dll either way.
        /// </summary>
        public static bool IsSupported()
        {
            return BridgeApplicationContext.IsPasskeySupported();
        }

        /// <summary>
        /// Registers a new passkey for the account that is already signed in.
        /// </summary>
        public static partial Task<Object> AddLoginAsync(WindowContext window, IClientService clientService);

        /// <summary>
        /// Signs in with an existing passkey.
        /// </summary>
        public static partial Task<Object> CheckAuthenticationAsync(WindowContext window, IClientService clientService);
    }
}
