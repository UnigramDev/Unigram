//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Settings;
using Telegram.Views.Chats.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Supergroups
{
    public enum ChatBoostFeature
    {
        None,
        AccentColor,
        BackgroundCustomEmoji,
        ProfileAccentColor,
        ProfileBackgroundCustomEmoji,
        EmojiStatus,
        ChatTheme,
        CustomBackground,
    }

    public class SupergroupProfileColorViewModel : ViewModelBase
    {
        private ChatBoostStatus _status;
        private ChatBoostFeatures _features;
        private bool _confirmed;

        public SupergroupProfileColorViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            var defaultTheme = new ChatThemeViewModel(ClientService, "\u274C", null, null, true);
            var themes = ClientService.GetChatThemes().Select(x => new ChatThemeViewModel(ClientService, x, true));

            ChatThemes = new ObservableCollection<ChatThemeViewModel>(new[] { defaultTheme }.Union(themes));
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var chatId = (long)parameter;

            Chat = ClientService.GetChat(chatId);

            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            SelectedChatTheme = string.IsNullOrEmpty(chat.ThemeName) ? ChatThemes[0] : ChatThemes.FirstOrDefault(x => x.Name == chat.ThemeName);

            var response1 = await ClientService.SendAsync(new GetChatBoostFeatures());
            var response2 = await ClientService.SendAsync(new GetChatBoostStatus(chat.Id));

            if (response1 is ChatBoostFeatures features && response2 is ChatBoostStatus status)
            {
                _features = features;
                _status = status;

                MinChatThemeBackgroundBoostLevel = features.MinChatThemeBackgroundBoostLevel;
                MinCustomBackgroundBoostLevel = features.MinCustomBackgroundBoostLevel;
                MinBackgroundCustomEmojiBoostLevel = features.MinBackgroundCustomEmojiBoostLevel;
                MinProfileBackgroundCustomEmojiBoostLevel = features.MinProfileBackgroundCustomEmojiBoostLevel;
                MinEmojiStatusBoostLevel = features.MinEmojiStatusBoostLevel;

                RaisePropertyChanged(nameof(MinChatThemeBackgroundBoostLevel));
                RaisePropertyChanged(nameof(MinCustomBackgroundBoostLevel));
                RaisePropertyChanged(nameof(MinBackgroundCustomEmojiBoostLevel));
                RaisePropertyChanged(nameof(MinProfileBackgroundCustomEmojiBoostLevel));
                RaisePropertyChanged(nameof(MinEmojiStatusBoostLevel));
            }
        }

        public override void NavigatingFrom(NavigatingEventArgs args)
        {
            if (_confirmed || _status == null || Chat is not Chat chat)
            {
                return;
            }

            var required = UpdateRequiredLevel(out _);
            if (required > _status.Level)
            {
                return;
            }

            var changed = false;

            var accentColorId = SelectedAccentColor?.Id ?? -1;
            var profileAccentColorId = SelectedProfileAccentColor?.Id ?? -1;

            if (accentColorId != chat.AccentColorId || SelectedCustomEmojiId != chat.BackgroundCustomEmojiId)
            {
                changed = true;
            }

            if (profileAccentColorId != chat.ProfileAccentColorId || SelectedProfileCustomEmojiId != chat.ProfileBackgroundCustomEmojiId)
            {
                changed = true;
            }

            if (SelectedEmojiStatus?.CustomEmojiId != chat.EmojiStatus?.CustomEmojiId || SelectedEmojiStatus?.ExpirationDate != chat.EmojiStatus?.ExpirationDate)
            {
                changed = true;
            }

            if (SelectedChatTheme?.Name != chat.ThemeName)
            {
                changed = true;
            }

            if (changed)
            {
                ConfirmClose();
                args.Cancel = true;
            }
        }

        public int MinChatThemeBackgroundBoostLevel { get; private set; }

        public int MinCustomBackgroundBoostLevel { get; private set; }

        public int MinBackgroundCustomEmojiBoostLevel { get; private set; }

        public int MinProfileBackgroundCustomEmojiBoostLevel { get; private set; }

        public int MinEmojiStatusBoostLevel { get; private set; }

        protected Chat _chat;
        public Chat Chat
        {
            get => _chat;
            set => Set(ref _chat, value);
        }

        public ObservableCollection<ChatThemeViewModel> ChatThemes { get; }

        private ChatThemeViewModel _selectedChatTheme;
        public ChatThemeViewModel SelectedChatTheme
        {
            get => _selectedChatTheme;
            set
            {
                if (Set(ref _selectedChatTheme, value))
                {
                    RaisePropertyChanged(nameof(RequiredLevel));
                }
            }
        }

        private NameColor _selectedAccentColor;
        public NameColor SelectedAccentColor
        {
            get => _selectedAccentColor;
            set
            {
                if (Set(ref _selectedAccentColor, value))
                {
                    RaisePropertyChanged(nameof(RequiredLevel));
                }
            }
        }

        private long _selectedCustomEmojiId;
        public long SelectedCustomEmojiId
        {
            get => _selectedCustomEmojiId;
            set
            {
                if (Set(ref _selectedCustomEmojiId, value))
                {
                    RaisePropertyChanged(nameof(RequiredLevel));
                }
            }
        }

        private ProfileColor _selectedProfileAccentColor;
        public ProfileColor SelectedProfileAccentColor
        {
            get => _selectedProfileAccentColor;
            set
            {
                if (Set(ref _selectedProfileAccentColor, value))
                {
                    RaisePropertyChanged(nameof(RequiredLevel));
                }
            }
        }

        private long _selectedProfileCustomEmojiId;
        public long SelectedProfileCustomEmojiId
        {
            get => _selectedProfileCustomEmojiId;
            set
            {
                if (Set(ref _selectedProfileCustomEmojiId, value))
                {
                    RaisePropertyChanged(nameof(RequiredLevel));
                }
            }
        }

        private EmojiStatus _selectedEmojiStatus;
        public EmojiStatus SelectedEmojiStatus
        {
            get => _selectedEmojiStatus;
            set
            {
                if (Set(ref _selectedEmojiStatus, value))
                {
                    RaisePropertyChanged(nameof(RequiredLevel));
                }
            }
        }

        public int RequiredLevel => UpdateRequiredLevel(out _);

        private int UpdateRequiredLevel(out ChatBoostFeature feature)
        {
            feature = ChatBoostFeature.None;

            var level = 0;

            if (SelectedAccentColor != null && SelectedAccentColor.MinChatBoostLevel > level)
            {
                feature = ChatBoostFeature.AccentColor;
                level = SelectedAccentColor.MinChatBoostLevel;
            }

            if (SelectedCustomEmojiId != 0 && MinBackgroundCustomEmojiBoostLevel > level)
            {
                feature = ChatBoostFeature.BackgroundCustomEmoji;
                level = MinBackgroundCustomEmojiBoostLevel;
            }

            if (SelectedProfileAccentColor != null && SelectedProfileAccentColor.MinChatBoostLevel > level)
            {
                feature = ChatBoostFeature.ProfileAccentColor;
                level = SelectedProfileAccentColor.MinChatBoostLevel;
            }

            if (SelectedProfileCustomEmojiId != 0 && MinProfileBackgroundCustomEmojiBoostLevel > level)
            {
                feature = ChatBoostFeature.ProfileBackgroundCustomEmoji;
                level = MinProfileBackgroundCustomEmojiBoostLevel;
            }

            if (SelectedEmojiStatus != null && MinEmojiStatusBoostLevel > level)
            {
                feature = ChatBoostFeature.EmojiStatus;
                level = MinEmojiStatusBoostLevel;
            }

            if (SelectedChatTheme?.DarkSettings != null && MinChatThemeBackgroundBoostLevel > level)
            {
                feature = ChatBoostFeature.ChatTheme;
                level = MinChatThemeBackgroundBoostLevel;
            }

            return level;
        }

        private async void ConfirmClose()
        {
            var confirm = await ShowPopupAsync(Strings.ChannelColorUnsavedMessage, Strings.ChannelColorUnsaved, Strings.ChatThemeSaveDialogDiscard, Strings.ChatThemeSaveDialogApply, destructive: true);
            if (confirm == ContentDialogResult.Primary)
            {
                _confirmed = true;
                NavigationService.GoBack();
            }
            else if (confirm == ContentDialogResult.Secondary)
            {
                _confirmed = true;
                Commit();
            }
        }

        public async void Commit()
        {
            if (Chat is not Chat chat)
            {
                return;
            }

            var response = await ClientService.SendAsync(new GetChatBoostStatus(chat.Id));
            if (response is not ChatBoostStatus status)
            {
                return;
            }

            var required = UpdateRequiredLevel(out var feature);
            if (required > status.Level)
            {
                await ShowPopupAsync(new ChatBoostFeaturesPopup(status, _features, feature, required));
                return;
            }

            var changed = false;

            var accentColorId = SelectedAccentColor?.Id ?? -1;
            var profileAccentColorId = SelectedProfileAccentColor?.Id ?? -1;

            if (accentColorId != chat.AccentColorId || SelectedCustomEmojiId != chat.BackgroundCustomEmojiId)
            {
                changed = true;
                ClientService.Send(new SetChatAccentColor(chat.Id, accentColorId, SelectedCustomEmojiId));
            }

            if (profileAccentColorId != chat.ProfileAccentColorId || SelectedProfileCustomEmojiId != chat.ProfileBackgroundCustomEmojiId)
            {
                changed = true;
                ClientService.Send(new SetChatProfileAccentColor(chat.Id, profileAccentColorId, SelectedProfileCustomEmojiId));
            }

            if (SelectedEmojiStatus?.CustomEmojiId != chat.EmojiStatus?.CustomEmojiId || SelectedEmojiStatus?.ExpirationDate != chat.EmojiStatus?.ExpirationDate)
            {
                changed = true;
                ClientService.Send(new SetChatEmojiStatus(chat.Id, SelectedEmojiStatus));
            }

            if (SelectedChatTheme?.Name != chat.ThemeName)
            {
                changed = true;
                ClientService.Send(new SetChatTheme(chat.Id, SelectedChatTheme?.Name ?? string.Empty));
            }

            if (changed)
            {
                ToastPopup.Show(Strings.ChannelAppearanceUpdated, new LocalFileSource("ms-appx:///Assets/Toasts/Success.tgs"));
            }

            NavigationService.GoBack();
        }
    }
}
