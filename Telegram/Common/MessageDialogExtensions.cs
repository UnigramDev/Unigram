//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading.Tasks;
using Telegram.Controls;
using Windows.UI.Xaml.Controls;

namespace Telegram.Common
{
    /// <summary>
    /// MessageDialog extension methods
    /// Source: https://github.com/xyzzer/WinRTXamlToolkit/blob/master/WinRTXamlToolkit/Controls/Extensions/MessageDialogExtensions.cs
    /// </summary>
    public static class MessageDialogExtensions
    {
        [ThreadStatic]
        private static TaskCompletionSource<ContentDialog> _currentDialogShowRequest;

        /// <summary>
        /// Begins an asynchronous operation showing a dialog.
        /// If another dialog is already shown using
        /// ShowQueuedAsync or ShowIfPossibleAsync method - it will wait
        /// for that previous dialog to be dismissed before showing the new one.
        /// </summary>
        /// <param name="dialog">The dialog.</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">This method can only be invoked from UI thread.</exception>
        public static async Task<ContentDialogResult> ShowQueuedAsync(this ContentDialog dialog)
        {
            while (_currentDialogShowRequest != null)
            {
                await _currentDialogShowRequest.Task;
            }

            Logger.Info(dialog.GetType().Name);

            if (dialog is ContentPopup popup)
            {
                popup.OnCreate();
            }

            var request = _currentDialogShowRequest = new TaskCompletionSource<ContentDialog>();
            var result = await dialog.ShowAsync();
            _currentDialogShowRequest = null;
            request.SetResult(dialog);

            Logger.Info(dialog.GetType().Name + ", closed");
            return result;
        }
    }
}
