//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Buffers.Text;
using System.Threading.Tasks;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.Common
{
    public static partial class PasskeyManager
    {
        // In process, because there is no stub here: the same WebAuthn wrapper the stub links, with
        // the app service round trip taken out of the middle.
        public static async partial Task<Object> AddLoginAsync(WindowContext window, IClientService clientService)
        {
            Logger.Info();

            var response = await clientService.SendAsync(new GetPasskeyParameters());
            if (response is not Text parameters)
            {
                return response;
            }

            var hwnd = new IntPtr(window.Handle);
            var data = WebAuthn.DeserializeRegisterData(parameters.TextValue);

            // Off the UI thread: WebAuthn blocks until the user has answered the system prompt, and
            // this thread is the one that would have to draw the window behind it.
            var result = await Task.Run(() => WebAuthn.MakeCredential(hwnd, data));

            if (result is WebAuthn.RegisterResult credential)
            {
                return await clientService.SendAsync(
                    new AddLoginPasskey(credential.ClientDataJson, credential.AttestationObject));
            }

            return ToError(result);
        }

        public static async partial Task<Object> CheckAuthenticationAsync(WindowContext window, IClientService clientService)
        {
            Logger.Info();

            var response = await clientService.SendAsync(new GetAuthenticationPasskeyParameters());
            if (response is not Text parameters)
            {
                return response;
            }

            var hwnd = new IntPtr(window.Handle);
            var data = WebAuthn.DeserializeLoginData(parameters.TextValue);

            var result = await Task.Run(() => WebAuthn.GetAssertion(hwnd, data));

            if (result is WebAuthn.LoginResult assertion)
            {
                // Base64Url for the id, which is what the stub sent over the bridge and therefore
                // what TDLib expects; the rest crosses as bytes.
                return await clientService.SendAsync(
                    new CheckAuthenticationPasskey(Base64Url.EncodeToString(assertion.CredentialId),
                        assertion.ClientDataJson, assertion.AuthenticatorData, assertion.Signature,
                        assertion.UserHandle));
            }

            return ToError(result);
        }

        // WebAuthn hands back the failure as an exception rather than throwing it, and its HResult
        // is what the user cancelling looks like - so it is carried through rather than flattened.
        private static Error ToError(object result)
        {
            if (result is Exception exception)
            {
                return new Error(exception.HResult, exception.Message ?? string.Empty);
            }

            return new Error(400, "Unknown error");
        }
    }
}
