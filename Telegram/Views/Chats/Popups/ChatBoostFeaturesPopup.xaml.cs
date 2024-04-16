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
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Updates;
using Telegram.Td.Api;
using Telegram.ViewModels.Supergroups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Chats.Popups
{
    public sealed partial class ChatBoostFeaturesPopup : ContentPopup
    {
        private readonly bool _channel;
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;
        private readonly Chat _chat;
        private readonly ChatBoostStatus _status;

        private ChatBoostSlots _slots;

        public ChatBoostFeaturesPopup(IClientService clientService, INavigationService navigationService, Chat chat, ChatBoostStatus status, ChatBoostSlots slots, ChatBoostFeatures features, ChatBoostFeature feature, int requiredLevel)
        {
            InitializeComponent();

            if (!clientService.TryGetSupergroup(chat, out var supergroup))
            {
                return;
            }

            _clientService = clientService;
            _navigationService = navigationService;

            _channel = chat.Type is ChatTypeSupergroup { IsChannel: true };
            _chat = chat;
            _status = status;
            _slots = slots;

            ScrollingHost.ItemsSource = features.Features
                .Where(x => x.Level > status.Level)
                .ToList();

            Link.Text = status.BoostUrl.Replace("https://", string.Empty);

            Title = status.AppliedSlotIds.Count > 0
                ? Strings.YouBoostedChannel
                : status.Level == 0
                ? Strings.BoostingEnableStoriesForChannel
                : Strings.HelpUpgradeChannel;

            if (status.Level == _clientService.Options.ChatBoostLevelMax)
            {
                Title = Strings.BoostsMaxLevelReached;
                TextBlockHelper.SetMarkdown(Description, string.Format(Strings.BoostsMaxLevelReachedDescription, chat.Title, string.Format(Strings.BoostsLevel, status.Level)));
            }
            else
            {
                Title = feature switch
                {
                    ChatBoostFeature.AccentColor => Strings.BoostingEnableColor,
                    ChatBoostFeature.ProfileAccentColor => Strings.BoostingEnableProfileColor,
                    ChatBoostFeature.EmojiStatus => Strings.BoostingEnableEmojiStatus,
                    ChatBoostFeature.ChatTheme => Strings.BoostingEnableWallpaper,
                    ChatBoostFeature.CustomBackground => Strings.BoostingEnableWallpaper,
                    ChatBoostFeature.BackgroundCustomEmoji => Strings.BoostingEnableLinkIcon,
                    ChatBoostFeature.ProfileBackgroundCustomEmoji => Strings.BoostingEnableProfileIcon,
                    _ => _channel ? Strings.BoostChannel : Strings.BoostGroup
                };

                var description = feature switch
                {
                    ChatBoostFeature.AccentColor => _channel ? Strings.ChannelNeedBoostsForColorDescription : Strings.GroupNeedBoostsForColorDescription,
                    ChatBoostFeature.ProfileAccentColor => _channel ? Strings.ChannelNeedBoostsForProfileColorDescription : Strings.GroupNeedBoostsForProfileColorDescription,
                    ChatBoostFeature.EmojiStatus => _channel ? Strings.ChannelNeedBoostsForEmojiStatusDescription : Strings.GroupNeedBoostsForEmojiStatusDescription,
                    ChatBoostFeature.ChatTheme => _channel ? Strings.ChannelNeedBoostsForWallpaperDescription : Strings.GroupNeedBoostsForWallpaperDescription,
                    ChatBoostFeature.CustomBackground => _channel ? Strings.ChannelNeedBoostsForCustomWallpaperDescription : Strings.GroupNeedBoostsForCustomWallpaperDescription,
                    ChatBoostFeature.BackgroundCustomEmoji => _channel ? Strings.ChannelNeedBoostsForReplyIconDescription : Strings.GroupNeedBoostsForReplyIconDescription,
                    ChatBoostFeature.ProfileBackgroundCustomEmoji => _channel ? Strings.ChannelNeedBoostsForProfileIconDescription : Strings.GroupNeedBoostsForProfileIconDescription,
                    ChatBoostFeature.DisableSponsoredMessages => _channel ? Strings.ChannelNeedBoostsForSwitchOffAdsDescription : Strings.ChannelNeedBoostsForSwitchOffAdsDescription,
                    _ => _channel ? Strings.ChannelNeedBoostsDescriptionForNewFeatures : Strings.GroupNeedBoostsDescriptionForNewFeatures
                };

                if (feature == ChatBoostFeature.None)
                {
                    description = string.Format(description, chat.Title, status.NextLevelBoostCount - status.BoostCount);

                    if (supergroup.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator && requiredLevel != 0)
                    {
                        description = string.Format("{0} {1}", description, Strings.BoostingPremiumUserCanBoostGroupWithLink);
                    }
                }
                else
                {
                    description = string.Format(description, requiredLevel);
                }

                TextBlockHelper.SetMarkdown(Description, description);
            }

            var justReached = status.AppliedSlotIds.Count > 0
                ? status.CurrentLevelBoostCount - status.BoostCount == 0
                : status.NextLevelBoostCount - status.BoostCount == 1;

            if (status.Level == _clientService.Options.ChatBoostLevelMax || (justReached && status.AppliedSlotIds.Count > 0))
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

            if (supergroup.Status is not ChatMemberStatusCreator and not ChatMemberStatusAdministrator || requiredLevel == 0)
            {
                CopyRoot.Visibility = Visibility.Collapsed;
                Description.Padding = new Thickness(0, 24, 0, 24);

                ScrollingHost.Padding = new Thickness(24, 0, 24, 24 + 32);

                PurchasePanel.Visibility = Visibility.Visible;
                PurchaseCommand.Content = _channel
                    ? Strings.BoostChannel
                    : Strings.BoostGroup;
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

        private void PurchaseShadow_Loaded(object sender, RoutedEventArgs e)
        {
            DropShadowEx.Attach(PurchaseShadow);
        }

        private async void Purchase_Click(object sender, RoutedEventArgs e)
        {
            var already = _slots.Slots
                .Where(x => x.CurrentlyBoostedChatId == _chat.Id)
                .Select(x => x.SlotId)
                .ToList();

            var available = _slots.Slots
                .Where(x => x.CurrentlyBoostedChatId == 0)
                .Select(x => x.SlotId)
                .ToList();

            if (_clientService.IsPremium)
            {
                if (available.Count > 0)
                {
                    ScrollingHost.ScrollToTop();

                    var response = await _clientService.SendAsync(new BoostChat(_chat.Id, new[] { available[0] }));
                    if (response is ChatBoostSlots slots)
                    {
                        _slots = slots;
                        Progress.Value += 1;

                        var aggregator = TypeResolver.Current.Resolve<IEventAggregator>(_clientService.SessionId);
                        aggregator.Publish(new UpdateConfetti());
                    }
                }
                else if (already.Count < _slots.Slots.Count)
                {
                    // TODO: reassign boost slots
                    Hide();

                    await new ChatBoostReassignPopup(_clientService, _chat, _slots).ShowQueuedAsync();
                }
                else
                {
                    var message = _clientService.Options.PremiumGiftBoostCount > 0
                        ? Locale.Declension(Strings.R.BoostingGetMoreBoostByGiftingCount, _clientService.Options.PremiumGiftBoostCount, _chat.Title)
                        : string.Format(Strings.BoostingGetMoreBoostByGifting, _chat.Title);

                    var confirm = await MessagePopup.ShowAsync(target: null, message, Strings.BoostingMoreBoostsNeeded, Strings.GiftPremium, Strings.Close);
                    if (confirm == ContentDialogResult.Primary)
                    {
                        Hide();
                    }
                }
            }
            else
            {
                var confirm = await MessagePopup.ShowAsync(target: null, _channel ? Strings.PremiumNeededForBoosting : Strings.PremiumNeededForBoostingGroup, Strings.PremiumNeeded, Strings.CheckPhoneNumberYes, Strings.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    Hide();

                    _navigationService.ShowPromo(new PremiumSourceFeature(new PremiumFeatureChatBoost()));
                }
            }
        }
    }
}
