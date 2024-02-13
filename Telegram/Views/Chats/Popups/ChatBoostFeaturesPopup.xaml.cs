//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Td.Api;
using Telegram.ViewModels.Supergroups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Chats.Popups
{
    public sealed partial class ChatBoostFeaturesPopup : ContentPopup
    {
        private readonly bool _channel;
        private readonly ChatBoostStatus _status;

        public ChatBoostFeaturesPopup(bool channel, ChatBoostStatus status, ChatBoostFeatures features, ChatBoostFeature feature, int requiredLevel)
        {
            InitializeComponent();

            _channel = channel;
            _status = status;

            ScrollingHost.ItemsSource = features.Features
                .Where(x => x.Level > status.Level)
                .ToList();

            Link.Text = status.BoostUrl.Replace("https://", string.Empty);

            Title = status.AppliedSlotIds.Count > 0
                ? Strings.YouBoostedChannel
                : status.Level == 0
                ? Strings.BoostingEnableStoriesForChannel
                : Strings.HelpUpgradeChannel;

            Title = feature switch
            {
                ChatBoostFeature.AccentColor => Strings.BoostingEnableColor,
                ChatBoostFeature.ProfileAccentColor => Strings.BoostingEnableProfileColor,
                ChatBoostFeature.EmojiStatus => Strings.BoostingEnableEmojiStatus,
                ChatBoostFeature.ChatTheme => Strings.BoostingEnableWallpaper,
                ChatBoostFeature.CustomBackground => Strings.BoostingEnableWallpaper,
                ChatBoostFeature.BackgroundCustomEmoji => Strings.BoostingEnableLinkIcon,
                ChatBoostFeature.ProfileBackgroundCustomEmoji => Strings.BoostingEnableProfileIcon,
                _ => Strings.BoostingIncreaseLevel
            };

            var description = feature switch
            {
                ChatBoostFeature.AccentColor => Strings.ChannelNeedBoostsForColorDescription,
                ChatBoostFeature.ProfileAccentColor => Strings.ChannelNeedBoostsForProfileColorDescription,
                ChatBoostFeature.EmojiStatus => Strings.ChannelNeedBoostsForEmojiStatusDescription,
                ChatBoostFeature.ChatTheme => Strings.ChannelNeedBoostsForWallpaperDescription,
                ChatBoostFeature.CustomBackground => Strings.ChannelNeedBoostsForCustomWallpaperDescription,
                ChatBoostFeature.BackgroundCustomEmoji => Strings.ChannelNeedBoostsForReplyIconDescription,
                ChatBoostFeature.ProfileBackgroundCustomEmoji => Strings.ChannelNeedBoostsForProfileIconDescription,
                _ => Strings.BoostingIncreaseLevel
            };

            TextBlockHelper.SetMarkdown(Description, string.Format(description, requiredLevel));

            var justReached = status.AppliedSlotIds.Count > 0
                ? status.CurrentLevelBoostCount - status.BoostCount == 0
                : status.NextLevelBoostCount - status.BoostCount == 1;

            Progress.Minimum = status.CurrentLevelBoostCount;
            Progress.Maximum = status.NextLevelBoostCount;
            Progress.Value = status.BoostCount;

            if (justReached && status.AppliedSlotIds.Count > 0)
            {
                Progress.Minimum = 0;
                Progress.Maximum = status.BoostCount;
                Progress.Value = status.BoostCount;

                Progress.MinimumText = string.Format(Strings.BoostsLevel, status.Level - 1);
                Progress.MaximumText = string.Format(Strings.BoostsLevel, status.Level);
            }
            else
            {
                Progress.Minimum = status.CurrentLevelBoostCount;
                Progress.Maximum = status.NextLevelBoostCount;
                Progress.Value = status.BoostCount;

                Progress.MinimumText = string.Format(Strings.BoostsLevel, status.Level);
                Progress.MaximumText = string.Format(Strings.BoostsLevel, status.Level + 1);
            }
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ChatBoostFeaturesCell cell && args.Item is ChatBoostLevelFeatures features)
            {
                cell.UpdateCell(_channel, features, args.ItemIndex);
            }
        }

        private void CopyLink_Click(object sender, RoutedEventArgs e)
        {
            MessageHelper.CopyLink(_status.BoostUrl);
        }
    }
}
