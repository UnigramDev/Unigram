//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells.Premium;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Views.Premium.Popups
{
    public sealed partial class FeaturesPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly IDictionary<Type, Animation> _animations;
        private readonly Stickers _stickers;

        public FeaturesPopup(IClientService clientService, PremiumPaymentOption option, IList<PremiumFeature> features, IDictionary<Type, Animation> animations, Stickers stickers, PremiumFeature selectedFeature)
        {
            InitializeComponent();

            _clientService = clientService;
            _animations = animations;
            _stickers = stickers;

            var items = features.Where(x => x is not PremiumFeatureIncreasedLimits).ToArray();

            Pager.NumberOfPages = items.Length;

            ScrollingHost.ItemsSource = items;
            ScrollingHost.SelectedItem = selectedFeature;

            PurchaseCommand.Content = clientService.IsPremium
                ? Strings.Resources.OK
                : string.Format(Strings.Resources.SubscribeToPremium, Locale.FormatCurrency(option.Amount / option.MonthCount, option.Currency));
        }

        public bool ShouldPurchase { get; private set; }

        private void Purchase_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            ShouldPurchase = true;
            Hide();
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems?.Count > 0)
            {
                var container = ScrollingHost.ContainerFromItem(e.AddedItems[0]) as SelectorItem;

                var content = container?.ContentTemplateRoot as PremiumFeatureCell;
                if (content != null)
                {
                    content.PlayAnimation();
                }

                _clientService.Send(new ViewPremiumFeature(e.AddedItems[0] as PremiumFeature));
            }

            if (e.RemovedItems?.Count > 0)
            {
                var container = ScrollingHost.ContainerFromItem(e.RemovedItems[0]) as SelectorItem;

                var content = container?.ContentTemplateRoot as PremiumFeatureCell;
                if (content != null)
                {
                    content.StopAnimation();
                }
            }
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (sender is PremiumFeatureCell cell && args.NewValue is PremiumFeature feature)
            {
                _animations.TryGetValue(feature.GetType(), out Animation value);
                cell.UpdateFeature(_clientService, feature, value);
            }
            else if (sender is PremiumFeatureUniqueStickersCell uniqueStickersCell)
            {
                uniqueStickersCell.UpdateFature(_clientService, _stickers?.StickersValue);
            }
        }

        private void PurchaseShadow_Loaded(object sender, RoutedEventArgs e)
        {
            DropShadowEx.Attach(PurchaseShadow);
        }
    }
}
