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
        CustomEmojiStickerSet,
        ChatTheme,
        CustomBackground,
        DisableSponsoredMessages
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
            var themes = ClientService.ChatThemes.Select(x => new ChatThemeViewModel(ClientService, x, true));

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

            var themeName = chat.Background?.Background.Type is BackgroundTypeChatTheme typeChatTheme
                ? typeChatTheme.ThemeName
                : chat.ThemeName;

            SelectedChatTheme = string.IsNullOrEmpty(themeName) ? ChatThemes[0] : ChatThemes.FirstOrDefault(x => x.Name == themeName);

            SelectedAccentColor = ClientService.GetAccentColor(chat.AccentColorId);
            SelectedCustomEmojiId = chat.BackgroundCustomEmojiId;
            SelectedProfileAccentColor = ClientService.GetProfileColor(chat.ProfileAccentColorId);
            SelectedProfileCustomEmojiId = chat.ProfileBackgroundCustomEmojiId;
            SelectedEmojiStatus = chat.EmojiStatus;

            var response1 = await ClientService.SendAsync(new GetChatBoostFeatures(chat.Type is ChatTypeSupergroup { IsChannel: true }));
            var response2 = await ClientService.SendAsync(new GetChatBoostStatus(chat.Id));

            if (response1 is ChatBoostFeatures features && response2 is ChatBoostStatus status)
            {
                _features = features;
                _status = status;

                int MinLevelOrZero(int level)
                {
                    return level < status.Level ? 0 : level;
                }

                MinChatThemeBackgroundBoostLevel = MinLevelOrZero(features.MinChatThemeBackgroundBoostLevel);
                MinCustomBackgroundBoostLevel = MinLevelOrZero(features.MinCustomBackgroundBoostLevel);
                MinBackgroundCustomEmojiBoostLevel = MinLevelOrZero(features.MinBackgroundCustomEmojiBoostLevel);
                MinProfileBackgroundCustomEmojiBoostLevel = MinLevelOrZero(features.MinProfileBackgroundCustomEmojiBoostLevel);
                MinEmojiStatusBoostLevel = MinLevelOrZero(features.MinEmojiStatusBoostLevel);
                MinCustomEmojiStickerSetBoostLevel = MinLevelOrZero(features.MinCustomEmojiStickerSetBoostLevel);
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

            var prevChatTheme = chat.Background?.Background.Type is BackgroundTypeChatTheme typeChatTheme
                ? typeChatTheme.ThemeName
                : chat.ThemeName;

            var nextChatTheme = SelectedChatTheme?.LightSettings != null
                ? SelectedChatTheme.Name
                : string.Empty;

            if (prevChatTheme != nextChatTheme)
            {
                changed = true;
            }

            if (changed)
            {
                ConfirmClose();
                args.Cancel = true;
            }
        }

        private int _minChatThemeBackgroundBoostLevel;
        public int MinChatThemeBackgroundBoostLevel
        {
            get => _minChatThemeBackgroundBoostLevel;
            set => Set(ref _minChatThemeBackgroundBoostLevel, value);
        }

        private int _minCustomBackgroundBoostLevel;
        public int MinCustomBackgroundBoostLevel
        {
            get => _minCustomBackgroundBoostLevel;
            set => Set(ref _minCustomBackgroundBoostLevel, value);
        }

        private int _minBackgroundCustomEmojiBoostLevel;
        public int MinBackgroundCustomEmojiBoostLevel
        {
            get => _minBackgroundCustomEmojiBoostLevel;
            set => Set(ref _minBackgroundCustomEmojiBoostLevel, value);
        }

        private int _minProfileBackgroundCustomEmojiBoostLevel;
        public int MinProfileBackgroundCustomEmojiBoostLevel
        {
            get => _minProfileBackgroundCustomEmojiBoostLevel;
            set => Set(ref _minProfileBackgroundCustomEmojiBoostLevel, value);
        }

        private int _minEmojiStatusBoostLevel;
        public int MinEmojiStatusBoostLevel
        {
            get => _minEmojiStatusBoostLevel;
            set => Set(ref _minEmojiStatusBoostLevel, value);
        }

        private int _minCustomEmojiStickerSetBoostLevel;
        public int MinCustomEmojiStickerSetBoostLevel
        {
            get => _minCustomEmojiStickerSetBoostLevel;
            set => Set(ref _minCustomEmojiStickerSetBoostLevel, value);
        }

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

        private long _selectedCustomEmojiStickerSet;
        public long SelectedCustomEmojiStickerSet
        {
            get => _selectedCustomEmojiStickerSet;
            set
            {
                if (Set(ref _selectedCustomEmojiStickerSet, value))
                {
                    RaisePropertyChanged(nameof(RequiredLevel));
                }
            }
        }

        private long _selectedStickerSet;
        public long SelectedStickerSet
        {
            get => _selectedStickerSet;
            set => Set(ref _selectedStickerSet, value);
        }

        public int RequiredLevel => UpdateRequiredLevel(out _);

        private int UpdateRequiredLevel(out ChatBoostFeature feature)
        {
            feature = ChatBoostFeature.None;

            var level = 0;

            if (Chat is not Chat chat)
            {
                return level;
            }

            var accentColorId = SelectedAccentColor?.Id ?? -1;
            var profileAccentColorId = SelectedProfileAccentColor?.Id ?? -1;

            if (accentColorId != -1 && accentColorId != chat.AccentColorId && SelectedAccentColor.MinChannelChatBoostLevel > level)
            {
                feature = ChatBoostFeature.AccentColor;
                level = SelectedAccentColor.MinChannelChatBoostLevel;
            }

            if (SelectedCustomEmojiId != 0 && SelectedCustomEmojiId != chat.BackgroundCustomEmojiId && MinBackgroundCustomEmojiBoostLevel > level)
            {
                feature = ChatBoostFeature.BackgroundCustomEmoji;
                level = MinBackgroundCustomEmojiBoostLevel;
            }

            if (profileAccentColorId != -1 && profileAccentColorId != chat.ProfileAccentColorId && SelectedProfileAccentColor.MinChannelChatBoostLevel > level)
            {
                feature = ChatBoostFeature.ProfileAccentColor;
                level = SelectedProfileAccentColor.MinChannelChatBoostLevel;
            }

            if (SelectedProfileCustomEmojiId != 0 && SelectedProfileCustomEmojiId != chat.ProfileBackgroundCustomEmojiId && MinProfileBackgroundCustomEmojiBoostLevel > level)
            {
                feature = ChatBoostFeature.ProfileBackgroundCustomEmoji;
                level = MinProfileBackgroundCustomEmojiBoostLevel;
            }

            if (SelectedEmojiStatus != null && SelectedEmojiStatus?.CustomEmojiId != chat.EmojiStatus?.CustomEmojiId && MinEmojiStatusBoostLevel > level)
            {
                feature = ChatBoostFeature.EmojiStatus;
                level = MinEmojiStatusBoostLevel;
            }

            if (ClientService.TryGetSupergroupFull(chat, out SupergroupFullInfo fullInfo))
            {
                if (SelectedCustomEmojiStickerSet != 0 && SelectedCustomEmojiStickerSet != fullInfo.CustomEmojiStickerSetId && MinCustomEmojiStickerSetBoostLevel > level)
                {
                    feature = ChatBoostFeature.CustomEmojiStickerSet;
                    level = MinCustomEmojiStickerSetBoostLevel;
                }
            }

            var prevChatTheme = chat.Background?.Background.Type is BackgroundTypeChatTheme typeChatTheme
                ? typeChatTheme.ThemeName
                : chat.ThemeName;

            var nextChatTheme = SelectedChatTheme?.LightSettings != null
                ? SelectedChatTheme.Name
                : string.Empty;

            if (nextChatTheme.Length > 0 && prevChatTheme != nextChatTheme && MinChatThemeBackgroundBoostLevel > level)
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
            if (response is not ChatBoostStatus status || !ClientService.TryGetSupergroupFull(chat, out SupergroupFullInfo fullInfo))
            {
                return;
            }

            var changed = false;

            if (SelectedStickerSet != fullInfo.StickerSetId && fullInfo.CanSetStickerSet)
            {
                changed = true;
                ClientService.Send(new SetSupergroupStickerSet(chat.Id, SelectedStickerSet));
            }

            var required = UpdateRequiredLevel(out var feature);
            if (required > status.Level)
            {
                await ShowPopupAsync(new ChatBoostFeaturesPopup(ClientService, NavigationService, chat, status, null, _features, feature, required));
                return;
            }

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

            if (SelectedCustomEmojiStickerSet != fullInfo.CustomEmojiStickerSetId)
            {
                changed = true;
                ClientService.Send(new SetSupergroupCustomEmojiStickerSet(chat.Id, SelectedCustomEmojiStickerSet));
            }

            var prevChatTheme = chat.Background?.Background.Type is BackgroundTypeChatTheme typeChatTheme
                ? typeChatTheme.ThemeName
                : chat.ThemeName;

            var nextChatTheme = SelectedChatTheme?.LightSettings != null
                ? SelectedChatTheme.Name
                : string.Empty;

            if (prevChatTheme != nextChatTheme)
            {
                changed = true;
                ClientService.Send(new SetChatTheme(chat.Id, nextChatTheme));
            }

            if (changed)
            {
                ToastPopup.Show(Strings.ChannelAppearanceUpdated, new LocalFileSource("ms-appx:///Assets/Toasts/Success.tgs"));
            }

            _confirmed = true;
            NavigationService.GoBack();
        }
    }
}
