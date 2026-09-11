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
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.DataTransfer.ShareTarget;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Host
{
    public sealed partial class ShareWindow : WindowContent
    {
        public ShareWindow(WindowContext window, ISession session)
            : base(window)
        {
            InitializeComponent();

            Background.Update(session.Resolve<IClientService>());

            StateLabel.Text = Constants.RELEASE
                ? Strings.AppDisplayName
                : Strings.AppName;
        }

        public async void Activate(ShareTargetActivatedEventArgs args, INavigationService navigationService, AuthorizationState state)
        {
            WatchDog.TrackEvent("ShareTarget");

            if (state is AuthorizationStateReady)
            {
                var popup = new ChooseChatsPopup();
                popup.IsSmokeEnabled = false;
                popup.IsLightDismissEnabled = false;
                popup.Closed += OnClosed;
                popup.AccountClick += OnAccountClick;

                navigationService.ShowPopup(popup, new ChooseChatsConfigurationShareOperation(args.ShareOperation));
            }
            else
            {
                try
                {
                    var options = new Windows.System.LauncherOptions();
                    options.TargetApplicationPackageFamilyName = Package.Current.Id.FamilyName;

                    await Windows.System.Launcher.LaunchUriAsync(new Uri("tg://"), options);
                }
                catch
                {
                    // It's too early?
                }
            }
        }

        private void ShowPopup(ISession session, ShareOperation shareOperation)
        {
            var popup = new ChooseChatsPopup();
            popup.IsSmokeEnabled = false;
            popup.IsLightDismissEnabled = false;
            popup.Closed += OnClosed;
            popup.AccountClick += OnAccountClick;

            var service = new TLNavigationService(session, Window, null, "Share");

            service.ShowPopup(popup, new ChooseChatsConfigurationShareOperation(shareOperation));
        }

        private void OnAccountClick(object sender, EventArgs e)
        {
            // The window's XamlRoot, not this control's: the passcode lock swaps the window
            // content out, so this root is detached - and its XamlRoot null - while it is up.
            foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(Window.XamlRoot))
            {
                if (popup.Child is ChooseChatsPopup chooseChats && chooseChats.ViewModel.Configuration is ChooseChatsConfigurationShareOperation shareOperation)
                {
                    chooseChats.Closed -= OnClosed;
                    chooseChats.AccountClick -= OnAccountClick;
                    chooseChats.Hide();

                    ShowPopup(sender as ISession, shareOperation.ShareOperation);
                }
            }
        }

        private void OnClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            if (sender is not ChooseChatsPopup chooseChats)
            {
                return;
            }

            chooseChats.Closed -= OnClosed;
            chooseChats.AccountClick -= OnAccountClick;

            if (args.Result != ContentDialogResult.Primary && chooseChats.ViewModel.Configuration is ChooseChatsConfigurationShareOperation shareOperation)
            {
                shareOperation.ShareOperation.TryReportCompleted();
            }
        }
    }
}
