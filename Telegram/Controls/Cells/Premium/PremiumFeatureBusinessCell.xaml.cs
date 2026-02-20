//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Premium.Popups;
using Windows.UI.Xaml;
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

            _viewChangedTimer = new DispatcherTimer();
            _viewChangedTimer.Interval = TimeSpan.FromMilliseconds(Constants.TypingTimeout);
            _viewChangedTimer.Tick += OnTick;
        }

        public void UpdateFeature(IClientService clientService, IList<BusinessFeature> features)
        {
            ScrollingHost.ItemsSource = features
                .Where(x => x is not BusinessFeatureChatFolderTags and not BusinessFeatureEmojiStatus and not BusinessFeatureUpgradedStories)
                .ToList();
        }

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

            title.Text = titleValue;
            subtitle.Text = subtitleValue;
            icon.Text = iconValue;
            icon.Foreground = new SolidColorBrush(ColorsHelper.CalculateColor(PromoPopup.Gradient, (float)args.ItemIndex / (sender.Items.Count - 1)));

            args.Handled = true;
        }

        public void PlayAnimation()
        {
            var scrollingHost = ScrollingHost.GetScrollViewer();
            if (scrollingHost != null)
            {
                _loading = false;
                scrollingHost?.AddHandler(PointerWheelChangedEvent, new PointerEventHandler(OnPointerWheelChangedEvent), true);
                scrollingHost.ViewChanged += OnViewChanged;
            }
            else if (!_loading)
            {
                _loading = true;
                ScrollingHost.Loaded += OnLoaded;
            }

            _viewChanged = false;
        }

        public void StopAnimation()
        {
            var scrollingHost = ScrollingHost.GetScrollViewer();
            scrollingHost?.RemoveHandler(PointerWheelChangedEvent, new PointerEventHandler(OnPointerWheelChangedEvent));
            scrollingHost.ViewChanged -= OnViewChanged;

            _loading = false;
            _viewChangedTimer.Stop();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_loading)
            {
                PlayAnimation();
            }
        }

        private bool _loading;

        private bool _viewChanged;
        private DispatcherTimer _viewChangedTimer;

        private void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            _viewChanged = true;
            _viewChangedTimer.Stop();
            _viewChangedTimer.Start();
        }

        private void OnTick(object sender, object e)
        {
            _viewChangedTimer.Stop();
            _viewChanged = false;
        }

        private void OnPointerWheelChangedEvent(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse && sender is ScrollViewer scrollingHost && !_viewChanged)
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
                else if (currentPoint.Properties.MouseWheelDelta < 0 && scrollingHost.VerticalOffset.AlmostEquals(scrollingHost.ScrollableHeight))
                {
                    var parent = this.GetParent<FlipView>();
                    if (parent?.SelectedIndex < parent.Items.Count - 1 && parent.SelectedItem is PremiumFeatureBusiness)
                    {
                        parent.SelectedIndex++;
                        StopAnimation();
                    }
                }
            }
        }
    }
}
