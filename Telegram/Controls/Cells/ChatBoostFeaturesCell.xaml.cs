//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Cells
{
    public sealed partial class ChatBoostFeaturesCell : UserControl
    {
        public ChatBoostFeaturesCell()
        {
            this.InitializeComponent();
        }

        public void UpdateCell(bool channel, ChatBoostLevelFeatures features, int index)
        {
            Level.Text = string.Format(index == 0 ? Strings.BoostLevelUnlocks : Strings.BoostLevel, features.Level);

            if (channel)
            {
                UpdateFeature(0, features.StoryPerDayCount, StoriesIcon, StoriesText, Strings.R.BoostFeatureStoriesPerDay);
                UpdateFeature(1, features.CustomEmojiReactionCount, ReactionsIcon, ReactionsText, Strings.R.BoostFeatureCustomReaction);
                UpdateFeature(2, features.TitleColorCount, NameColorsIcon, NameColorsText, Strings.R.BoostFeatureNameColor);
                UpdateFeature(3, features.AccentColorCount, StylesIcon, StylesText, Strings.R.BoostFeatureReplyColor);
                UpdateFeature(4, features.CanSetBackgroundCustomEmoji, NameIconsIcon, NameIconsText, Strings.BoostFeatureReplyIcon);
                UpdateFeature(5, features.CanSetEmojiStatus, EmojiStatusIcon, EmojiStatusText, string.Format(Strings.BoostFeatureEmojiStatuses, "**1000+**"));
                UpdateFeature(6, features.ProfileAccentColorCount, ProfileColorsIcon, ProfileColorsText, Strings.R.BoostFeatureProfileColor);
                UpdateFeature(7, features.CanSetProfileBackgroundCustomEmoji, ProfileIconsIcon, ProfileIconsText, Strings.BoostFeatureProfileIcon);
                UpdateFeature(8, features.ChatThemeBackgroundCount, BackgroundsIcon, BackgroundsText, Strings.R.BoostFeatureBackground);
                UpdateFeature(9, features.CanSetCustomBackground, CustomBackgroundIcon, CustomBackgroundText, Strings.BoostFeatureCustomBackground);

                Collapse(VoiceToTextIcon, VoiceToTextText);
                Collapse(CustomEmojiPackIcon, CustomEmojiPackText);
            }
            else
            {
                UpdateFeature(0, features.CanSetCustomBackground, CustomBackgroundIcon, CustomBackgroundText, Strings.BoostFeatureCustomBackgroundGroup);
                UpdateFeature(1, features.ChatThemeBackgroundCount, BackgroundsIcon, BackgroundsText, Strings.R.BoostFeatureBackgroundGroup);
                UpdateFeature(2, features.CanSetEmojiStatus, EmojiStatusIcon, EmojiStatusText, string.Format(Strings.BoostFeatureEmojiStatuses, "**1000+**"));
                UpdateFeature(3, features.CanRecognizeSpeech, VoiceToTextIcon, VoiceToTextText, Strings.BoostFeatureVoiceToTextConversion);
                UpdateFeature(4, features.CanSetProfileBackgroundCustomEmoji, ProfileIconsIcon, ProfileIconsText, Strings.BoostFeatureProfileIconGroup);
                UpdateFeature(5, features.CanSetCustomEmojiStickerSet, CustomEmojiPackIcon, CustomEmojiPackText, Strings.BoostFeatureCustomEmojiPack);
                UpdateFeature(6, features.ProfileAccentColorCount, ProfileColorsIcon, ProfileColorsText, Strings.R.BoostFeatureProfileColorGroup);
                UpdateFeature(7, features.StoryPerDayCount, StoriesIcon, StoriesText, Strings.R.BoostFeatureStoriesPerDay);

                Collapse(ReactionsIcon, ReactionsText);
                Collapse(NameColorsIcon, NameColorsText);
                Collapse(StylesIcon, StylesText);
                Collapse(NameIconsIcon, NameIconsText);
            }
        }

        private void UpdateFeature(int index, int count, TextBlock icon, TextBlock text, string key)
        {
            if (count > 0)
            {
                TextBlockHelper.SetMarkdown(text, string.Format(Locale.Declension(key, count, false), $"**{count}**"));

                Grid.SetRow(icon, index + 1);
                Grid.SetRow(text, index + 1);
            }
            else
            {
                icon.Visibility = Visibility.Collapsed;
                text.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateFeature(int index, bool enabled, TextBlock icon, TextBlock text, string key)
        {
            if (enabled)
            {
                TextBlockHelper.SetMarkdown(text, key);

                Grid.SetRow(icon, index + 1);
                Grid.SetRow(text, index + 1);
            }
            else
            {
                icon.Visibility = Visibility.Collapsed;
                text.Visibility = Visibility.Collapsed;
            }
        }

        private void Collapse(TextBlock icon, TextBlock text)
        {
            icon.Visibility = Visibility.Collapsed;
            text.Visibility = Visibility.Collapsed;
        }
    }
}
