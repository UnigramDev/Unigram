//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading.Tasks;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Controls.Messages;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;
using Telegram.ViewModels.Settings;
using Telegram.ViewModels.Supergroups;
using Telegram.Views.Supergroups.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Supergroups
{
    public sealed partial class SupergroupProfileColorPage : HostedPage
    {
        public SupergroupProfileColorViewModel ViewModel => DataContext as SupergroupProfileColorViewModel;

        public SupergroupProfileColorPage()
        {
            InitializeComponent();
            Title = Strings.ChannelColorTitle2;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ProfileView.Initialize(ViewModel.ClientService, new MessageSenderChat(ViewModel.Chat.Id));

            if (ViewModel.Chat.EmojiStatus != null)
            {
                AnimatedStatus.Source = new CustomEmojiFileSource(ViewModel.ClientService, ViewModel.Chat.EmojiStatus.Type);
                EmojiStatus.Badge = string.Empty;
            }
            else
            {
                AnimatedStatus.Source = null;
                EmojiStatus.Badge = Strings.UserReplyIconOff;
            }

            if (ViewModel.Chat.Type is ChatTypeSupergroup { IsChannel: true })
            {
                FindName(nameof(NameView));
                NameView.Initialize(ViewModel.ClientService, new MessageSenderChat(ViewModel.Chat.Id));

                WallpaperRoot.Footer = Strings.ChannelWallpaper2Info;
                WallpaperLabel.Text = Strings.ChannelWallpaper;
                EmojiStatusRoot.Footer = Strings.ChannelEmojiStatusInfo;
                EmojiStatusLabel.Text = Strings.ChannelEmojiStatus;
            }
            else
            {
                FindName(nameof(WallpaperPreview));
                //FindName(nameof(EmojiPackRoot));
                EmojiPackRoot.Visibility = Visibility.Visible;

                if (ViewModel.ClientService.TryGetUser(ViewModel.ClientService.Options.MyId, out User user))
                {
                    Message1.Mockup(ViewModel.ClientService, Strings.FontSizePreviewLine1, user, Strings.FontSizePreviewReply, false, DateTime.Now.AddSeconds(-25));
                    Message2.Mockup(Strings.FontSizePreviewLine2, true, DateTime.Now);

                    BackgroundControl.Update(ViewModel.ClientService, null);
                }

                WallpaperRoot.Footer = Strings.GroupWallpaper2Info;
                WallpaperLabel.Text = Strings.GroupWallpaper;
                EmojiStatusRoot.Footer = Strings.GroupEmojiStatusInfo;
                EmojiStatusLabel.Text = Strings.GroupEmojiStatus;

                if (ViewModel.ClientService.TryGetSupergroupFull(ViewModel.Chat, out SupergroupFullInfo fullInfo))
                {
                    LoadStickerSet(fullInfo.CustomEmojiStickerSetId, EmojiPackAnimated);
                    LoadStickerSet(fullInfo.StickerSetId, StickerPackAnimated);

                    StickerPackRoot.Visibility = fullInfo.CanSetStickerSet
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
            }
        }

        private async void LoadStickerSet(long stickerSetId, AnimatedImage target)
        {
            if (stickerSetId != 0)
            {
                var response = await ViewModel.ClientService.SendAsync(new GetStickerSet(stickerSetId));
                if (response is StickerSet set)
                {
                    target.Source = DelayedFileSource.FromStickerSet(ViewModel.ClientService, set);
                    return;
                }
            }

            target.Source = null;
        }

        private void EmojiStatus_Click(object sender, RoutedEventArgs e)
        {
            var flyout = EmojiMenuFlyout.ShowAt(ViewModel.ClientService, EmojiDrawerMode.ChatEmojiStatus, AnimatedStatus, EmojiFlyoutAlignment.TopRight);
            flyout.EmojiSelected += Flyout_EmojiSelected;
        }

        private void Flyout_EmojiSelected(object sender, EmojiSelectedEventArgs e)
        {
            if (e.Type is ReactionTypeCustomEmoji customEmoji)
            {
                ViewModel.SelectedEmojiStatus = new EmojiStatus(new EmojiStatusTypeCustomEmoji(customEmoji.CustomEmojiId), 0);

                AnimatedStatus.Source = new CustomEmojiFileSource(ViewModel.ClientService, customEmoji.CustomEmojiId);
                EmojiStatus.Badge = string.Empty;
            }
            else
            {
                ViewModel.SelectedEmojiStatus = null;

                AnimatedStatus.Source = null;
                EmojiStatus.Badge = Strings.UserReplyIconOff;
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (List.SelectedItem is ChatThemeViewModel chatTheme)
            {
                BackgroundControl?.UpdateChat(ViewModel.ClientService, null, chatTheme.Type);
            }
        }

        private async void EmojiPack_Click(object sender, RoutedEventArgs e)
        {
            var tsc = new TaskCompletionSource<object>();
            var args = new SupergroupEditStickerSetArgs(ViewModel.Chat.Id, new StickerTypeCustomEmoji());

            var confirm = await ViewModel.NavigationService.ShowPopupAsync(new SupergroupEditStickerSetPopup(tsc), args);
            var set = await tsc.Task as StickerSetInfo;

            if (confirm == ContentDialogResult.Primary)
            {
                ViewModel.SelectedCustomEmojiStickerSet = set?.Id ?? 0;
                EmojiPackAnimated.Source = DelayedFileSource.FromStickerSetInfo(ViewModel.ClientService, set);
            }
        }

        private async void StickerPack_Click(object sender, RoutedEventArgs e)
        {
            var tsc = new TaskCompletionSource<object>();
            var args = new SupergroupEditStickerSetArgs(ViewModel.Chat.Id, new StickerTypeRegular());

            var confirm = await ViewModel.NavigationService.ShowPopupAsync(new SupergroupEditStickerSetPopup(tsc), args);
            var set = await tsc.Task as StickerSetInfo;

            if (confirm == ContentDialogResult.Primary)
            {
                ViewModel.SelectedStickerSet = set?.Id ?? 0;
                StickerPackAnimated.Source = DelayedFileSource.FromStickerSetInfo(ViewModel.ClientService, set);
            }
        }

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new GridViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.ItemContainer.ContentTemplateRoot is not ChatThemeCell content)
            {
                return;
            }

            if (args.InRecycleQueue)
            {
                content.Recycle();
                return;
            }
            else if (args.Item is ChatThemeViewModel theme)
            {
                content.Update(args.ItemContainer, theme);
                args.Handled = true;
            }
        }

        #endregion

        #region Binding

        private string ConvertRequiredLevel(int value, UIElement element)
        {
            if (value > 0)
            {
                element.Visibility = Visibility.Visible;
                return Icons.LockClosedFilled12 + Icons.Spacing + string.Format(Strings.BoostLevel, value);
            }
            else
            {
                element.Visibility = Visibility.Collapsed;
                return string.Empty;
            }
        }

        #endregion
    }
}
