using Telegram.Td.Api;
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
            if (item is Background wallpaper)
            {
                if (wallpaper.Id == 1000001)
                {
                    return DefaultTemplate;
                }

                if ((wallpaper.Id == Constants.WallpaperLocalId && wallpaper.Name != Constants.WallpaperColorFileName) || wallpaper.Document != null)
                {
                    return ItemTemplate;
                }

                return SolidTemplate;
            }

            return base.SelectTemplateCore(item, container);
        }
    }
}
