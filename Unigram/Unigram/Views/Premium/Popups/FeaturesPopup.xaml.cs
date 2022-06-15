using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Td.Api;
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

        public FeaturesPopup(IProtoService protoService, IList<PremiumFeature> features, IDictionary<Type, Animation> animations, PremiumFeature selectedFeature)
        {
            InitializeComponent();

            _protoService = protoService;
            _animations = animations;

            var items = features.Where(x => x is not PremiumFeatureIncreasedLimits).ToArray();

            Pager.NumberOfPages = items.Length;

            ScrollingHost.ItemsSource = items;
            ScrollingHost.SelectedItem = selectedFeature;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
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
        }
    }
}
