using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Selectors
{
    public class InlineBotResultTemplateSelector : DataTemplateSelector
    {
        public DataTemplate AudioMediaTemplate { get; set; }
        public DataTemplate AudioTemplate { get; set; }
        public DataTemplate ContactResultTemplate { get; set; }
        public DataTemplate GameMediaTemplate { get; set; }
        public DataTemplate GeoResultTemplate { get; set; }
        public DataTemplate GifTemplate { get; set; }
        public DataTemplate GifMediaTemplate { get; set; }
        public DataTemplate PhotoTemplate { get; set; }
        public DataTemplate PhotoMediaTemplate { get; set; }
        public DataTemplate ResultTemplate { get; set; }
        public DataTemplate StickerMediaTemplate { get; set; }
        public DataTemplate VenueResultTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            var result = item as InlineQueryResult;
            if (result != null && result.IsMedia())
            {
                return PhotoMediaTemplate;
            }

            if (item is MosaicMediaRow row)
            {
                if (row.Count == 1 && row[0].Width == 0)
                {
                    return ResultTemplate;
                }
                else
                {
                    return PhotoTemplate;
                }
            }

            return ResultTemplate;
        }
    }
}
