using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Popups;
using Windows.UI.Xaml;

namespace Unigram.Common
{
    /// <summary>
    /// MessageDialog extension methods
    /// Source: https://github.com/xyzzer/WinRTXamlToolkit/blob/master/WinRTXamlToolkit/Controls/Extensions/MessageDialogExtensions.cs
    /// </summary>
    public static class MessageDialogExtensions
    {
        private static TaskCompletionSource<MessageDialog> _currentDialogShowRequest;

        /// <summary>
        /// Begins an asynchronous operation showing a dialog.
        /// If another dialog is already shown using
        /// ShowQueuedAsync or ShowIfPossibleAsync method - it will wait
        /// for that previous dialog to be dismissed before showing the new one.
        /// </summary>
        /// <param name="dialog">The dialog.</param>
        /// <returns></returns>
        /// <exception cref="System.InvalidOperationException">This method can only be invoked from UI thread.</exception>
        public static async Task<IUICommand> ShowQueuedAsync(this MessageDialog dialog)
        {
            if (!Window.Current.Dispatcher.HasThreadAccess)
            {
                throw new InvalidOperationException("This method can only be invoked from UI thread.");
            }

            while (_currentDialogShowRequest != null)
            {
                await _currentDialogShowRequest.Task;
            }

            var request = _currentDialogShowRequest = new TaskCompletionSource<MessageDialog>();
            var result = await dialog.ShowAsync();
            _currentDialogShowRequest = null;
            request.SetResult(dialog);

            return result;
        }

        /// <summary>
        /// Begins an asynchronous operation showing a dialog.
        /// If another dialog is already shown using
        /// ShowQueuedAsync or ShowIfPossibleAsync method - it will wait
        /// return immediately and the new dialog won't be displayed.
        /// </summary>
        /// <param name="dialog">The dialog.</param>
        /// <returns></returns>
        /// <exception cref="System.InvalidOperationException">This method can only be invoked from UI thread.</exception>
        public static async Task<IUICommand> ShowIfPossibleAsync(this MessageDialog dialog)
        {
            if (!Window.Current.Dispatcher.HasThreadAccess)
            {
                throw new InvalidOperationException("This method can only be invoked from UI thread.");
            }

            while (_currentDialogShowRequest != null)
            {
                return null;
            }

            var request = _currentDialogShowRequest = new TaskCompletionSource<MessageDialog>();
            var result = await dialog.ShowAsync();
            _currentDialogShowRequest = null;
            request.SetResult(dialog);

            return result;
        }
    }
}
