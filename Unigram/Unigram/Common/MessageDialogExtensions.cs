//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Unigram.Navigation;
using Unigram.Services;

namespace Unigram.Common
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
        public static async Task<ContentDialogResult> ShowQueuedAsync(this ContentDialog dialog, XamlRoot xamlRoot)
        {
            while (_currentDialogShowRequest != null)
            {
                await _currentDialogShowRequest.Task;
            }

            dialog.XamlRoot = xamlRoot;

            if (dialog.XamlRoot.Content is FrameworkElement element)
            {
                var app = BootStrapper.Current.RequestedTheme == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
                var frame = element.RequestedTheme;

                if (app != frame)
                {
                    dialog.RequestedTheme = SettingsService.Current.Appearance.GetCalculatedElementTheme();
                }
            }

            var request = _currentDialogShowRequest = new TaskCompletionSource<ContentDialog>();
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
        /// <exception cref="InvalidOperationException">This method can only be invoked from UI thread.</exception>
        public static async Task<ContentDialogResult> ShowIfPossibleAsync(this ContentDialog dialog, XamlRoot xamlRoot)
        {
            while (_currentDialogShowRequest != null)
            {
                return ContentDialogResult.None;
            }

            dialog.XamlRoot = xamlRoot;

            if (dialog.XamlRoot.Content is FrameworkElement element)
            {
                var app = BootStrapper.Current.RequestedTheme == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
                var frame = element.RequestedTheme;

                if (app != frame)
                {
                    dialog.RequestedTheme = SettingsService.Current.Appearance.GetCalculatedElementTheme();
                }
            }

            var request = _currentDialogShowRequest = new TaskCompletionSource<ContentDialog>();
            var result = await dialog.ShowAsync();
            _currentDialogShowRequest = null;
            request.SetResult(dialog);

            return result;
        }
    }
}
