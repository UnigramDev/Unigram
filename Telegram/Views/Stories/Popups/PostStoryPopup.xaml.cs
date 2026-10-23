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
using Telegram.Controls.Chats;
using Telegram.Controls.Drawers;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;
using Telegram.Views.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;

namespace Telegram.Views.Stories.Popups
{
    /// <summary>
    /// What is asked before a story is posted: its caption, who can view it, whether it can be
    /// saved, whether it stays on the profile, and how long it lasts.
    /// </summary>
    public sealed partial class PostStoryPopup : ModalPopup, IViewWithAutocomplete
    {
        // The caption's counter shows once fewer than this many characters are left.
        private const int CounterThreshold = 25;

        private readonly int _captionLimit;
        private int _captionLength;
        private bool _captionLimitToast;

        private CaptionController _caption;
        private IAutocompleteCollection _autocomplete;

        // In hours. Only Premium picks one other than a day.
        private static readonly int[] Periods = { 6, 12, 24, 48 };
        private const int DefaultPeriod = 24;

        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;
        private readonly StorySettings _settings;

        private List<long> _exceptFromEveryone;
        private List<long> _exceptFromContacts;
        private List<long> _selectedContacts;
        private List<long> _closeFriends;

        private int _period = DefaultPeriod;

        public PostStoryPopup(IClientService clientService, INavigationService navigationService)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;
            _settings = clientService.Session.Resolve<ISettingsService>().Stories;

            Title = Strings.StoryPrivacyAlertTitle;

            EmojiPanel.DataContext = EmojiDrawerViewModel.Create(clientService.Session);

            // Not a hard limit: past it the counter turns negative and the story cannot be posted,
            // so a pasted caption can still be trimmed rather than being cut off.
            _captionLimit = (int)clientService.Options.StoryCaptionLengthMax;

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

            // Starting from what the last story was posted with. A period other than a day stays
            // Premium's, whatever was saved.
            var period = clientService.IsPremium && Array.IndexOf(Periods, _settings.Period) != -1
                ? _settings.Period
                : DefaultPeriod;

            Period.SelectedIndex = Array.IndexOf(Periods, period);

            AllowScreenshots.IsChecked = _settings.AllowScreenshots;
            Keep.IsChecked = _settings.Keep;

            _exceptFromEveryone = _settings.ExceptFromEveryone;
            _exceptFromContacts = _settings.ExceptFromContacts;
            _selectedContacts = _settings.SelectedContacts;

            var option = _settings.Privacy switch
            {
                StoryPrivacyMode.Contacts => Contacts,
                StoryPrivacyMode.CloseFriends => CloseFriends,
                StoryPrivacyMode.SelectedContacts => SelectedContacts,
                _ => Everyone
            };

            option.IsChecked = true;

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

        #region Caption

        public IAutocompleteCollection Autocomplete
        {
            get => _autocomplete;
            set
            {
                _autocomplete = value;

                ListAutocomplete.ItemsSource = value;
                ListAutocomplete.Visibility = value != null
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                if (value != null)
                {
                    ListAutocomplete.Orientation = value.Orientation;
                }
            }
        }

        private void CaptionInput_Loaded(object sender, RoutedEventArgs e)
        {
            _caption ??= new CaptionController(CaptionInput, CaptionInput, this, _clientService)
            {
                ListAutocomplete = ListAutocomplete
            };
        }

        private void CaptionInput_Unloaded(object sender, RoutedEventArgs e)
        {
            _caption?.Detach();
            _caption = null;

            Autocomplete = null;
        }

        private void Autocomplete_ItemClick(object sender, ItemClickEventArgs e)
        {
            _caption?.Accept(e.ClickedItem);
        }

        private void Autocomplete_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            if (args.Item is User user && args.ItemContainer.ContentTemplateRoot is Grid content)
            {
                var photo = content.Children[0] as ProfilePicture;
                var title = content.Children[1] as TextBlock;

                var name = title.Inlines[0] as Run;
                var username = title.Inlines[1] as Run;

                name.Text = user.FullName();
                username.Text = user.HasActiveUsername(out string usernameValue)
                    ? $" @{usernameValue}"
                    : string.Empty;

                photo.Source = ProfilePictureSource.User(_clientService, user);
            }
            else if (args.Item is string hashtag && args.ItemContainer.ContentTemplateRoot is Grid hashtagContent)
            {
                var title = hashtagContent.Children[0] as TextBlock;
                title.Text = hashtag;
            }

            args.Handled = true;
        }

        // Counted in code points, as the server does.
        private void CaptionInput_ContentChanged(object sender, EventArgs e)
        {
            var text = CaptionInput.GetFormattedText()?.Text ?? string.Empty;
            var length = 0;

            for (int i = 0; i < text.Length; i++)
            {
                if (!char.IsLowSurrogate(text[i]))
                {
                    length++;
                }
            }

            var previous = _captionLength;
            _captionLength = length;

            if (_captionLimit <= 0)
            {
                return;
            }

            var left = _captionLimit - length;
            var full = left <= 0;

            CaptionCounter.Visibility = left < CounterThreshold
                ? Visibility.Visible
                : Visibility.Collapsed;

            CaptionCounterLeft.Text = CaptionCounterOver.Text = left.ToString();
            CaptionCounterLeft.Visibility = full ? Visibility.Collapsed : Visibility.Visible;
            CaptionCounterOver.Visibility = full ? Visibility.Visible : Visibility.Collapsed;

            Send.IsEnabled = left >= 0;

            if (left >= 0)
            {
                _captionLimitToast = false;
            }
            else if (length > previous && !_clientService.IsPremium && _clientService.IsPremiumAvailable)
            {
                ShowCaptionLimit();
            }
        }

        // Once each time the caption goes over, while it grows: Premium allows a longer one.
        private async void ShowCaptionLimit()
        {
            VisualUtilities.ShakeView(CaptionCounter);

            if (_captionLimitToast)
            {
                return;
            }

            _captionLimitToast = true;

            var response = await _clientService.SendAsync(new GetPremiumLimit(new PremiumLimitTypeStoryCaptionLength()));
            if (response is not PremiumLimit limit || limit.DefaultValue <= 0 || _captionLength >= limit.PremiumValue)
            {
                return;
            }

            var times = (int)Math.Round((double)limit.PremiumValue / limit.DefaultValue);
            var text = $"**{Strings.CaptionPremiumTitle}**\n" + Locale.Declension(Strings.R.CaptionPremiumSubtitle, times, limit.PremiumValue);

            ToastPopup.ShowPromo(_navigationService, text, Strings.PremiumMore, new PremiumSourceLimitExceeded(new PremiumLimitTypeStoryCaptionLength()));
        }

        #endregion

        private void Accept()
        {
            if (_captionLimit > 0 && _captionLength > _captionLimit)
            {
                VisualUtilities.ShakeView(CaptionCounter);
                return;
            }

            // Typed formatting is already refused, but a contact picked as a mention still adds an entity.
            if (!_clientService.Options.CanUseTextEntitiesInStoryCaption && Caption?.Entities?.Count > 0)
            {
                ToastPopup.ShowFeaturePromo(_navigationService, Strings.StoryPremiumFormatting, new PremiumFeatureUpgradedStories());
                return;
            }

            // A story for selected contacts needs some to be for.
            if (SelectedContacts.IsChecked == true && _selectedContacts.Count == 0)
            {
                EditList(SelectedContacts);
                return;
            }

            _settings.Privacy = Contacts.IsChecked == true ? StoryPrivacyMode.Contacts
                : CloseFriends.IsChecked == true ? StoryPrivacyMode.CloseFriends
                : SelectedContacts.IsChecked == true ? StoryPrivacyMode.SelectedContacts
                : StoryPrivacyMode.Everyone;

            _settings.ExceptFromEveryone = _exceptFromEveryone;
            _settings.ExceptFromContacts = _exceptFromContacts;
            _settings.SelectedContacts = _selectedContacts;
            _settings.AllowScreenshots = AllowScreenshots.IsChecked == true;
            _settings.Keep = Keep.IsChecked == true;
            _settings.Period = _period;

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
