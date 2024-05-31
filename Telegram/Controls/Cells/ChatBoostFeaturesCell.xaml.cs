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
            var i = 0;

            if (channel)
            {
                UpdateFeature(i++, features.CanDisableSponsoredMessages, DisableSponsoredIcon, DisableSponsoredText, Strings.BoostFeatureSwitchOffAds);
                UpdateFeature(i++, features.StoryPerDayCount, StoriesIcon, StoriesText, Strings.R.BoostFeatureStoriesPerDay);
                UpdateFeature(i++, features.CustomEmojiReactionCount, ReactionsIcon, ReactionsText, Strings.R.BoostFeatureCustomReaction);
                UpdateFeature(i++, features.TitleColorCount, NameColorsIcon, NameColorsText, Strings.R.BoostFeatureNameColor);
                UpdateFeature(i++, features.AccentColorCount, StylesIcon, StylesText, Strings.R.BoostFeatureReplyColor);
                UpdateFeature(i++, features.CanSetBackgroundCustomEmoji, NameIconsIcon, NameIconsText, Strings.BoostFeatureReplyIcon);
                UpdateFeature(i++, features.CanSetEmojiStatus, EmojiStatusIcon, EmojiStatusText, string.Format(Strings.BoostFeatureEmojiStatuses, "**1000+**"));
                UpdateFeature(i++, features.ProfileAccentColorCount, ProfileColorsIcon, ProfileColorsText, Strings.R.BoostFeatureProfileColor);
                UpdateFeature(i++, features.CanSetProfileBackgroundCustomEmoji, ProfileIconsIcon, ProfileIconsText, Strings.BoostFeatureProfileIcon);
                UpdateFeature(i++, features.ChatThemeBackgroundCount, BackgroundsIcon, BackgroundsText, Strings.R.BoostFeatureBackground);
                UpdateFeature(i++, features.CanSetCustomBackground, CustomBackgroundIcon, CustomBackgroundText, Strings.BoostFeatureCustomBackground);

                Collapse(VoiceToTextIcon, VoiceToTextText);
                Collapse(CustomEmojiPackIcon, CustomEmojiPackText);
            }
            else
            {
                UpdateFeature(i++, features.CanSetCustomBackground, CustomBackgroundIcon, CustomBackgroundText, Strings.BoostFeatureCustomBackgroundGroup);
                UpdateFeature(i++, features.ChatThemeBackgroundCount, BackgroundsIcon, BackgroundsText, Strings.R.BoostFeatureBackgroundGroup);
                UpdateFeature(i++, features.CanSetEmojiStatus, EmojiStatusIcon, EmojiStatusText, string.Format(Strings.BoostFeatureEmojiStatuses, "**1000+**"));
                UpdateFeature(i++, features.CanRecognizeSpeech, VoiceToTextIcon, VoiceToTextText, Strings.BoostFeatureVoiceToTextConversion);
                UpdateFeature(i++, features.CanSetProfileBackgroundCustomEmoji, ProfileIconsIcon, ProfileIconsText, Strings.BoostFeatureProfileIconGroup);
                UpdateFeature(i++, features.CanSetCustomEmojiStickerSet, CustomEmojiPackIcon, CustomEmojiPackText, Strings.BoostFeatureCustomEmojiPack);
                UpdateFeature(i++, features.ProfileAccentColorCount, ProfileColorsIcon, ProfileColorsText, Strings.R.BoostFeatureProfileColorGroup);
                UpdateFeature(i++, features.StoryPerDayCount, StoriesIcon, StoriesText, Strings.R.BoostFeatureStoriesPerDay);

                Collapse(DisableSponsoredIcon, DisableSponsoredText);
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

                icon.Visibility = Visibility.Visible;
                text.Visibility = Visibility.Visible;
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

                icon.Visibility = Visibility.Visible;
                text.Visibility = Visibility.Visible;
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
