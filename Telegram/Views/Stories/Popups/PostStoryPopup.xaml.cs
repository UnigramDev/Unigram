//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Drawers;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;
using Telegram.Views.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Views.Stories.Popups
{
    /// <summary>
    /// What is asked before a story is posted: its caption, who can view it, whether it can be
    /// saved, whether it stays on the profile, and how long it lasts.
    /// </summary>
    public sealed partial class PostStoryPopup : ModalPopup
    {
        // In hours. Only Premium picks one other than a day.
        private static readonly int[] Periods = { 6, 12, 24, 48 };
        private const int DefaultPeriod = 24;

        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        private List<long> _exceptFromEveryone = new();
        private List<long> _exceptFromContacts = new();
        private List<long> _selectedContacts = new();
        private List<long> _closeFriends;

        private int _period = DefaultPeriod;

        public PostStoryPopup(IClientService clientService, INavigationService navigationService)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            Title = Strings.StoryPrivacyAlertTitle;

            EmojiPanel.DataContext = EmojiDrawerViewModel.Create(clientService.Session);

            CaptionInput.MaxLength = (int)clientService.Options.StoryCaptionLengthMax;

            if (!clientService.Options.CanUseTextEntitiesInStoryCaption)
            {
                CaptionInput.AllowedEntities = FormattedTextEntity.None;
            }

            // A period other than a day is Premium's, so there is nothing to choose where it
            // cannot be bought.
            if (!clientService.IsPremium && !clientService.IsPremiumAvailable)
            {
                PeriodRoot.Visibility = Visibility.Collapsed;
            }

            var periods = new List<string>();

            foreach (var hours in Periods)
            {
                periods.Add(Locale.Declension(Strings.R.Hours, hours));
            }

            Period.ItemsSource = periods;
            Period.SelectedIndex = Array.IndexOf(Periods, DefaultPeriod);

            Everyone.IsChecked = true;

            UpdateList();
            LoadCloseFriends();
        }

        public FormattedText Caption => CaptionInput.GetFormattedText();

        public StoryPrivacySettings PrivacySettings
        {
            get
            {
                if (Contacts.IsChecked == true)
                {
                    return new StoryPrivacySettingsContacts(new Vector<long>(_exceptFromContacts));
                }
                else if (CloseFriends.IsChecked == true)
                {
                    return new StoryPrivacySettingsCloseFriends();
                }
                else if (SelectedContacts.IsChecked == true)
                {
                    return new StoryPrivacySettingsSelectedUsers(new Vector<long>(_selectedContacts));
                }

                return new StoryPrivacySettingsEveryone(new Vector<long>(_exceptFromEveryone));
            }
        }

        // In seconds.
        public int ActivePeriod => _period * 3600;

        public bool IsPostedToChatPage => Keep.IsChecked == true;

        public bool ProtectContent => AllowScreenshots.IsChecked != true;

        private async void LoadCloseFriends()
        {
            if (await _clientService.SendAsync(new GetCloseFriends()) is Td.Api.Users users)
            {
                _closeFriends = users.UserIds.ToList();
                UpdateList();
            }
        }

        private void UpdateList()
        {
            EveryoneList.Content = _exceptFromEveryone.Count > 0
                ? Locale.Declension(Strings.R.StoryPrivacyOptionExcludePeople, _exceptFromEveryone.Count)
                : Strings.StoryPrivacyOptionContactsDetail;

            ContactsList.Content = _exceptFromContacts.Count > 0
                ? Locale.Declension(Strings.R.StoryPrivacyOptionExcludePeople, _exceptFromContacts.Count)
                : Strings.StoryPrivacyOptionContactsDetail;

            CloseFriendsList.Content = _closeFriends is { Count: > 0 }
                ? Locale.Declension(Strings.R.StoryPrivacyOptionPeople, _closeFriends.Count)
                : Strings.StoryPrivacyOptionCloseFriendsDetail;

            SelectedContactsList.Content = _selectedContacts.Count > 0
                ? Locale.Declension(Strings.R.StoryPrivacyOptionPeople, _selectedContacts.Count)
                : Strings.StoryPrivacyOptionSelectedContactsDetail;
        }

        // Editing an option's list chooses the option too.
        private void List_Click(object sender, RoutedEventArgs e)
        {
            var option = sender == EveryoneList ? Everyone
                : sender == ContactsList ? Contacts
                : sender == CloseFriendsList ? CloseFriends
                : SelectedContacts;

            option.IsChecked = true;
            EditList(option);
        }

        private async void EditList(RadioButton option)
        {
            string title;
            List<long> list;

            if (option == Everyone)
            {
                title = Strings.StoryPrivacyAlertExcludeFromEveryoneTitle;
                list = _exceptFromEveryone;
            }
            else if (option == Contacts)
            {
                title = Strings.StoryPrivacyAlertExcludedContactsTitle;
                list = _exceptFromContacts;
            }
            else if (option == CloseFriends)
            {
                title = Strings.StoryPrivacyAlertCloseFriendsTitle;
                list = _closeFriends ?? new List<long>();
            }
            else
            {
                title = Strings.StoryPrivacyAlertSelectContactsTitle;
                list = _selectedContacts;
            }

            // The picker preselects chats: a user's private chat has the user's id, once it is loaded.
            foreach (var userId in list)
            {
                await _clientService.SendAsync(new CreatePrivateChat(userId, true));
            }

            var chats = await ChooseChatsPopup.PickChatsAsync(_navigationService, title, list.ToArray(), ChooseChatsOptions.Contacts, ListViewSelectionMode.Multiple, true);
            if (chats == null)
            {
                return;
            }

            var users = new List<long>();

            foreach (var chat in chats)
            {
                if (chat.Type is ChatTypePrivate privateChat)
                {
                    users.Add(privateChat.UserId);
                }
            }

            if (option == Everyone)
            {
                _exceptFromEveryone = users;
            }
            else if (option == Contacts)
            {
                _exceptFromContacts = users;
            }
            else if (option == CloseFriends)
            {
                // The list is the account's, not the story's.
                _closeFriends = users;
                _clientService.Send(new SetCloseFriends(new Vector<long>(users)));
            }
            else
            {
                _selectedContacts = users;
            }

            UpdateList();
        }

        private void Period_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Period.SelectedIndex < 0)
            {
                return;
            }

            var hours = Periods[Period.SelectedIndex];

            // A locked period is not taken: the selection goes back to what was there.
            if (hours != DefaultPeriod && !_clientService.IsPremium)
            {
                ToastPopup.ShowPromo(_navigationService, Locale.Declension(Strings.R.StoryPeriodPremium, hours), Strings.OptionPremiumRequiredButton, new PremiumSourceStoryFeature(new PremiumStoryFeatureCustomExpirationDuration()));

                Period.SelectedIndex = Array.IndexOf(Periods, _period);
                return;
            }

            _period = hours;
            KeepInfo.Text = Locale.Declension(Strings.R.StoryKeepInfo, _period);
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            Accept();
        }

        private void CaptionInput_Submitted(object sender, EventArgs e)
        {
            Accept();
        }

        private void Accept()
        {
            // A story for selected contacts needs some to be for.
            if (SelectedContacts.IsChecked == true && _selectedContacts.Count == 0)
            {
                EditList(SelectedContacts);
                return;
            }

            Hide(ContentDialogResult.Primary);
        }

        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            EmojiPanel.ViewModel.Update();
            EmojiFlyout.ShowAt(CaptionPanel, FlyoutPlacementMode.Top, FlyoutShowMode.Transient, source: EmojiButton);
        }

        private void Emoji_ItemClick(object sender, EmojiDrawerItemClickEventArgs e)
        {
            if (e.ClickedItem is EmojiData emoji)
            {
                CaptionInput.InsertText(emoji.Value);
                CaptionInput.FocusInput(FocusState.Programmatic);
            }
            else if (e.ClickedItem is StickerViewModel sticker)
            {
                CaptionInput.InsertEmoji(sticker);
                CaptionInput.FocusInput(FocusState.Programmatic);
            }
        }
    }
}
