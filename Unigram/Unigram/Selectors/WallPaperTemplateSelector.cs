using TdWindows;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Selectors
{
    public class WallPaperTemplateSelector : DataTemplateSelector
    {
        public DataTemplate DefaultTemplate { get; set; }

        public DataTemplate ItemTemplate { get; set; }
        public DataTemplate SolidTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is Wallpaper wallpaper)
            {
                if (wallpaper.Id == 1000001)
                {
                    return DefaultTemplate;
                }

                if (wallpaper.Sizes != null && wallpaper.Sizes.Count > 0)
                {
                    return ItemTemplate;
                }

                return SolidTemplate;
            }

            return base.SelectTemplateCore(item, container);
        }
    }
}
