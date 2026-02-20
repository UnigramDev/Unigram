//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using LinqToVisualTree;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Entities;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Chats
{
    public sealed partial class ChatAccountInfo : UserControl
    {
        private DialogViewModel _viewModel;

        public ChatAccountInfo()
        {
            InitializeComponent();

            DataContextChanged += OnDataContextChanged;
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ChatAccountInfoAutomationPeer(this);
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= OnPropertyChanged;
            }

            _viewModel = args.NewValue as DialogViewModel;

            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += OnPropertyChanged;
            }
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_viewModel.GroupsInCommon))
            {
                UpdateGroupsInCommon();
            }
        }

        private void UpdateGroupsInCommon()
        {
            if (_viewModel?.GroupsInCommon?.TotalCount > 0)
            {
                GroupsInfo.Visibility = Visibility.Visible;
                Groups.Visibility = Visibility.Visible;
                Groups.Text = Locale.Declension(Strings.R.Groups, _viewModel.GroupsInCommon.TotalCount);
            }
            else
            {
                GroupsInfo.Visibility = Visibility.Collapsed;
                Groups.Visibility = Visibility.Collapsed;
            }
        }

        public void Update(IClientService clientService, User user, UserFullInfo fullInfo, AccountInfo info)
        {
            Title.Text = user.FullName();
            Status.Text = user.IsContact
                ? Strings.ContactInfoIsContact
                : Strings.ContactInfoIsNotContact;

            if (Country.Codes.TryGetValue(info.PhoneNumberCountryCode, out Country country))
            {
                Phone.Text = string.Format("{0} {1}", country.Emoji, country.DisplayName);
                Phone.Visibility = Visibility.Visible;
                PhoneInfo.Visibility = Visibility.Visible;
            }
            else
            {
                Phone.Visibility = Visibility.Collapsed;
                PhoneInfo.Visibility = Visibility.Collapsed;
            }

            if (info.RegistrationYear != 0 && info.RegistrationMonth != 0)
            {
                var date = new DateTime(info.RegistrationYear, info.RegistrationMonth, 1);
                var format = Formatter.Date(date, Strings.formatterMonthYear);

                Registration.Text = format;
                Registration.Visibility = Visibility.Visible;
                RegistrationInfo.Visibility = Visibility.Visible;
            }
            else
            {
                Registration.Visibility = Visibility.Collapsed;
                RegistrationInfo.Visibility = Visibility.Collapsed;
            }

            if (info.LastNameChangeDate != 0)
            {
                FullNameInfo.Visibility = Visibility.Visible;
                FullName.Text = string.Format(Strings.ContactInfoUserUpdatedName, Formatter.RelativeDate(DateTime.Now.ToTimestamp() - info.LastNameChangeDate));
            }
            else
            {
                FullNameInfo.Visibility = Visibility.Collapsed;
            }

            if (info.LastPhotoChangeDate != 0)
            {
                PhotoInfo.Visibility = Visibility.Visible;
                Photo.Text = string.Format(Strings.ContactInfoUserUpdatedPhoto, Formatter.RelativeDate(DateTime.Now.ToTimestamp() - info.LastPhotoChangeDate));
            }
            else
            {
                PhotoInfo.Visibility = Visibility.Collapsed;
            }

            if (fullInfo.BotVerification != null && clientService.TryGetUser(fullInfo.BotVerification.BotUserId, out User verifierBotUser))
            {
                var emoji = new CustomEmojiFileSource(clientService, fullInfo.BotVerification.IconCustomEmojiId);
                var text = fullInfo.BotVerification.CustomDescription.Text.Length > 0
                    ? fullInfo.BotVerification.CustomDescription
                    : Strings.BotVerifierRepresentatives.AsFormattedText();

                TextBlockHelper.SetFormattedText(BotVerifiedText, text);

                BotVerifiedInfo.Source = emoji;
                BotVerifiedInfo.Visibility = Visibility.Visible;

                BotVerifiedRoot.Visibility = Visibility.Visible;
            }
            else
            {
                BotVerifiedText.Inlines.Clear();
                BotVerifiedText.Inlines.Add(Strings.ContactInfoNotVerified);

                BotVerifiedInfo.Source = null;
                BotVerifiedInfo.Visibility = Visibility.Collapsed;

                BotVerifiedRoot.Visibility = Visibility.Visible;
            }
        }
    }

    public partial class ChatAccountInfoAutomationPeer : FrameworkElementAutomationPeer
    {
        private readonly ChatAccountInfo _owner;

        public ChatAccountInfoAutomationPeer(ChatAccountInfo owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override string GetNameCore()
        {
            var builder = new StringBuilder();
            var descendants = _owner.DescendantsAndSelf();

            foreach (UIElement child in descendants.Where(x => x is TextBlock or RichTextBlock))
            {
                var view = AutomationProperties.GetAccessibilityView(child);
                if (view == AccessibilityView.Raw)
                {
                    continue;
                }

                var peer = FrameworkElementAutomationPeer.FromElement(child);
                if (peer == null)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(peer.GetName());
            }

            return builder.ToString();
        }
    }
}
