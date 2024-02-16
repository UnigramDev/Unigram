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

        public void UpdateCell(ChatBoostLevelFeatures features, int index)
        {
            Level.Text = string.Format(index == 0 ? Strings.BoostLevelUnlocks : Strings.BoostLevel, features.Level);

            UpdateFeature(features.StoryPerDayCount, StoriesIcon, StoriesText, Strings.R.BoostFeatureStoriesPerDay);
            UpdateFeature(features.CustomEmojiReactionCount, ReactionsIcon, ReactionsText, Strings.R.BoostFeatureCustomReaction);
            UpdateFeature(features.TitleColorCount, NameColorsIcon, NameColorsText, Strings.R.BoostFeatureNameColor);
            UpdateFeature(features.AccentColorCount, StylesIcon, StylesText, Strings.R.BoostFeatureReplyColor);
            UpdateFeature(features.CanSetBackgroundCustomEmoji, NameIconsIcon, NameIconsText, Strings.BoostFeatureReplyIcon);
            UpdateFeature(features.CanSetEmojiStatus, EmojiStatusIcon, EmojiStatusText, string.Format(Strings.BoostFeatureEmojiStatuses, "**1000+**"));
            UpdateFeature(features.ProfileAccentColorCount, ProfileColorsIcon, ProfileColorsText, Strings.R.BoostFeatureProfileColor);
            UpdateFeature(features.CanSetProfileBackgroundCustomEmoji, ProfileIconsIcon, ProfileIconsText, Strings.BoostFeatureProfileIcon);
            UpdateFeature(features.ChatThemeBackgroundCount, BackgroundsIcon, BackgroundsText, Strings.R.BoostFeatureBackground);
            UpdateFeature(features.CanSetCustomBackground, CustomBackgroundIcon, CustomBackgroundText, Strings.BoostFeatureCustomBackground);
            UpdateFeature(features.CustomEmojiReactionCount, ReactionsIcon, ReactionsText, Strings.R.BoostFeatureCustomReaction);
            UpdateFeature(features.CustomEmojiReactionCount, ReactionsIcon, ReactionsText, Strings.R.BoostFeatureCustomReaction);
            UpdateFeature(features.CustomEmojiReactionCount, ReactionsIcon, ReactionsText, Strings.R.BoostFeatureCustomReaction);
            UpdateFeature(features.CustomEmojiReactionCount, ReactionsIcon, ReactionsText, Strings.R.BoostFeatureCustomReaction);
        }

        private void UpdateFeature(int count, TextBlock icon, TextBlock text, string key)
        {
            if (count > 0)
            {
                TextBlockHelper.SetMarkdown(text, string.Format(Locale.Declension(key, count, false), $"**{count}**"));
            }
            else
            {
                icon.Visibility = Visibility.Collapsed;
                text.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateFeature(bool enabled, TextBlock icon, TextBlock text, string key)
        {
            if (enabled)
            {
                TextBlockHelper.SetMarkdown(text, key);
            }
            else
            {
                icon.Visibility = Visibility.Collapsed;
                text.Visibility = Visibility.Collapsed;
            }
        }
    }
}
