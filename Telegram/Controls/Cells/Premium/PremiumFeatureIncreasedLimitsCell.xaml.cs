//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI;

namespace Telegram.Controls.Cells.Premium
{
    public sealed partial class PremiumFeatureIncreasedLimitsCell : UserControl, IPremiumFeatureCell
    {
        private readonly Color[] _gradient = new Color[]
        {
            Color.FromArgb(0xFF, 0x5B, 0xA0, 0xFE),
            Color.FromArgb(0xFF, 0x79, 0x8A, 0xFF),
            Color.FromArgb(0xFF, 0x93, 0x77, 0xFF),
            Color.FromArgb(0xFF, 0xAC, 0x64, 0xF2),
            Color.FromArgb(0xFF, 0xC4, 0x56, 0xAE),
            Color.FromArgb(0xFF, 0xCF, 0x57, 0x9A),
            Color.FromArgb(0xFF, 0xDB, 0x58, 0x87),
            Color.FromArgb(0xFF, 0xDA, 0x49, 0x6F),
            Color.FromArgb(0xFF, 0xE9, 0x5D, 0x44),
            Color.FromArgb(0xFF, 0xF2, 0x82, 0x2A)
        };

        //public PremiumFeatureIncreasedLimitsCell(IClientService clientService, PremiumPaymentOption option, IList<PremiumLimit> limits)
        //{
        //    InitializeComponent();

        //    Title = Strings.DoubledLimits;

        //    ScrollingHost.ItemsSource = limits;
        //    PurchaseCommand.Content = PromoPopup.GetPaymentString(clientService.IsPremium, option);

        //    clientService.Send(new ViewPremiumFeature(new PremiumFeatureIncreasedLimits()));
        //}

        public PremiumFeatureIncreasedLimitsCell()
        {
            InitializeComponent();
        }

        public void UpdateFeature(IClientService clientService, IList<PremiumLimit> limits)
        {
            ScrollingHost.ItemsSource = limits;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var limit = args.Item as PremiumLimit;
            var content = args.ItemContainer.ContentTemplateRoot as Grid;

            var titleValue = string.Empty;
            var subtitleValue = string.Empty;

            switch (limit.Type)
            {
                case PremiumLimitTypeBioLength:
                    titleValue = Strings.BioLimitTitle;
                    subtitleValue = Strings.BioLimitSubtitle;
                    break;
                case PremiumLimitTypeCaptionLength:
                    titleValue = Strings.CaptionsLimitTitle;
                    subtitleValue = Strings.CaptionsLimitSubtitle;
                    break;
                case PremiumLimitTypeChatFolderChosenChatCount:
                    titleValue = Strings.ChatPerFolderLimitTitle;
                    subtitleValue = Strings.ChatPerFolderLimitSubtitle;
                    break;
                case PremiumLimitTypeChatFolderCount:
                    titleValue = Strings.FoldersLimitTitle;
                    subtitleValue = Strings.FoldersLimitSubtitle;
                    break;
                case PremiumLimitTypeCreatedPublicChatCount:
                    titleValue = Strings.PublicLinksLimitTitle;
                    subtitleValue = Strings.PublicLinksLimitSubtitle;
                    break;
                case PremiumLimitTypeFavoriteStickerCount:
                    titleValue = Strings.FavoriteStickersLimitTitle;
                    subtitleValue = Strings.FavoriteStickersLimitSubtitle;
                    break;
                case PremiumLimitTypePinnedArchivedChatCount:
                    titleValue = "";
                    subtitleValue = "";
                    break;
                case PremiumLimitTypePinnedChatCount:
                    titleValue = Strings.PinChatsLimitTitle;
                    subtitleValue = Strings.PinChatsLimitSubtitle;
                    break;
                case PremiumLimitTypeSavedAnimationCount:
                    titleValue = Strings.SavedGifsLimitTitle;
                    subtitleValue = Strings.SavedGifsLimitSubtitle;
                    break;
                case PremiumLimitTypeSupergroupCount:
                    titleValue = Strings.GroupsAndChannelsLimitTitle;
                    subtitleValue = Strings.GroupsAndChannelsLimitSubtitle;
                    break;
                case PremiumLimitTypeConnectedAccounts:
                    titleValue = Strings.ConnectedAccountsLimitTitle;
                    subtitleValue = Strings.ConnectedAccountsLimitSubtitle;
                    break;
            }

            var title = content.FindName("Title") as TextBlock;
            var subtitle = content.FindName("Subtitle") as TextBlock;
            var prevLimit = content.FindName("PrevLimit") as TextBlock;
            var nextLimit = content.FindName("NextLimit") as TextBlock;
            var nextPanel = content.FindName("NextPanel") as Grid;

            var item = (double)args.ItemIndex;
            var total = sender.Items.Count - 1;
            var length = _gradient.Length - 1;

            var index = (int)(item / total * length);

            title.Text = titleValue;
            subtitle.Text = string.Format(subtitleValue, limit.PremiumValue);
            prevLimit.Text = limit.DefaultValue.ToString();
            nextLimit.Text = limit.PremiumValue.ToString();
            nextPanel.Background = new SolidColorBrush(_gradient[index]);

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
            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && sender is ScrollViewer scrollingHost)
            {
                var currentPoint = e.GetCurrentPoint(this);
                if (currentPoint.Properties.MouseWheelDelta > 0 && scrollingHost.VerticalOffset == 0)
                {
                    var parent = this.GetParent<FlipView>();
                    if (parent?.SelectedIndex > 0 && parent.SelectedItem is PremiumFeatureIncreasedLimits)
                    {
                        parent.SelectedIndex--;
                        StopAnimation();
                    }
                }
            }
        }
    }
}
