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
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.Views.Host;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Stars.Popups
{
    public sealed partial class UpgradedGiftValuePopup : TeachingTipEx
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        private readonly UpgradedGift _gift;
        private readonly UpgradedGiftValueInfo _valueInfo;

        private readonly TaskCompletionSource<ContentDialogResult> _tsc = new();

        public UpgradedGiftValuePopup(IClientService clientService, INavigationService navigationService, UpgradedGift gift, UpgradedGiftValueInfo valueInfo)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            _gift = gift;
            _valueInfo = valueInfo;

            AnimatedPhoto.Source = DelayedFileSource.FromSticker(clientService, gift.Model.Sticker);

            TitleLabel.Text = Locale.FormatCurrency(gift.ValueAmount, gift.ValueCurrency);
            TextBlockHelper.SetMarkdown(MessageLabel, string.Format(Strings.GiftValueAverage, gift.Title));

            InitialSale.Content = Formatter.DateAt(valueInfo.InitialSaleDate);
            InitialPrice.Text = string.Format("{0} (~{1})", valueInfo.InitialSaleStarCount, Locale.FormatCurrency(valueInfo.InitialSalePrice, valueInfo.Currency));

            if (valueInfo.LastSaleDate != 0)
            {
                LastSale.Content = Formatter.DateAt(valueInfo.LastSaleDate);
            }
            else
            {
                LastSale.Visibility = Visibility.Collapsed;
            }

            if (valueInfo.LastSalePrice != 0)
            {
                LastPrice.Text = Locale.FormatCurrency(valueInfo.LastSalePrice, valueInfo.Currency);
                LastPriceInfo.Glyph = Formatter.Percent((double)valueInfo.LastSalePrice / valueInfo.InitialSalePrice);
            }
            else
            {
                LastPriceRoot.Visibility = Visibility.Collapsed;
            }

            if (valueInfo.MinimumPrice != 0)
            {
                MinimumPrice.Text = Locale.FormatCurrency(valueInfo.MinimumPrice, valueInfo.Currency);
            }
            else
            {
                MinimumPriceRoot.Visibility = Visibility.Collapsed;
            }

            if (valueInfo.AverageSalePrice != 0)
            {
                AveragePrice.Text = Locale.FormatCurrency(valueInfo.AverageSalePrice, valueInfo.Currency);
            }
            else
            {
                AveragePriceRoot.Visibility = Visibility.Collapsed;
            }

            if (valueInfo.TelegramListedGiftCount != 0)
            {
                TelegramListedGiftCount.Content = string.Format("{0} {1}", valueInfo.TelegramListedGiftCount, Strings.GiftValueOnSaleTelegram);
            }
            else
            {
                TelegramListedGiftCount.Visibility = Visibility.Collapsed;
            }

            if (valueInfo.FragmentListedGiftCount != 0)
            {
                FragmentListedGiftCount.Content = string.Format("{0} {1}", valueInfo.FragmentListedGiftCount, Strings.GiftValueOnSaleFragment);
            }
            else
            {
                FragmentListedGiftCount.Visibility = Visibility.Collapsed;
            }

            ActionButtonClick += OnAction;

            ActionButtonStyle = BootStrapper.Current.Resources["AccentButtonStyle"] as Style;
            //ActionButtonContent = Strings.OK;

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

        public static Task<ContentDialogResult> ShowAsync(XamlRoot xamlRoot, IClientService clientService, INavigationService navigationService, UpgradedGift gift, UpgradedGiftValueInfo valueInfo)
        {
            if (xamlRoot.Content is not IToastHost host)
            {
                return null;
            }

            var popup = new UpgradedGiftValuePopup(clientService, navigationService, gift, valueInfo)
            {
                PreferredPlacement = TeachingTipPlacementMode.Center,
                Width = 314,
                MinWidth = 314,
                MaxWidth = 314,
                MaxHeight = 720,
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

        private void MinimumPriceInfo_Click(object sender, RoutedEventArgs e)
        {
            ToastPopup.Show(MinimumPriceInfo, string.Format(Strings.GiftValueMinPriceInfo, Locale.FormatCurrency(_valueInfo.MinimumPrice, _valueInfo.Currency), _gift.Title), TeachingTipPlacementMode.Top);
        }

        private void AveragePriceInfo_Click(object sender, RoutedEventArgs e)
        {
            ToastPopup.Show(AveragePriceInfo, string.Format(Strings.GiftValueAveragePriceInfo, Locale.FormatCurrency(_valueInfo.AverageSalePrice, _valueInfo.Currency), _gift.Title), TeachingTipPlacementMode.Top);
        }

        private void TelegramListedGiftCount_Click(object sender, RoutedEventArgs e)
        {
            IsOpen = false;
            _navigationService.HidePopup(typeof(ReceivedGiftPopup));
            _navigationService.ShowPopup(new ResoldGiftsPopup(_clientService, _navigationService, _gift, _valueInfo, _clientService.MyId));
        }

        private void FragmentListedGiftCount_Click(object sender, RoutedEventArgs e)
        {
            IsOpen = false;
            MessageHelper.OpenUrl(_clientService, _navigationService, _valueInfo.FragmentUrl);
        }
    }
}
