//
// Copyright Fela Ameghino 2015-2023
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
using Telegram.Services.Keyboard;
using Windows.ApplicationModel.Core;
using Windows.System.Profile;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Host
{
    public sealed partial class StandalonePage : Page
    {
        private readonly INavigationService _navigationService;
        private readonly IShortcutsService _shortcutsService;

        public StandalonePage(INavigationService navigationService)
        {
            RequestedTheme = SettingsService.Current.Appearance.GetCalculatedElementTheme();
            InitializeComponent();

            InitializeTitleBar();

            _navigationService = navigationService;
            _shortcutsService = TLContainer.Current.Resolve<IShortcutsService>(navigationService.SessionId);

            Grid.SetRow(navigationService.Frame, 2);
            LayoutRoot.Children.Add(navigationService.Frame);

            if (navigationService is TLNavigationService service && service.ClientService != null)
            {
                var user = service.ClientService.GetUser(service.ClientService.Options.MyId);
                if (user != null)
                {
                    StatusLabel.Text = string.Format("{0} - {1}", user.FullName(), "Unigram");
                    ApplicationView.GetForCurrentView().Title = user.FullName();
                }
            }

            navigationService.Frame.Navigated += OnNavigated;

            if (navigationService.Frame.Content is HostedPage hosted)
            {
                PageHeader.Content = hosted.Header;
            }
            else
            {
                PageHeader.Content = null;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            WindowContext.Current.InputListener.KeyDown += OnAcceleratorKeyActivated;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            WindowContext.Current.InputListener.KeyDown -= OnAcceleratorKeyActivated;
        }

        private void OnNavigated(object sender, Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            if (e.Content is HostedPage hosted)
            {
                PageHeader.Content = hosted.Header;
            }
            else
            {
                PageHeader.Content = null;
            }
        }

        private void InitializeTitleBar()
        {
            var sender = CoreApplication.GetCurrentView().TitleBar;

            if (string.Equals(AnalyticsInfo.VersionInfo.DeviceFamily, "Windows.Desktop") && UIViewSettings.GetForCurrentView().UserInteractionMode == UserInteractionMode.Mouse)
            {
                // If running on PC and tablet mode is disabled, then titlebar is most likely visible
                // So we're going to force it
                Navigation.Padding = new Thickness(48, 0, 0, 0);
                Navigation.Height = 32;
            }
            else
            {
                Navigation.Padding = new Thickness(sender.IsVisible ? Math.Max(sender.SystemOverlayLeftInset, 6) : 0, 0, 0, 0);
                Navigation.Height = sender.IsVisible ? sender.Height : 0;
            }

            sender.ExtendViewIntoTitleBar = true;
            sender.IsVisibleChanged += CoreTitleBar_LayoutMetricsChanged;
            sender.LayoutMetricsChanged += CoreTitleBar_LayoutMetricsChanged;
        }

        private void CoreTitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            Navigation.Padding = new Thickness(sender.IsVisible ? Math.Max(sender.SystemOverlayLeftInset, 6) : 0, 0, 0, 0);
            Navigation.Height = sender.IsVisible ? sender.Height : 0;

            var popups = VisualTreeHelper.GetOpenPopups(Window.Current);
            foreach (var popup in popups)
            {
                if (popup.Child is OverlayPage contentDialog)
                {
                    contentDialog.Padding = new Thickness(0, sender.IsVisible ? sender.Height : 0, 0, 0);
                }
            }
        }

        private void OnAcceleratorKeyActivated(Window sender, InputKeyDownEventArgs args)
        {
            var invoked = _shortcutsService.Process(args);

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
                await ApplicationView.GetForCurrentView().ConsolidateAsync();
            }
        }
    }
}
