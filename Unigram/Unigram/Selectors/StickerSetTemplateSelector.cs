using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Telegram.Api.TL.Messages;
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
            if (item is TLMessagesStickerSet stickerSet)
            {
                if (stickerSet.Set.ShortName.Equals("tg/recentlyUsed"))
                {
                    return RecentsTemplate ?? ItemTemplate;
                }
                else if (stickerSet.Set.ShortName.Equals("tg/favedStickers"))
                {
                    return FavedTemplate ?? ItemTemplate;
                }
                else if (stickerSet.Set.ShortName.Equals("tg/groupStickers"))
                {
                    return GroupTemplate ?? ItemTemplate;
                }

                return ItemTemplate;
            }

            return base.SelectTemplateCore(item, container);
        }
    }
}
