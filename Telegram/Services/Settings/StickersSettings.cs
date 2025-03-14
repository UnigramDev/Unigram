//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Windows.Storage;

namespace Telegram.Services.Settings
{
    public enum StickersSuggestionMode
    {
        All,
        Installed,
        None
    }

    public enum StickersTab
    {
        Emoji,
        Animations,
        Stickers,
        None
    }

    public partial class StickersSettings : SettingsServiceBase
    {
        public StickersSettings(ApplicationDataContainer container)
            : base(container)
        {

        }

        private int? _suggestionMode;
        public StickersSuggestionMode SuggestionMode
        {
            get => (StickersSuggestionMode)(_suggestionMode ??= GetValueOrDefault("SuggestionMode", 0));
            set => AddOrUpdateValue(ref _suggestionMode, "SuggestionMode", (int)value);
        }

        public bool? _suggestCustomEmoji;
        public bool SuggestCustomEmoji
        {
            get => _suggestCustomEmoji ??= GetValueOrDefault("SuggestCustomEmoji", true);
            set => AddOrUpdateValue(ref _suggestCustomEmoji, "SuggestCustomEmoji", value);
        }

        private int? _selectedTab;
        public StickersTab SelectedTab
        {
            get => (StickersTab)(_selectedTab ??= GetValueOrDefault("SelectedTab", 2));
            set => AddOrUpdateValue(ref _selectedTab, "SelectedTab", (int)value);
        }

        private int? _skinTone;
        public EmojiSkinTone SkinTone
        {
            get => (EmojiSkinTone)(_skinTone ??= GetValueOrDefault("SkinTone", 0));
            set => AddOrUpdateValue(ref _skinTone, "SkinTone", (int)value);
        }

        private bool? _dynamicPackOrder;
        public bool DynamicPackOrder
        {
            get => _dynamicPackOrder ??= GetValueOrDefault("DynamicPackOrder", true);
            set => AddOrUpdateValue(ref _dynamicPackOrder, "DynamicPackOrder", value);
        }

        private bool? _isSidebarEnabled;
        public bool IsSidebarEnabled
        {
            get => _isSidebarEnabled ??= GetValueOrDefault("IsSidebarEnabled", true);
            set => AddOrUpdateValue(ref _isSidebarEnabled, "IsSidebarEnabled", value);
        }

        private bool? _isPointerOverEnabled;
        public bool IsPointerOverEnabled
        {
            get => _isPointerOverEnabled ??= GetValueOrDefault("IsPointerOverEnabled", true);
            set => AddOrUpdateValue(ref _isPointerOverEnabled, "IsPointerOverEnabled", value);
        }
    }
}
