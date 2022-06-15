using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Selectors
{
    public class PremiumFeatureTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ItemTemplate { get; set; }

        public DataTemplate StickersTemplate { get; set; }

        public DataTemplate ReactionsTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is PremiumFeatureUniqueReactions)
            {
                return ReactionsTemplate;
            }
            else if (item is PremiumFeatureUniqueStickers)
            {
                return StickersTemplate;
            }

            return ItemTemplate;
        }
    }
}
