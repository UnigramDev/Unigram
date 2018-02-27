using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Selectors
{
    public class StickerSetTemplateSelector : DataTemplateSelector
    {
        public DataTemplate GroupTemplate { get; set; }
        public DataTemplate RecentsTemplate { get; set; }
        public DataTemplate FavedTemplate { get; set; }
        public DataTemplate ItemTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is ViewModels.Dialogs.StickerSetViewModel stickerSet)
            {
                if (stickerSet.Name.Equals("tg/recentlyUsed"))
                {
                    return RecentsTemplate ?? ItemTemplate;
                }
                else if (stickerSet.Name.Equals("tg/favedStickers"))
                {
                    return FavedTemplate ?? ItemTemplate;
                }
                else if (stickerSet.Name.Equals("tg/groupStickers"))
                {
                    return GroupTemplate ?? ItemTemplate;
                }

                return ItemTemplate;
            }

            return base.SelectTemplateCore(item, container);
        }
    }

    public class StickerSetStyleSelector : StyleSelector
    {
        public Style RecentStyle { get; set; }
        public Style FavoriteStyle { get; set; }
        public Style ItemStyle { get; set; }

        protected override Style SelectStyleCore(object item, DependencyObject container)
        {
            if (item is ViewModels.Dialogs.StickerSetViewModel stickerSet)
            {
                if (stickerSet.Name.Equals("tg/recentlyUsed"))
                {
                    return RecentStyle ?? ItemStyle;
                }
                else if (stickerSet.Name.Equals("tg/favedStickers"))
                {
                    return FavoriteStyle ?? ItemStyle;
                }
                //else if (stickerSet.Name.Equals("tg/groupStickers"))
                //{
                //    return GroupTemplate ?? ItemStyle;
                //}

                return ItemStyle;
            }

            return base.SelectStyleCore(item, container);
        }
    }
}
