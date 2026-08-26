//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;

namespace Windows.UI.Xaml.Input
{
    public static class FocusManagerEx
    {
        private static bool _reported;

        public static object TryGetFocusedElement(XamlRoot xamlRoot)
        {
            try
            {
                return FocusManager.GetFocusedElement(xamlRoot);
            }
            catch (Exception ex)
            {
                // All the remote procedure calls must be wrapped in a try-catch block.
                // Logged rather than swallowed because this is the app-side face of the E_FAIL that
                // also crashes from inside XAML, where no try-catch of ours can reach. The failure
                // is permanent once it starts, so only the first one is uploaded - the rest stay in
                // the log tail, where the run of them shows how long the app went on in that state.
                if (_reported)
                {
                    Telegram.Logger.Error(ex);
                }
                else
                {
                    _reported = true;
                    Telegram.Logger.Exception(ex);
                }

                return null;
            }
        }
    }
}
