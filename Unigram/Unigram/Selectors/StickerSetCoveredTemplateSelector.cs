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
    public class StickerSetCoveredTemplateSelector : DataTemplateSelector
    {
        public DataTemplate CoveredTemplate { get; set; }
        public DataTemplate MultiCoveredTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            return item is TLStickerSetCovered ? CoveredTemplate : MultiCoveredTemplate;
        }
    }
}
