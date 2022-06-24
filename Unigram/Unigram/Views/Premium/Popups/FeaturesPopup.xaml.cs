using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Cells.Premium;
using Unigram.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Unigram.Views.Premium.Popups
{
    public sealed partial class FeaturesPopup : ContentPopup
    {
        private readonly IProtoService _protoService;
        private readonly IDictionary<Type, Animation> _animations;
        private readonly Stickers _stickers;

        public FeaturesPopup(IProtoService protoService, PremiumState state, IList<PremiumFeature> features, IDictionary<Type, Animation> animations, Stickers stickers, PremiumFeature selectedFeature)
        {
            InitializeComponent();

            _protoService = protoService;
            _animations = animations;
            _stickers = stickers;

            var items = features.Where(x => x is not PremiumFeatureIncreasedLimits).ToArray();

            Pager.NumberOfPages = items.Length;

            ScrollingHost.ItemsSource = items;
            ScrollingHost.SelectedItem = selectedFeature;

            PurchaseCommand.Content = protoService.IsPremium
                ? Strings.Resources.OK
                : string.Format(Strings.Resources.SubscribeToPremium, Locale.FormatCurrency(state.MonthlyAmount, state.Currency));
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

                _protoService.Send(new ViewPremiumFeature(e.AddedItems[0] as PremiumFeature));
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
            if (sender is PremiumFeatureCell cell && args.NewValue is PremiumFeature feature && _animations.TryGetValue(feature.GetType(), out Animation value))
            {
                cell.UpdateFeature(_protoService, feature, value);
            }
            else if (sender is PremiumFeatureUniqueReactionsCell uniqueReactionsCell)
            {
                uniqueReactionsCell.UpdateFeature(_protoService);
            }
            else if (sender is PremiumFeatureUniqueStickersCell uniqueStickersCell)
            {
                uniqueStickersCell.UpdateFature(_protoService, _stickers?.StickersValue);
            }
        }

        private void PurchaseShadow_Loaded(object sender, RoutedEventArgs e)
        {
            DropShadowEx.Attach(PurchaseShadow);
        }
    }
}
