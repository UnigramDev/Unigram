//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Navigation
{
    // DOCS: https://github.com/Windows-XAML/Template10/wiki/Docs-%7C-MVVM
    public abstract class ViewModelBase : BindableBase
    {
        protected virtual Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            return Task.CompletedTask;
        }

        protected virtual Task OnNavigatedFromAsync(NavigationState suspensionState, bool suspending)
        {
            return Task.CompletedTask;
        }

        public virtual void NavigatingFrom(NavigatingEventArgs args)
        {

        }

        public virtual INavigationService NavigationService { get; set; }

        public virtual IDispatcherContext Dispatcher { get; set; }

        public virtual IDictionary<string, object> SessionState { get; set; }

        public Task<ContentDialogResult> ShowPopupAsync(ContentPopup popup)
        {
            return popup.ShowQueuedAsync();
        }

        public void ShowPopup(ContentPopup popup)
        {
            _ = popup.ShowQueuedAsync();
        }

        public Task<ContentDialogResult> ShowPopupAsync(Type sourcePopupType, object parameter = null, TaskCompletionSource<object> tsc = null)
        {
            return NavigationService.ShowPopupAsync(sourcePopupType, parameter, tsc);
        }

        public Task<ContentDialogResult> ShowPopupAsync(string message, string title = null, string primary = null, string secondary = null, bool dangerous = false)
        {
            return MessagePopup.ShowAsync(message, title, primary, secondary, dangerous);
        }

        public Task<ContentDialogResult> ShowPopupAsync(FormattedText message, string title = null, string primary = null, string secondary = null, bool dangerous = false)
        {
            return MessagePopup.ShowAsync(message, title, primary, secondary, dangerous);
        }

        public void ShowPopup(string message, string title = null, string primary = null, string secondary = null, bool dangerous = false)
        {
            _ = MessagePopup.ShowAsync(message, title, primary, secondary, dangerous);
        }

        public void ShowPopup(FormattedText message, string title = null, string primary = null, string secondary = null, bool dangerous = false)
        {
            _ = MessagePopup.ShowAsync(message, title, primary, secondary, dangerous);
        }
    }
}