//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells.Premium;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Premium.Popups
{
    public sealed partial class FeaturesPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly IList<BusinessFeature> _businessFeatures;
        private readonly IList<PremiumLimit> _limits;
        private readonly IDictionary<Type, Animation> _animations;
        private readonly Stickers _stickers;

        public FeaturesPopup(IClientService clientService, PremiumPaymentOption option, IList<PremiumFeature> features, IList<BusinessFeature> businessFeatures, IList<PremiumLimit> limits, IDictionary<Type, Animation> animations, Stickers stickers, PremiumFeature selectedFeature)
        {
            InitializeComponent();

            _clientService = clientService;
            _businessFeatures = businessFeatures;
            _limits = limits;
            _animations = animations;
            _stickers = stickers;

            Pager.NumberOfPages = features.Count;
            Pager.Visibility = features.Count > 1
                ? Visibility.Visible
                : Visibility.Collapsed;

            ScrollingHost.ItemsSource = features;
            ScrollingHost.SelectedItem = selectedFeature;

            PurchaseCommand.Content = PromoPopup.GetPaymentString(clientService.IsPremium, option);
        }

        public bool ShouldPurchase { get; private set; }

        private void Purchase_Click(object sender, RoutedEventArgs e)
        {
            ShouldPurchase = true;
            Hide();
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (sender is PremiumFeatureCell cell && args.NewValue is PremiumFeature feature)
            {
                _animations.TryGetValue(feature.GetType(), out Animation value);
                cell.UpdateFeature(_clientService, feature, value);

                if (ScrollingHost.SelectedItem == feature)
                {
                    cell.PlayAnimation();
                }
            }
            else if (sender is PremiumFeatureUniqueStickersCell uniqueStickersCell)
            {
                uniqueStickersCell.UpdateFeature(_clientService, _stickers?.StickersValue);
            }
            else if (sender is PremiumFeatureIncreasedLimitsCell increasedLimitsCell)
            {
                increasedLimitsCell.UpdateFeature(_clientService, _limits);
            }
            else if (sender is PremiumFeatureUpgradedStoriesCell upgradedStoriesCell)
            {
                upgradedStoriesCell.UpdateFeature(_clientService);
            }
            else if (sender is PremiumFeatureBusinessCell businessCell)
            {
                businessCell.UpdateFeature(_clientService, _businessFeatures);
            }
        }

        private void PurchaseShadow_Loaded(object sender, RoutedEventArgs e)
        {
            DropShadowEx.Attach(PurchaseShadow);
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ScrollingHost.SelectedItem is PremiumFeature feature)
            {
                _clientService.Send(new ViewPremiumFeature(feature));
            }

            if (e.AddedItems.Count > 0)
            {
                var selector = ScrollingHost.ContainerFromItem(e.AddedItems[0]) as FlipViewItem;
                var cell = selector?.ContentTemplateRoot as IPremiumFeatureCell;
                cell?.PlayAnimation();
            }

            if (e.RemovedItems.Count > 0)
            {
                var selector = ScrollingHost.ContainerFromItem(e.RemovedItems[0]) as FlipViewItem;
                var cell = selector?.ContentTemplateRoot as IPremiumFeatureCell;
                cell?.StopAnimation();
            }
        }
    }
}
