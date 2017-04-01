using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Selectors
{
    public class StickerSetItemTemplateSelector : DataTemplateSelector
    {
        public bool IsMasks { get; set; }

        public DataTemplate ImageTemplate { get; set; }
        public DataTemplate MaskTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            return IsMasks ? MaskTemplate : ImageTemplate;
        }
    }
}
