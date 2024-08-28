//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Telegram.Td.Api;

namespace Telegram.Selectors
{
    public partial class PremiumFeatureTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ItemTemplate { get; set; }

        public DataTemplate StickersTemplate { get; set; }

        public DataTemplate StoriesTemplate { get; set; }

        public DataTemplate LimitsTemplate { get; set; }

        public DataTemplate BusinessTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            return item switch
            {
                PremiumFeatureUniqueStickers => StickersTemplate,
                PremiumFeatureUpgradedStories => StoriesTemplate,
                PremiumFeatureIncreasedLimits => LimitsTemplate,
                PremiumFeatureBusiness => BusinessTemplate,
                BusinessFeatureUpgradedStories => StoriesTemplate,
                _ => ItemTemplate
            };
        }
    }
}
