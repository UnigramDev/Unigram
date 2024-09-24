//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Controls;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.Views.Popups;

namespace Telegram.Navigation
{
    public abstract class ViewModelBase : BindableBase, INavigable
    {
        private readonly IClientService _clientService;
        private readonly ISettingsService _settingsService;
        private readonly IEventAggregator _aggregator;

        public ViewModelBase(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
        {
            _clientService = clientService;
            _settingsService = settingsService;
            _aggregator = aggregator;
        }

        #region Navigation

        public virtual Task NavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (this is IHandle)
            {
                Subscribe();
            }

            return OnNavigatedToAsync(parameter, mode, state);
        }

        protected virtual Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            return Task.CompletedTask;
        }

        public virtual void NavigatedFrom(NavigationState suspensionState, bool suspending)
        {
            if (this is IHandle)
            {
                Unsubscribe();
            }

            OnNavigatedFrom(suspensionState, suspending);
        }

        protected virtual void OnNavigatedFrom(NavigationState suspensionState, bool suspending)
        {

        }

        public virtual void NavigatingFrom(NavigatingEventArgs args)
        {

        }

        public virtual void Subscribe()
        {

        }

        public void Unsubscribe()
        {
            Aggregator.Unsubscribe(this);
        }

        #endregion

        public IClientService ClientService => _clientService;

        public ISettingsService Settings => _settingsService;

        public IEventAggregator Aggregator => _aggregator;

        public int SessionId => _clientService.SessionId;

        public bool IsPremium => _clientService.IsPremium;
        public bool IsPremiumAvailable => _clientService.IsPremiumAvailable;

        private bool _isLoading;
        public virtual bool IsLoading
        {
            get => _isLoading;
            set => Set(ref _isLoading, value);
        }

        // TODO: not great, only used by ShowPopupAsync extension
        private XamlRoot _xamlRoot;
        public XamlRoot XamlRoot
        {
            get => NavigationService?.XamlRoot ?? _xamlRoot;
            set => _xamlRoot = value;
        }

        public virtual INavigationService NavigationService { get; set; }

        public virtual IDispatcherContext Dispatcher { get; set; }

        public virtual IDictionary<string, object> SessionState { get; set; }

        #region Popups

        public Task<ContentDialogResult> ShowPopupAsync(ContentPopup popup, object parameter = null, ElementTheme requestedTheme = ElementTheme.Default)
        {
            return NavigationService.ShowPopupAsync(popup, parameter, requestedTheme);
        }

        public void ShowPopup(ContentPopup popup, object parameter = null, ElementTheme requestedTheme = ElementTheme.Default)
        {
            _ = NavigationService.ShowPopupAsync(popup, parameter, requestedTheme);
        }

        public virtual Task<ContentDialogResult> ShowPopupAsync(string message, string title = null, string primary = null, string secondary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default)
        {
            if (ContentPopup.IsAnyPopupOpen(XamlRoot))
            {
                return MessagePopup.ShowAsync(XamlRoot, target: null, message, title, primary, secondary, destructive, requestedTheme);
            }

            return MessagePopup.ShowAsync(XamlRoot, message, title, primary, secondary, destructive, requestedTheme);
        }

        public Task<ContentDialogResult> ShowPopupAsync(FrameworkElement target, string message, string title = null, string primary = null, string secondary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default)
        {
            return MessagePopup.ShowAsync(XamlRoot, target, message, title, primary, secondary, destructive, requestedTheme);
        }

        public Task<ContentDialogResult> ShowPopupAsync(FormattedText message, string title = null, string primary = null, string secondary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default)
        {
            return MessagePopup.ShowAsync(XamlRoot, message, title, primary, secondary, destructive, requestedTheme);
        }

        //public Task<ContentDialogResult> ShowPopupAsync(FrameworkElement target, FormattedText message, string title = null, string primary = null, string secondary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default)
        //{
        //    return MessagePopup.ShowAsync(target, message, title, primary, secondary, destructive, requestedTheme);
        //}

        public void ShowPopup(string message, string title = null, string primary = null, string secondary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default)
        {
            _ = MessagePopup.ShowAsync(XamlRoot, message, title, primary, secondary, destructive, requestedTheme);
        }

        public void ShowPopup(FormattedText message, string title = null, string primary = null, string secondary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default)
        {
            _ = MessagePopup.ShowAsync(XamlRoot, message, title, primary, secondary, destructive, requestedTheme);
        }

        public Task<InputPopupResult> ShowInputAsync(InputPopupType type, string message, string title = null, string placeholderText = null, string primary = null, string secondary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default)
        {
            return InputPopup.ShowAsync(XamlRoot, type, message, title, placeholderText, primary, secondary, destructive, requestedTheme);
        }

        public Task<InputPopupResult> ShowInputAsync(FrameworkElement target, InputPopupType type, string message, string title = null, string placeholderText = null, string primary = null, string secondary = null, bool destructive = false, ElementTheme requestedTheme = ElementTheme.Default)
        {
            return InputPopup.ShowAsync(XamlRoot, target, type, message, title, placeholderText, primary, secondary, destructive, requestedTheme);
        }

        public ToastPopup ShowToast(string text, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return ToastPopup.Show(XamlRoot, ClientEx.ParseMarkdown(text), null, TeachingTipPlacementMode.Center, requestedTheme, dismissAfter);
        }

        public ToastPopup ShowToast(string text, ToastPopupIcon icon, ElementTheme requestedTheme = ElementTheme.Dark, TimeSpan? dismissAfter = null)
        {
            return ToastPopup.Show(XamlRoot, ClientEx.ParseMarkdown(text), icon, TeachingTipPlacementMode.Center, requestedTheme, dismissAfter);
        }

        #endregion

        public virtual void BeginOnUIThread(DispatcherQueueHandler action)
        {
            var dispatcher = Dispatcher;
            dispatcher ??= WindowContext.Main?.Dispatcher;

            if (dispatcher != null)
            {
                dispatcher.Dispatch(action);
            }
            else
            {
                try
                {
                    action();
                }
                catch { }
            }
        }
    }
}