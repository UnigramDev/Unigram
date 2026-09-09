//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.Views.Host;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class TransferGiftPopup : ModalPopup
    {
        public string Text { get; set; } = string.Empty;

        public TransferGiftPopup(IClientService clientService, ReceivedGift gift, Chat chat, bool resale)
        {
            InitializeComponent();

            if (gift.Gift is SentGiftUpgraded upgraded)
            {
                Animated.Source = new DelayedFileSource(clientService, upgraded.Gift.Model.Sticker);

                Photo1.Update(clientService, upgraded.Gift);

                if (chat != null)
                {
                    Photo2.Source = ProfilePictureSource.Chat(clientService, chat);
                }
                else if (clientService.TryGetUser(clientService.Options.MyId, out User user))
                {
                    Photo2.Source = ProfilePictureSource.User(clientService, user);
                }

                if (resale && upgraded.Gift.ResaleParameters != null)
                {
                    if (chat != null)
                    {
                        TextBlockHelper.SetMarkdown(MessageLabel, Locale.Declension(Strings.R.Gift2BuyPriceText, upgraded.Gift.ResaleParameters.StarCount, upgraded.Gift.ToName(), chat.Title));
                    }
                    else
                    {
                        TextBlockHelper.SetMarkdown(MessageLabel, Locale.Declension(Strings.R.Gift2BuyPriceSelfText, upgraded.Gift.ResaleParameters.StarCount, upgraded.Gift.ToName()));
                    }

                    PrimaryButtonContent = Strings.Gift2TransferDo;
                }
                else if (gift.TransferStarCount > 0)
                {
                    TextBlockHelper.SetMarkdown(MessageLabel, Locale.Declension(Strings.R.Gift2TransferPriceText, gift.TransferStarCount, upgraded.Gift.ToName(), chat.Title));
                    PrimaryButtonContent = Strings.Gift2TransferDo;
                }
                else
                {
                    TextBlockHelper.SetMarkdown(MessageLabel, string.Format(Strings.Gift2TransferText, upgraded.Gift.ToName(), chat.Title));
                    PrimaryButtonContent = Strings.Gift2TransferDo;
                }
            }


            PrimaryButtonStyle = BootStrapper.Current.Resources["AccentButtonStyle"] as Style;
            SecondaryButtonContent = Strings.Cancel;

        }

        public TransferGiftPopup(IClientService clientService, GiftForResale upgraded, Chat chat)
        {
            InitializeComponent();

            Animated.Source = new DelayedFileSource(clientService, upgraded.Gift.Model.Sticker);

            Photo1.Update(clientService, upgraded.Gift);

            if (chat != null)
            {
                Photo2.Source = ProfilePictureSource.Chat(clientService, chat);
            }
            else if (clientService.TryGetUser(clientService.Options.MyId, out User user))
            {
                Photo2.Source = ProfilePictureSource.User(clientService, user);
            }

            if (upgraded.Gift.ResaleParameters != null)
            {
                if (chat != null)
                {
                    TextBlockHelper.SetMarkdown(MessageLabel, Locale.Declension(Strings.R.Gift2BuyPriceText, upgraded.Gift.ResaleParameters.StarCount, upgraded.Gift.ToName(), chat.Title));
                }
                else
                {
                    TextBlockHelper.SetMarkdown(MessageLabel, Locale.Declension(Strings.R.Gift2BuyPriceSelfText, upgraded.Gift.ResaleParameters.StarCount, upgraded.Gift.ToName()));
                }

                PrimaryButtonContent = Strings.Gift2TransferDo;
            }


            PrimaryButtonStyle = BootStrapper.Current.Resources["AccentButtonStyle"] as Style;
            SecondaryButtonContent = Strings.Cancel;

        }

        public static Task<ContentDialogResult> ShowAsync(XamlRoot xamlRoot, IClientService clientService, ReceivedGift gift, Chat chat, bool resale)
        {
            var popup = new TransferGiftPopup(clientService, gift, chat, resale)
            {
                Width = 314,
                MinWidth = 314,
                MaxWidth = 314,
                IsLightDismissEnabled = true,
            };

            return popup.ShowAsync(xamlRoot);
        }

        public static Task<ContentDialogResult> ShowAsync(XamlRoot xamlRoot, IClientService clientService, GiftForResale gift, Chat chat)
        {
            var popup = new TransferGiftPopup(clientService, gift, chat)
            {
                Width = 314,
                MinWidth = 314,
                MaxWidth = 314,
                IsLightDismissEnabled = true,
            };

            return popup.ShowAsync(xamlRoot);
        }
    }
}
