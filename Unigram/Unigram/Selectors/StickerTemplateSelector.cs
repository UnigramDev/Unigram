using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Selectors
{
    public class StickerTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ItemTemplate { get; set; }
        public DataTemplate AnimatedTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is ViewModels.Dialogs.StickerViewModel sticker)
            {
                if (sticker.IsAnimated)
                {
                    return AnimatedTemplate;
                }

                return ItemTemplate;
            }

            return base.SelectTemplateCore(item, container);
        }
    }
}
