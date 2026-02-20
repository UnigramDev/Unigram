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
    public sealed partial class TransferGiftPopup : TeachingTipEx
    {
        public string Text { get; set; } = string.Empty;

        private readonly TaskCompletionSource<ContentDialogResult> _tsc = new();

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

                    ActionButtonContent = Strings.Gift2TransferDo;
                }
                else if (gift.TransferStarCount > 0)
                {
                    TextBlockHelper.SetMarkdown(MessageLabel, Locale.Declension(Strings.R.Gift2TransferPriceText, gift.TransferStarCount, upgraded.Gift.ToName(), chat.Title));
                    ActionButtonContent = Strings.Gift2TransferDo;
                }
                else
                {
                    TextBlockHelper.SetMarkdown(MessageLabel, string.Format(Strings.Gift2TransferText, upgraded.Gift.ToName(), chat.Title));
                    ActionButtonContent = Strings.Gift2TransferDo;
                }
            }

            ActionButtonClick += OnAction;

            ActionButtonStyle = BootStrapper.Current.Resources["AccentButtonStyle"] as Style;
            CloseButtonContent = Strings.Cancel;

            Closed += OnClosed;
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

                ActionButtonContent = Strings.Gift2TransferDo;
            }

            ActionButtonClick += OnAction;

            ActionButtonStyle = BootStrapper.Current.Resources["AccentButtonStyle"] as Style;
            CloseButtonContent = Strings.Cancel;

            Closed += OnClosed;
        }

        private void OnAction(TeachingTip sender, object args)
        {
            _tsc.TrySetResult(ContentDialogResult.Primary);
            IsOpen = false;
        }

        private void OnClosed(TeachingTip sender, TeachingTipClosedEventArgs args)
        {
            _tsc.TrySetResult(ContentDialogResult.Secondary);
        }

        public Task<ContentDialogResult> ShowAsync()
        {
            IsOpen = true;
            return _tsc.Task;
        }

        public static Task<ContentDialogResult> ShowAsync(XamlRoot xamlRoot, IClientService clientService, ReceivedGift gift, Chat chat, bool resale)
        {
            if (xamlRoot.Content is not IToastHost host)
            {
                return null;
            }

            var popup = new TransferGiftPopup(clientService, gift, chat, resale)
            {
                PreferredPlacement = TeachingTipPlacementMode.Center,
                Width = 314,
                MinWidth = 314,
                MaxWidth = 314,
                IsLightDismissEnabled = true,
                ShouldConstrainToRootBounds = true,
            };

            popup.Closed += (s, args) =>
            {
                host.ToastClosed(s);
            };

            host.ToastOpened(popup);

            return popup.ShowAsync();
        }

        public static Task<ContentDialogResult> ShowAsync(XamlRoot xamlRoot, IClientService clientService, GiftForResale gift, Chat chat)
        {
            if (xamlRoot.Content is not IToastHost host)
            {
                return null;
            }

            var popup = new TransferGiftPopup(clientService, gift, chat)
            {
                PreferredPlacement = TeachingTipPlacementMode.Center,
                Width = 314,
                MinWidth = 314,
                MaxWidth = 314,
                IsLightDismissEnabled = true,
                ShouldConstrainToRootBounds = true,
            };

            popup.Closed += (s, args) =>
            {
                host.ToastClosed(s);
            };

            host.ToastOpened(popup);

            return popup.ShowAsync();
        }
    }
}
