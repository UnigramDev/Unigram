//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI;

namespace Telegram.Controls
{
    // How a transaction reads in a list row and at the top of its receipt popup, for both
    // currencies: 45 StarTransactionType constructors and 9 TonTransactionType ones. The
    // list and the popup resolve through here rather than each carrying its own chain;
    // notes/star-transactions.md maps both unions back to MTProto and to the Android
    // branches these strings come from.
    public readonly struct TransactionInfo
    {
        public readonly ProfilePictureSource Photo;

        // Non-null only for paid media, where the row shows thumbnails instead of Photo.
        public readonly Vector<PaidMedia> Media;

        public readonly string Title;

        // Null when the row is a single line.
        public readonly string Subtitle;

        // Header of the receipt popup's counterparty row.
        public readonly string Header;

        // False when the transaction has no counterparty, and the popup shows a bare
        // source row rather than an avatar.
        public readonly bool HasSender;

        // The receipt popup leads with the reason and names the counterparty in its table,
        // so it reads the same two strings the other way round - unless the row has no
        // counterparty to move down, or its thumbnails already stand in for one.
        public string Heading => HasSender && Media == null && Subtitle != null ? Subtitle : Title;

        private TransactionInfo(ProfilePictureSource photo, Vector<PaidMedia> media, string title, string subtitle, string header, bool hasSender)
        {
            Photo = photo;
            Media = media;
            Title = title;
            Subtitle = subtitle;
            Header = header;
            HasSender = hasSender;
        }

        private static readonly ProfilePictureSourceText _premium = new(Icons.Premium, true, Color.FromArgb(0xFF, 0xFD, 0xD2, 0x1A), Color.FromArgb(0xFF, 0xE4, 0x7B, 0x03));
        private static readonly ProfilePictureSourceText _fragment = new(Icons.FragmentFilled, true, Colors.Black, Colors.Black);
        private static readonly ProfilePictureSourceText _api = ProfilePictureSourceText.GetGlyph(Icons.ChatStarsFilled, 3);
        private static readonly ProfilePictureSourceText _ads = ProfilePictureSourceText.GetGlyph(Icons.MegaphoneFilled, 1);
        private static readonly ProfilePictureSourceText _search = ProfilePictureSourceText.GetGlyph(Icons.SearchFilled, 5);
        private static readonly ProfilePictureSourceText _gift = ProfilePictureSourceText.GetGlyph(Icons.GiftPremium, 6);
        private static readonly ProfilePictureSourceText _diamond = ProfilePictureSourceText.GetGlyph(Icons.Diamond, 2);
        private static readonly ProfilePictureSourceText _unsupported = ProfilePictureSourceText.GetGlyph(Icons.QuestionCircle, long.MinValue);

        private static TransactionInfo FromSource(ProfilePictureSource photo, string title, string subtitle = null)
        {
            return new TransactionInfo(photo, null, title, subtitle, Strings.StarsTransactionSource, false);
        }

        private static TransactionInfo FromUser(IClientService clientService, long userId, string subtitle, string header)
        {
            if (clientService.TryGetUser(userId, out User user))
            {
                return new TransactionInfo(ProfilePictureSource.User(clientService, user), null, user.FullName(), subtitle, header, true);
            }

            return new TransactionInfo(_unsupported, null, Strings.StarsTransactionUnknown, subtitle, header, true);
        }

        private static TransactionInfo FromChat(IClientService clientService, long chatId, string subtitle, string header)
        {
            if (clientService.TryGetChat(chatId, out Chat chat))
            {
                return new TransactionInfo(ProfilePictureSource.Chat(clientService, chat), null, chat.Title, subtitle, header, true);
            }

            return new TransactionInfo(_unsupported, null, Strings.StarsTransactionUnknown, subtitle, header, true);
        }

        private static TransactionInfo FromSender(IClientService clientService, MessageSender sender, string subtitle, string header)
        {
            var photo = ProfilePictureSource.MessageSender(clientService, sender);
            if (photo == null)
            {
                return new TransactionInfo(_unsupported, null, Strings.StarsTransactionUnknown, subtitle, header, true);
            }

            return new TransactionInfo(photo, null, clientService.GetTitle(sender), subtitle, header, true);
        }

        private static TransactionInfo FromMedia(Vector<PaidMedia> media, TransactionInfo peer)
        {
            // The thumbnails replace the avatar, so the counterparty moves down to the subtitle.
            return new TransactionInfo(peer.Photo, media, Strings.StarMediaPurchase, peer.Title, peer.Header, true);
        }

        public static TransactionInfo FromStarTransaction(IClientService clientService, StarTransaction transaction)
        {
            var refund = transaction.IsRefund;

            switch (transaction.Type)
            {
                // Deposits and withdrawals: no counterparty, so the row is the platform itself.
                case StarTransactionTypePremiumBotDeposit:
                    return FromSource(_premium, Strings.StarsTransactionBot);
                case StarTransactionTypeAppStoreDeposit:
                case StarTransactionTypeGooglePlayDeposit:
                    return FromSource(_premium, Strings.StarsTransactionInApp);
                case StarTransactionTypeFragmentDeposit:
                    return FromSource(_fragment, Strings.StarsTransactionFragment);
                case StarTransactionTypeFragmentWithdrawal:
                    return FromSource(_fragment, Strings.StarsTransactionWithdrawFragment);
                case StarTransactionTypeTelegramAdsWithdrawal:
                    return FromSource(_ads, Strings.StarsTransactionAds);
                case StarTransactionTypeTelegramApiUsage telegramApiUsage:
                    return FromSource(_api, Strings.StarsTransactionFloodskip, Locale.Declension(Strings.R.StarsTransactionFloodskipMessages, telegramApiUsage.RequestCount));
                case StarTransactionTypePublicPostSearch:
                    return FromSource(_search, Strings.StarsTransactionPostsSearch);

                // A gift of stars. user_id is 0 when it came through Fragment rather than
                // from a user, and then there is nobody to name.
                case StarTransactionTypeUserDeposit userDeposit:
                    if (userDeposit.UserId == 0)
                    {
                        return FromSource(_fragment, Strings.StarsGiftReceived, Strings.StarsTransactionUnknown);
                    }

                    return FromUser(clientService, userDeposit.UserId, refund ? Strings.StarsGiftSent : Strings.StarsGiftReceived, Strings.Gift2From);
                case StarTransactionTypeGiveawayDeposit giveawayDeposit:
                    return FromChat(clientService, giveawayDeposit.ChatId, Strings.StarsGiveawayPrizeReceived, Strings.StarGiveawayPrizeFrom);

                // Paid media. The thumbnails carry the row, so these fall through FromMedia.
                case StarTransactionTypeBotPaidMediaPurchase botPaidMediaPurchase:
                    return FromMedia(botPaidMediaPurchase.Media, FromUser(clientService, botPaidMediaPurchase.UserId, null, Strings.StarsTransactionRecipient));
                case StarTransactionTypeBotPaidMediaSale botPaidMediaSale:
                    return FromMedia(botPaidMediaSale.Media, FromUser(clientService, botPaidMediaSale.UserId, null, Strings.Gift2From));
                case StarTransactionTypeChannelPaidMediaPurchase channelPaidMediaPurchase:
                    return FromMedia(channelPaidMediaPurchase.Media, FromChat(clientService, channelPaidMediaPurchase.ChatId, null, Strings.StarsTransactionRecipient));
                case StarTransactionTypeChannelPaidMediaSale channelPaidMediaSale:
                    return FromMedia(channelPaidMediaSale.Media, FromUser(clientService, channelPaidMediaSale.UserId, null, Strings.Gift2From));

                // Bot invoices and subscriptions.
                case StarTransactionTypeBotInvoicePurchase botInvoicePurchase:
                    return FromUser(clientService, botInvoicePurchase.UserId, botInvoicePurchase.ProductInfo.Title, Strings.StarsTransactionRecipient);
                case StarTransactionTypeBotInvoiceSale botInvoiceSale:
                    return FromUser(clientService, botInvoiceSale.UserId, botInvoiceSale.ProductInfo.Title, Strings.Gift2From);
                case StarTransactionTypeBotSubscriptionPurchase botSubscriptionPurchase:
                    return FromUser(clientService, botSubscriptionPurchase.UserId, Strings.StarsTransactionSubscriptionMonthly, Strings.StarSubscriptionTo);
                case StarTransactionTypeBotSubscriptionSale botSubscriptionSale:
                    return FromUser(clientService, botSubscriptionSale.UserId, Strings.StarsTransactionSubscriptionMonthly, Strings.Gift2From);
                case StarTransactionTypeChannelSubscriptionPurchase channelSubscriptionPurchase:
                    return FromChat(clientService, channelSubscriptionPurchase.ChatId, Strings.StarsTransactionSubscriptionMonthly, Strings.StarSubscriptionTo);
                case StarTransactionTypeChannelSubscriptionSale channelSubscriptionSale:
                    return FromUser(clientService, channelSubscriptionSale.UserId, Strings.StarsTransactionSubscriptionMonthly, Strings.Gift2From);

                // Paid reactions and paid messages.
                case StarTransactionTypeChannelPaidReactionSend channelPaidReactionSend:
                    return FromChat(clientService, channelPaidReactionSend.ChatId, Strings.StarsReactionsSent, Strings.StarsTransactionRecipient);
                case StarTransactionTypeChannelPaidReactionReceive channelPaidReactionReceive:
                    return FromUser(clientService, channelPaidReactionReceive.UserId, Strings.StarsReactionsSent, Strings.Gift2From);
                case StarTransactionTypePaidMessageSend paidMessageSend:
                    return FromChat(clientService, paidMessageSend.ChatId, Locale.Declension(Strings.R.StarsTransactionMessageFee, paidMessageSend.MessageCount), Strings.Gift2To);
                case StarTransactionTypePaidMessageReceive paidMessageReceive:
                    return FromSender(clientService, paidMessageReceive.SenderId, Locale.Declension(Strings.R.StarsTransactionMessageFee, paidMessageReceive.MessageCount), Strings.Gift2From);
                case StarTransactionTypePaidGroupCallMessageSend paidGroupCallMessageSend:
                    return FromChat(clientService, paidGroupCallMessageSend.ChatId, Strings.StarsTransactionLiveStoryMessageFee, Strings.Gift2To);
                case StarTransactionTypePaidGroupCallMessageReceive paidGroupCallMessageReceive:
                    return FromSender(clientService, paidGroupCallMessageReceive.SenderId, Strings.StarsTransactionLiveStoryMessageFee, Strings.Gift2From);
                case StarTransactionTypePaidGroupCallReactionSend paidGroupCallReactionSend:
                    return FromChat(clientService, paidGroupCallReactionSend.ChatId, Strings.StarsTransactionLiveStoryReactionFee, Strings.Gift2To);
                case StarTransactionTypePaidGroupCallReactionReceive paidGroupCallReactionReceive:
                    return FromSender(clientService, paidGroupCallReactionReceive.SenderId, Strings.StarsTransactionLiveStoryReactionFee, Strings.Gift2From);

                // TDLib recognises a suggested post by the server-sent product title and then
                // drops it, so the wording has to be restated here.
                case StarTransactionTypeSuggestedPostPaymentSend suggestedPostPaymentSend:
                    return FromChat(clientService, suggestedPostPaymentSend.ChatId, Strings.StarsTransactionSuggestedPost, Strings.Gift2To);
                case StarTransactionTypeSuggestedPostPaymentReceive suggestedPostPaymentReceive:
                    return FromUser(clientService, suggestedPostPaymentReceive.UserId, Strings.StarsTransactionSuggestedPost, Strings.Gift2From);

                case StarTransactionTypeAffiliateProgramCommission affiliateProgramCommission:
                    return FromChat(clientService, affiliateProgramCommission.ChatId, string.Format(Strings.StarTransactionCommission, affiliateProgramCommission.CommissionPerMille.CommissionPercent()), Strings.StarAffiliate);

                case StarTransactionTypePremiumPurchase premiumPurchase:
                    return FromUser(clientService, premiumPurchase.UserId, Strings.StarsTransactionPremiumGift, Strings.Gift2To);

                // TDLib drops the product info here too, and unlike the suggested post there
                // is no fixed wording to put back, so the bot name is the whole row.
                case StarTransactionTypeBusinessBotTransferSend businessBotTransferSend:
                    return FromUser(clientService, businessBotTransferSend.UserId, null, Strings.StarsTransactionRecipient);
                case StarTransactionTypeBusinessBotTransferReceive businessBotTransferReceive:
                    return FromUser(clientService, businessBotTransferReceive.UserId, null, Strings.Gift2From);

                // Gifts.
                case StarTransactionTypeGiftPurchase giftPurchase:
                    return FromSender(clientService, giftPurchase.OwnerId, refund ? Strings.Gift2TransactionRefundedSent : Strings.Gift2TransactionSent, Strings.Gift2To);
                case StarTransactionTypeGiftSale giftSale:
                    return FromUser(clientService, giftSale.UserId, refund ? Strings.Gift2TransactionRefundedConverted : Strings.Gift2TransactionConverted, Strings.Gift2From);
                case StarTransactionTypeGiftAuctionBid giftAuctionBid:
                    return FromSender(clientService, giftAuctionBid.OwnerId, refund ? Strings.Gift2TransactionRefundedAuctionBid : Strings.Gift2TransactionAuctionBid, Strings.Gift2To);
                case StarTransactionTypeGiftUpgrade giftUpgrade:
                    return FromUser(clientService, giftUpgrade.UserId, refund ? Strings.Gift2TransactionRefundedUpgrade : Strings.Gift2TransactionUpgraded, Strings.StarGiftUpgradeGiftFrom);
                case StarTransactionTypeGiftUpgradePurchase giftUpgradePurchase:
                    return FromSender(clientService, giftUpgradePurchase.OwnerId, Strings.Gift2TransactionPrepaidUpgrade, Strings.Gift2To);
                case StarTransactionTypeGiftOriginalDetailsDrop giftOriginalDetailsDrop:
                    return FromSender(clientService, giftOriginalDetailsDrop.OwnerId, Strings.Gift2TransactionRemovedDescription, Strings.StarsTransactionRecipient);
                case StarTransactionTypeGiftTransfer giftTransfer:
                    return FromSender(clientService, giftTransfer.OwnerId, refund ? Strings.StarGiftTransactionGiftTransferRefund : Strings.StarGiftTransactionGiftTransfer, Strings.Gift2To);
                case StarTransactionTypeUpgradedGiftPurchase upgradedGiftPurchase:
                    return FromUser(clientService, upgradedGiftPurchase.UserId, refund ? Strings.StarGiftTransactionGiftSaleRefund : Strings.StarGiftTransactionGiftPurchase, Strings.Gift2From);
                case StarTransactionTypeUpgradedGiftSale upgradedGiftSale:
                    // A sale settled against a standing offer is worded apart from an ordinary
                    // resale, and a refunded one reads as the buyer's side of the same deal.
                    var sale = refund
                        ? upgradedGiftSale.ViaOffer
                            ? Strings.StarGiftTransactionGiftOfferRefund
                            : Strings.StarGiftTransactionGiftPurchaseRefund
                        : Strings.StarGiftTransactionGiftSale;

                    return FromUser(clientService, upgradedGiftSale.UserId, sale, Strings.Gift2To);

                // The only gift type with no counterparty: the seller isn't known at bid time.
                case StarTransactionTypeGiftPurchaseOffer giftPurchaseOffer:
                    return FromSource(_gift, giftPurchaseOffer.Gift.Title, refund ? Strings.StarGiftTransactionGiftSaleRefund : Strings.StarGiftTransactionGiftOffer);

                default:
                    return FromSource(_unsupported, Strings.StarsTransactionUnsupported);
            }
        }
    }
}
