//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Settings;
using Telegram.ViewModels.Settings.Privacy;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Settings.Privacy
{
    public sealed partial class SettingsPrivacyShowForwardedPage : HostedPage
    {
        public SettingsPrivacyShowForwardedViewModel ViewModel => DataContext as SettingsPrivacyShowForwardedViewModel;

        public SettingsPrivacyShowForwardedPage()
        {
            InitializeComponent();
            Title = Strings.PrivacyForwards;

            if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.UIElement", "Shadow"))
            {
                var themeShadow = new ThemeShadow();
                ToolTip.Shadow = themeShadow;
                ToolTip.Translation += new Vector3(0, 0, 32);

                themeShadow.Receivers.Add(BackgroundControl);
                themeShadow.Receivers.Add(MessagePreview);
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            UpdateMessage();
            BackgroundControl.Update(ViewModel.ClientService, ViewModel.Aggregator);

            ViewModel.PropertyChanged += OnPropertyChanged;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.SelectedItem))
            {
                UpdateMessage();
            }
        }

        private void UpdateMessage()
        {
            var user = ViewModel.ClientService.GetUser(ViewModel.ClientService.Options.MyId);
            if (user != null && ViewModel.ClientService.TryGetChat(user.Id, out Chat chat))
            {
                MessageOrigin origin = ViewModel.SelectedItem == PrivacyValue.DisallowAll
                    ? new MessageOriginHiddenUser(user.FullName())
                    : new MessageOriginUser(user.Id);

                var forwardInfo = new MessageForwardInfo(origin, 0, null, string.Empty);
                var content = new MessageText(new FormattedText(Strings.PrivacyForwardsMessageLine, Array.Empty<TextEntity>()), null, null);

                var message = new Message(0, new MessageSenderUser(user.Id), 0, null, null, false, false, false, false, false, false, false, false, DateTime.Now.ToTimestamp(), 0, forwardInfo, null, null, Array.Empty<UnreadReaction>(), null, null, 0, 0, null, 0, 0, 0, 0, 0, string.Empty, 0, 0, string.Empty, content, null);

                var playback = TypeResolver.Current.Playback;
                var settings = TypeResolver.Current.Resolve<ISettingsService>(ViewModel.ClientService.SessionId);

                var delegato = new ChatMessageDelegate(ViewModel.ClientService, settings, chat);
                var viewModel = new MessageViewModel(ViewModel.ClientService, playback, delegato, chat, message, true);

                MessagePreview.UpdateMessage(viewModel);
            }
        }

        #region Binding

        private string ConvertToolTip(PrivacyValue value)
        {
            return value == PrivacyValue.AllowAll ? Strings.PrivacyForwardsEverybody : value == PrivacyValue.AllowContacts ? Strings.PrivacyForwardsContacts : Strings.PrivacyForwardsNobody;
        }

        private Visibility ConvertNever(PrivacyValue value)
        {
            return value is PrivacyValue.AllowAll or PrivacyValue.AllowContacts ? Visibility.Visible : Visibility.Collapsed;
        }

        private Visibility ConvertAlways(PrivacyValue value)
        {
            return value is PrivacyValue.AllowContacts or PrivacyValue.DisallowAll ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

    }
}
