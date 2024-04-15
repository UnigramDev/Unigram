//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using System.Linq;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells.Premium
{
    public sealed partial class PremiumFeatureBusinessCell : UserControl, IPremiumFeatureCell
    {
        public PremiumFeatureBusinessCell()
        {
            InitializeComponent();
        }

        public void UpdateFeature(IClientService clientService, IList<BusinessFeature> features)
        {
            ScrollingHost.ItemsSource = features
                .Where(x => x is not BusinessFeatureChatFolderTags and not BusinessFeatureEmojiStatus and not BusinessFeatureUpgradedStories)
                .ToList();
        }

        private readonly Color[] _gradient = new Color[]
        {
            Color.FromArgb(0xFF, 0xef, 0x69, 0x22), //
            Color.FromArgb(0xFF, 0xe9, 0x5a, 0x2c),
            Color.FromArgb(0xFF, 0xe7, 0x4e, 0x33),
            Color.FromArgb(0xFF, 0xe5, 0x49, 0x37), //
            Color.FromArgb(0xFF, 0xe3, 0x43, 0x3c),
            Color.FromArgb(0xFF, 0xdb, 0x37, 0x4b),
            Color.FromArgb(0xFF, 0xcb, 0x3e, 0x6d), //
            Color.FromArgb(0xFF, 0xbc, 0x43, 0x95),
            Color.FromArgb(0xFF, 0xab, 0x4a, 0xc4),
            Color.FromArgb(0xFF, 0xa3, 0x4c, 0xd7), //
            Color.FromArgb(0xFF, 0x9b, 0x4f, 0xed),
            Color.FromArgb(0xFF, 0x89, 0x58, 0xff),
            Color.FromArgb(0xFF, 0x67, 0x6b, 0xff), //
            Color.FromArgb(0xFF, 0x61, 0x72, 0xff),
            Color.FromArgb(0xFF, 0x5b, 0x79, 0xff),
            Color.FromArgb(0xFF, 0x44, 0x92, 0xff),
            Color.FromArgb(0xFF, 0x42, 0x9b, 0xd5), //
            Color.FromArgb(0xFF, 0x41, 0xa6, 0xa5),
            Color.FromArgb(0xFF, 0x3e, 0xb2, 0x6d),
            Color.FromArgb(0xFF, 0x3d, 0xbd, 0x4a), //
        };

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var feature = args.Item;
            var content = args.ItemContainer.ContentTemplateRoot as Grid;

            var iconValue = string.Empty;
            var titleValue = string.Empty;
            var subtitleValue = string.Empty;

            switch (feature)
            {
                case BusinessFeatureGreetingMessage:
                    iconValue = Icons.HandWaveFilled;
                    titleValue = Strings.PremiumBusinessGreetingMessages;
                    subtitleValue = Strings.PremiumBusinessGreetingMessagesDescription;
                    break;
                case BusinessFeatureAwayMessage:
                    iconValue = Icons.ChatSnoozeFilled;
                    titleValue = Strings.PremiumBusinessAwayMessages;
                    subtitleValue = Strings.PremiumBusinessAwayMessagesDescription;
                    break;
                case BusinessFeatureQuickReplies:
                    iconValue = Icons.ArrowReplyFilled;
                    titleValue = Strings.PremiumBusinessQuickReplies;
                    subtitleValue = Strings.PremiumBusinessQuickRepliesDescription;
                    break;
                case BusinessFeatureOpeningHours:
                    iconValue = Icons.ClockFilled;
                    titleValue = Strings.PremiumBusinessOpeningHours;
                    subtitleValue = Strings.PremiumBusinessOpeningHoursDescription;
                    break;
                case BusinessFeatureLocation:
                    iconValue = Icons.LocationFilled;
                    titleValue = Strings.PremiumBusinessLocation;
                    subtitleValue = Strings.PremiumBusinessLocationDescription;
                    break;
                case BusinessFeatureBots:
                    iconValue = Icons.BotFilled;
                    titleValue = Strings.PremiumBusinessChatbots2;
                    subtitleValue = Strings.PremiumBusinessChatbotsDescription;
                    break;
                case BusinessFeatureStartPage:
                    iconValue = Icons.ChatInfoFilled;
                    titleValue = Strings.PremiumBusinessIntro;
                    subtitleValue = Strings.PremiumBusinessIntroDescription;
                    break;
                case BusinessFeatureAccountLinks:
                    iconValue = Icons.ChatLinkFilled;
                    titleValue = Strings.PremiumBusinessChatLinks;
                    subtitleValue = Strings.PremiumBusinessChatLinksDescription;
                    break;
                case BusinessFeatureChatFolderTags:
                    titleValue = Strings.PremiumPreviewFolderTags;
                    subtitleValue = Strings.PremiumPreviewFolderTagsDescription;
                    break;
                case BusinessFeatureEmojiStatus:
                    titleValue = Strings.PremiumPreviewBusinessEmojiStatus;
                    subtitleValue = Strings.PremiumPreviewBusinessEmojiStatusDescription;
                    break;
                case BusinessFeatureUpgradedStories:
                    titleValue = Strings.PremiumPreviewBusinessStories;
                    subtitleValue = Strings.PremiumPreviewBusinessStoriesDescription;
                    break;
            }

            var title = content.FindName("Title") as TextBlock;
            var subtitle = content.FindName("Subtitle") as TextBlock;
            var icon = content.FindName("Icon") as TextBlock;

            var item = (double)args.ItemIndex;
            var total = sender.Items.Count - 1;
            var length = _gradient.Length - 1;

            var index = (int)(item / total * length);

            title.Text = titleValue;
            subtitle.Text = subtitleValue;
            icon.Text = iconValue;
            icon.Foreground = new SolidColorBrush(_gradient[index]);

            args.Handled = true;
        }

        public void PlayAnimation()
        {
            var scrollingHost = ScrollingHost.GetScrollViewer();
            scrollingHost?.AddHandler(PointerWheelChangedEvent, new PointerEventHandler(OnPointerWheelChangedEvent), true);
        }

        public void StopAnimation()
        {
            var scrollingHost = ScrollingHost.GetScrollViewer();
            scrollingHost?.RemoveHandler(PointerWheelChangedEvent, new PointerEventHandler(OnPointerWheelChangedEvent));
        }

        private void OnPointerWheelChangedEvent(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse && sender is ScrollViewer scrollingHost)
            {
                var currentPoint = e.GetCurrentPoint(this);
                if (currentPoint.Properties.MouseWheelDelta > 0 && scrollingHost.VerticalOffset == 0)
                {
                    var parent = this.GetParent<FlipView>();
                    if (parent?.SelectedIndex > 0 && parent.SelectedItem is PremiumFeatureBusiness)
                    {
                        parent.SelectedIndex--;
                        StopAnimation();
                    }
                }
            }
        }
    }
}
