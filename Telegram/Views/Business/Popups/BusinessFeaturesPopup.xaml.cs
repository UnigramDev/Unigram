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
using Telegram.Views.Premium.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Business.Popups
{
    public sealed partial class BusinessFeaturesPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly IDictionary<Type, Animation> _animations;

        public BusinessFeaturesPopup(IClientService clientService, PremiumPaymentOption option, IList<BusinessFeature> features, IDictionary<Type, Animation> animations, BusinessFeature selectedFeature)
        {
            InitializeComponent();

            _clientService = clientService;
            _animations = animations;

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
            if (sender is PremiumFeatureCell cell && args.NewValue is BusinessFeature feature)
            {
                _animations.TryGetValue(feature.GetType(), out Animation value);
                cell.UpdateFeature(_clientService, feature, value);

                if (ScrollingHost.SelectedItem == feature)
                {
                    cell.PlayAnimation();
                }
            }
            else if (sender is PremiumFeatureUpgradedStoriesCell upgradedStoriesCell)
            {
                upgradedStoriesCell.UpdateFeature(_clientService);
            }
        }

        private void PurchaseShadow_Loaded(object sender, RoutedEventArgs e)
        {
            DropShadowEx.Attach(PurchaseShadow);
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ScrollingHost.SelectedItem is BusinessFeature feature)
            {
                //_clientService.Send(new ViewBusinessFeature(feature));
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
