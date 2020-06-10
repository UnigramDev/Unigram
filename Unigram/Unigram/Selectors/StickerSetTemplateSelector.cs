using System;
using Unigram.ViewModels.Drawers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Selectors
{
    public class StickerSetTemplateSelector : DataTemplateSelector
    {
        public DataTemplate GroupTemplate { get; set; }
        public DataTemplate RecentsTemplate { get; set; }
        public DataTemplate TrendingTemplate { get; set; }
        public DataTemplate FavedTemplate { get; set; }
        public DataTemplate ItemTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is StickerSetViewModel stickerSet)
            {
                if (string.Equals(stickerSet.Name, "tg/recentlyUsed", StringComparison.OrdinalIgnoreCase))
                {
                    return RecentsTemplate ?? ItemTemplate;
                }
                else if (string.Equals(stickerSet.Name, "tg/favedStickers", StringComparison.OrdinalIgnoreCase))
                {
                    return FavedTemplate ?? ItemTemplate;
                }
                else if (string.Equals(stickerSet.Name, "tg/groupStickers", StringComparison.OrdinalIgnoreCase))
                {
                    return GroupTemplate ?? ItemTemplate;
                }
            }
            else if (item is AnimationsCollection animations)
            {
                if (string.Equals(animations.Name, "tg/recentlyUsed", StringComparison.OrdinalIgnoreCase))
                {
                    return RecentsTemplate ?? ItemTemplate;
                }
                else if (string.Equals(animations.Name, "tg/trending", StringComparison.OrdinalIgnoreCase))
                {
                    return TrendingTemplate ?? ItemTemplate;
                }
            }

            return ItemTemplate;
        }
    }

    public class StickerSetStyleSelector : StyleSelector
    {
        public Style RecentStyle { get; set; }
        public Style FavoriteStyle { get; set; }
        public Style TrendingStyle { get; set; }
        public Style ItemStyle { get; set; }

        protected override Style SelectStyleCore(object item, DependencyObject container)
        {
            if (item is StickerSetViewModel stickerSet)
            {
                if (string.Equals(stickerSet.Name, "tg/recentlyUsed", StringComparison.OrdinalIgnoreCase))
                {
                    return RecentStyle ?? ItemStyle;
                }
                else if (string.Equals(stickerSet.Name, "tg/favedStickers", StringComparison.OrdinalIgnoreCase))
                {
                    return FavoriteStyle ?? ItemStyle;
                }
                //else if (string.Equals(stickerSet.Name, "tg/groupStickers", StringComparison.OrdinalIgnoreCase))
                //{
                //    return GroupTemplate ?? ItemTemplate;
                //}
            }
            else if (item is AnimationsCollection animations)
            {
                if (string.Equals(animations.Name, "tg/recentlyUsed", StringComparison.OrdinalIgnoreCase))
                {
                    return RecentStyle ?? ItemStyle;
                }
                else if (string.Equals(animations.Name, "tg/trending", StringComparison.OrdinalIgnoreCase))
                {
                    return TrendingStyle ?? ItemStyle;
                }
            }

            return ItemStyle;
        }
    }
}
