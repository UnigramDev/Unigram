//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Streams;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;

namespace Telegram.Controls.Messages.Service
{
    // One template for four contents (gift, gifted premium, gifted stars, premium gift
    // code): everything a branch turns on has to be turned off by the others, or the
    // recycled container inherits it.
    public sealed partial class MessageGiftContent : MessageService
    {
        public MessageGiftContent()
        {
            InitializeComponent();
        }

        protected override void UpdateContent(MessageViewModel message)
        {
            if (message.Content is MessageGift gift)
            {
                UpdateGift(message, gift);
            }
            else if (message.Content is MessagePremiumGiftCode premiumGiftCode)
            {
                UpdatePremiumGiftCode(message, premiumGiftCode);
            }
            else if (message.Content is MessageGiftedPremium giftedPremium)
            {
                UpdateGiftedPremium(message, giftedPremium);
            }
            else if (message.Content is MessageGiftedStars giftedStars)
            {
                UpdateGiftedStars(message, giftedStars);
            }
        }

        private void UpdateGift(MessageViewModel message, MessageGift gift)
        {
            var user = message.ClientService.GetTitle(gift.SenderId, true);
            var self = message.ClientService.GetUser(message.ClientService.Options.MyId);

            if (user == null || self == null)
            {
                return;
            }

            if (message.IsOutgoing)
            {
                Title.Text = gift.IsPrivate
                    ? string.Format(Strings.Gift2ActionTitleInAnonymous, user)
                    : string.Format(Strings.Gift2ActionTitle, self.FullName(true));

                if (gift.Text.Text.Length > 0)
                {
                    Subtitle.SetText(message.ClientService, gift.Text);
                }
                else if (gift.PrepaidUpgradeStarCount > 0)
                {
                    Subtitle.SetText(message.ClientService, ClientEx.ParseMarkdown(string.Format(Strings.Gift2ActionUpgradeOut, user)));
                }
                else if (gift.SellStarCount > 0)
                {
                    Subtitle.SetText(message.ClientService, ClientEx.ParseMarkdown(Locale.Declension(Strings.R.Gift2ActionOutInfo, gift.SellStarCount, user)));
                }
                else
                {
                    Subtitle.SetText(message.ClientService, ClientEx.ParseMarkdown(string.Format(Strings.Gift2Info2OutExpired, user)));
                }

                View.Visibility = Visibility.Visible;
                ViewLabel.Text = Strings.ActionGiftPremiumView;
            }
            else
            {
                Title.Text = gift.IsPrivate
                    ? Strings.Gift2ActionTitleAnonymous
                    : string.Format(Strings.Gift2ActionTitle, user);

                if (gift.Text.Text.Length > 0)
                {
                    Subtitle.SetText(message.ClientService, gift.Text);
                }
                else if (gift.PrepaidUpgradeStarCount > 0 && !gift.WasUpgraded)
                {
                    Subtitle.SetText(message.ClientService, ClientEx.ParseMarkdown(Strings.Gift2ActionUpgrade));
                }
                else if (gift.IsSaved)
                {
                    Subtitle.SetText(message.ClientService, ClientEx.ParseMarkdown(Strings.Gift2ActionSavedInfo));
                }
                else
                {
                    Subtitle.SetText(message.ClientService, ClientEx.ParseMarkdown(gift.WasConverted
                        ? Locale.Declension(Strings.R.Gift2ActionConvertedInfo, gift.SellStarCount)
                        : Locale.Declension(Strings.R.Gift2ActionInfo, gift.SellStarCount)));
                }

                View.Visibility = Visibility.Visible;
                ViewLabel.Text = gift.PrepaidUpgradeStarCount > 0 && !gift.WasUpgraded
                    ? Strings.Gift2Unpack
                    : Strings.ActionGiftPremiumView;
            }

            Animation.LoopCount = 0;
            Animation.Source = new DelayedFileSource(message.ClientService, gift.Gift.Sticker);
            Animation.Margin = new Thickness(0, 0, 0, 8);

            if (message.ClientService.TryGetChat(gift.Gift.PublisherChatId, out Chat publisherChat)
                && message.ClientService.TryGetSupergroup(publisherChat, out Supergroup publisher)
                && publisher.HasActiveUsername(out string username))
            {
                Publisher.Visibility = Visibility.Visible;
                TextBlockHelper.SetMarkdown(PublisherLabel, string.Format(Strings.Gift2ActionReleasedBy, $"@{username}"));
            }
            else
            {
                Publisher.Visibility = Visibility.Collapsed;
            }

            if (gift.Gift.OverallLimits != null)
            {
                RibbonRoot.Visibility = Visibility.Visible;
                Ribbon.Text = string.Format(Strings.Gift2Limited1OfRibbon, gift.Gift.TotalText());
            }
            else
            {
                RibbonRoot.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdatePremiumGiftCode(MessageViewModel message, MessagePremiumGiftCode premiumGiftCode)
        {
            if (premiumGiftCode.Text.Text.Length > 0)
            {
                Subtitle.SetText(message.ClientService, premiumGiftCode.Text);
            }
            else
            {
                Subtitle.SetText(message.ClientService, ClientEx.ParseMarkdown(Strings.ActionGiftPremiumText));
            }

            // TODO: confirm no days here
            Title.Text = Locale.Declension(Strings.R.ActionGiftPremiumTitle2, premiumGiftCode.DayCount / 30);
            ViewLabel.Text = Strings.GiftPremiumUseGiftBtn;
            View.Visibility = Visibility.Visible;

            UpdateGiftAnimation(message, premiumGiftCode.Sticker);
        }

        private void UpdateGiftedPremium(MessageViewModel message, MessageGiftedPremium giftedPremium)
        {
            if (giftedPremium.Text.Text.Length > 0)
            {
                Subtitle.SetText(message.ClientService, giftedPremium.Text);
            }
            else
            {
                Subtitle.SetText(message.ClientService, ClientEx.ParseMarkdown(Strings.ActionGiftPremiumText));
            }

            // TODO: confirm no days here
            Title.Text = Locale.Declension(Strings.R.ActionGiftPremiumTitle2, giftedPremium.DayCount / 30);
            ViewLabel.Text = Strings.ActionGiftPremiumView;
            View.Visibility = Visibility.Visible;

            UpdateGiftAnimation(message, giftedPremium.Sticker);
        }

        private void UpdateGiftedStars(MessageViewModel message, MessageGiftedStars giftedStars)
        {
            Title.Text = Locale.Declension(Strings.R.ActionGiftStarsTitle, giftedStars.StarCount);
            ViewLabel.Text = Strings.ActionGiftStarsView;
            View.Visibility = Visibility.Visible;

            if (giftedStars.ReceiverUserId == 0)
            {
                Subtitle.SetText(message.ClientService, ClientEx.ParseMarkdown(Strings.ActionGiftStarsSubtitleYou));
            }
            else if (message.ClientService.TryGetUser(giftedStars.ReceiverUserId, out User receiver))
            {
                Subtitle.SetText(message.ClientService, ClientEx.ParseMarkdown(string.Format(Strings.ActionGiftStarsSubtitle, receiver.FullName(true))));
            }

            UpdateGiftAnimation(message, giftedStars.Sticker);
        }

        // Shared by the three contents that show a plain sticker: neither the publisher
        // chip nor the ribbon belongs to them, and both are MessageGift's to set.
        private void UpdateGiftAnimation(MessageViewModel message, Sticker sticker)
        {
            Animation.LoopCount = 1;
            Animation.Margin = new Thickness(0, -20, 0, 12);
            Animation.Source = DelayedFileSource.FromSticker(message.ClientService, sticker);

            Publisher.Visibility = Visibility.Collapsed;
            RibbonRoot.Visibility = Visibility.Collapsed;
        }

        public override void Recycle()
        {
            base.Recycle();

            Animation.Source = null;
            Subtitle.Clear();
        }

        private void Service_Click(object sender, RoutedEventArgs e)
        {
            if (Message?.Delegate != null)
            {
                Message.Delegate.ExecuteServiceMessage(Message);
            }
        }
    }
}
