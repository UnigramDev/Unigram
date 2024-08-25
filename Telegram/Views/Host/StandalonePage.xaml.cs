//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Keyboard;
using Windows.ApplicationModel.Core;

namespace Telegram.Views.Host
{
    public class StandaloneViewModel : ViewModelBase
    {
        public StandaloneViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }
    }

    public sealed partial class StandalonePage : Page, IToastHost
    {
        private readonly INavigationService _navigationService;
        private readonly IShortcutsService _shortcutsService;

        public StandalonePage(INavigationService navigationService)
        {
            RequestedTheme = SettingsService.Current.Appearance.GetCalculatedElementTheme();
            InitializeComponent();

            _navigationService = navigationService;
            _shortcutsService = TypeResolver.Current.Resolve<IShortcutsService>(navigationService.SessionId);

            //Grid.SetRow(navigationService.Frame, 2);
            //LayoutRoot.Children.Add(navigationService.Frame);

#if DEBUG && !MOCKUP
            StateLabel.Text = Strings.AppName;
#else
            StateLabel.Text = "Unigram";
#endif

            var clientService = TypeResolver.Current.Resolve<IClientService>(navigationService.SessionId);
            var settingsService = TypeResolver.Current.Resolve<ISettingsService>(navigationService.SessionId);
            var aggregator = TypeResolver.Current.Resolve<IEventAggregator>(navigationService.SessionId);

            MasterDetail.Initialize(navigationService as NavigationService, null, new StandaloneViewModel(clientService, settingsService, aggregator), false);
            MasterDetail.NavigationService.FrameFacade.Navigating += OnNavigating;

            OnNavigating(null, new NavigatingEventArgs(null, null, null, null)
            {
                SourcePageType = MasterDetail.NavigationService.CurrentPageType
            });
        }

        public INavigationService NavigationService => _navigationService;

        public void Connect(TeachingTip toast)
        {
            if (_navigationService?.Frame != null)
            {
                _navigationService.Frame.Resources.Remove("TeachingTip");
                _navigationService.Frame.Resources.Add("TeachingTip", toast);
            }
        }

        public void Disconnect(TeachingTip toast)
        {
            if (_navigationService?.Frame != null && _navigationService.Frame.Resources.TryGetValue("TeachingTip", out object cached))
            {
                if (cached == toast)
                {
                    _navigationService.Frame.Resources.Remove("TeachingTip");
                }
            }
        }

        private void OnNavigating(object sender, NavigatingEventArgs e)
        {
            var allowed = e.SourcePageType == typeof(ChatPage) ||
                e.SourcePageType == typeof(ChatPinnedPage) ||
                e.SourcePageType == typeof(ChatThreadPage) ||
                e.SourcePageType == typeof(ChatScheduledPage) ||
                e.SourcePageType == typeof(ChatEventLogPage) ||
                e.SourcePageType == typeof(ChatSavedPage) ||
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
            var context = WindowContext.ForXamlRoot(this);
            if (context != null)
            {
                context.InputListener.KeyDown += OnAcceleratorKeyActivated;
            }

            InitializeTitleBar();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.FrameFacade.Navigating -= OnNavigating;
            MasterDetail.Dispose();

            var context = WindowContext.ForXamlRoot(this);
            if (context != null)
            {
                context.InputListener.KeyDown -= OnAcceleratorKeyActivated;
            }

            UnloadTitleBar();
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

        private void OnAcceleratorKeyActivated(Window sender, InputKeyDownEventArgs args)
        {
            var invoked = _shortcutsService.Process(args);
            if (invoked == null)
            {
                return;
            }

            foreach (var command in invoked.Commands)
            {
                ProcessAppCommands(command, args);
            }
        }

        private async void ProcessAppCommands(ShortcutCommand command, InputKeyDownEventArgs args)
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
        }
    }
}
