//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Windows.ApplicationModel.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Host
{
    public partial class StandaloneViewModel : ViewModelBase
    {
        public StandaloneViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }
    }

    public sealed partial class StandalonePage : Page, IPopupHost
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        public StandalonePage(INavigationService navigationService)
        {
            InitializeComponent();

            _clientService = navigationService.Session.Resolve<IClientService>();
            _navigationService = navigationService;

            //Grid.SetRow(navigationService.Frame, 2);
            //LayoutRoot.Children.Add(navigationService.Frame);

            StateLabel.Text = Constants.RELEASE
                ? Strings.AppDisplayName
                : Strings.AppName;

            var settingsService = navigationService.Session.Resolve<ISettingsService>();
            var aggregator = navigationService.Session.Resolve<IEventAggregator>();

            MasterDetail.Initialize(navigationService as NavigationService, null, new StandaloneViewModel(_clientService, settingsService, aggregator), false);
            MasterDetail.NavigationService.FrameFacade.Navigating += OnNavigating;
            MasterDetail.NavigationService.FrameFacade.ShortcutInvoked += OnShortcutInvoked;

            OnNavigating(null, new NavigatingEventArgs(null, null, null, null)
            {
                SourcePageType = MasterDetail.NavigationService.CurrentPageType
            });
        }

        public INavigationService NavigationService => _navigationService;

        public void PopupOpened()
        {
            NavigationService.Window.SetTitleBar(null);
        }

        public void PopupClosed()
        {
            NavigationService.Window.SetTitleBar(TitleBarHandle);
        }

        private void OnNavigating(object sender, NavigatingEventArgs e)
        {
            var allowed = e.SourcePageType == typeof(ChatPage) ||
                e.SourcePageType == typeof(ChatPinnedPage) ||
                e.SourcePageType == typeof(ChatScheduledPage) ||
                e.SourcePageType == typeof(ChatEventLogPage) ||
                e.SourcePageType == typeof(ChatBusinessRepliesPage) ||
                e.SourcePageType == typeof(BlankPage);

            var type = allowed ? BackgroundKind.Background : BackgroundKind.Material;

            if (MasterDetail.CurrentState == MasterDetailState.Minimal && e.SourcePageType == typeof(BlankPage))
            {
                type = BackgroundKind.None;
            }

            if (MasterDetail.CurrentState != MasterDetailState.Unknown)
            {
                MasterDetail.ShowHideBackground(type, true);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            InitializeTitleBar();

            ShowHideBanner(LifetimeService.Current.Playback);
            LifetimeService.Current.Playback.SourceChanged += OnPlaybackSourceChanged;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            //MasterDetail.NavigationService.FrameFacade.Navigating -= OnNavigating;
            //MasterDetail.Dispose();

            UnloadTitleBar();
            LifetimeService.Current.Playback.SourceChanged -= OnPlaybackSourceChanged;
        }

        private void InitializeTitleBar()
        {
            var sender = CoreApplication.GetCurrentView().TitleBar;
            sender.IsVisibleChanged += OnLayoutMetricsChanged;
            sender.LayoutMetricsChanged += OnLayoutMetricsChanged;

            OnLayoutMetricsChanged(sender, null);
        }

        private void UnloadTitleBar()
        {
            var sender = CoreApplication.GetCurrentView().TitleBar;
            sender.IsVisibleChanged -= OnLayoutMetricsChanged;
            sender.LayoutMetricsChanged -= OnLayoutMetricsChanged;
        }

        private void OnLayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            try
            {
                TitleBarrr.ColumnDefinitions[0].Width = new GridLength(Math.Max(sender.SystemOverlayLeftInset, 0), GridUnitType.Pixel);
                TitleBarrr.ColumnDefinitions[4].Width = new GridLength(Math.Max(sender.SystemOverlayRightInset, 0), GridUnitType.Pixel);

                Grid.SetColumn(TitleBarLogo, sender.SystemOverlayLeftInset > 0 ? 3 : 1);
                StateLabel.FlowDirection = sender.SystemOverlayLeftInset > 0
                    ? FlowDirection.RightToLeft
                    : FlowDirection.LeftToRight;
            }
            catch
            {
                // Most likely InvalidComObjectException
            }
        }

        private void OnShortcutInvoked(object sender, ShortcutInvokedEventArgs args)
        {
            foreach (var command in args.Shortcut.Commands)
            {
                ProcessAppCommands(command, args);
            }
        }

        private async void ProcessAppCommands(ShortcutCommand command, ShortcutInvokedEventArgs args)
        {
            if (command == ShortcutCommand.Search)
            {
                if (_navigationService.Frame.Content is ISearchablePage child)
                {
                    child.Search();
                }

                args.Handled = true;
            }
            else if (command == ShortcutCommand.Close)
            {
                await WindowContext.Current.ConsolidateAsync();
            }
            else if (command == ShortcutCommand.MediaStop)
            {
                LifetimeService.Current.Playback.Clear();
                args.Handled = true;
            }
        }

        private void Banner_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            MasterDetail.BackgroundMargin = new Thickness(0, -e.NewSize.Height, 0, 0);
        }

        private void OnPlaybackSourceChanged(IPlaybackService sender, object args)
        {
            this.BeginOnUIThread(() => ShowHideBanner(sender));
        }

        private void ShowHideBanner(IPlaybackService sender)
        {
            if (sender.CurrentItem != null && Playback == null)
            {
                FindName(nameof(Playback));
                Playback.Update(_clientService, _navigationService);
            }
        }
    }
}
