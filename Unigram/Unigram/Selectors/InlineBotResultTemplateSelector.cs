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
            var resultBase = item as TLBotInlineResultBase;
            if (resultBase != null)
            {
                if (resultBase.SendMessage is TLBotInlineMessageMediaContact)
                {
                    return ContactResultTemplate;
                }
                else if (resultBase.SendMessage is TLBotInlineMessageMediaVenue)
                {
                    return VenueResultTemplate;
                }
                else if (resultBase.SendMessage is TLBotInlineMessageMediaGeo)
                {
                    return GeoResultTemplate;
                }
            }

            if (item is TLBotInlineMediaResult mediaResult)
            {
                if (mediaResult.Type.Equals("photo", StringComparison.OrdinalIgnoreCase))
                {
                    return PhotoMediaTemplate;
                }
                else if (mediaResult.Type.Equals("sticker", StringComparison.OrdinalIgnoreCase))
                {
                    return StickerMediaTemplate;
                }
                else if (mediaResult.Type.Equals("gif", StringComparison.OrdinalIgnoreCase))
                {
                    return GifMediaTemplate;
                }
                else if (mediaResult.Type.Equals("audio", StringComparison.OrdinalIgnoreCase))
                {
                    return AudioMediaTemplate;
                }
                else if (mediaResult.Type.Equals("voice", StringComparison.OrdinalIgnoreCase))
                {
                    return AudioMediaTemplate;
                }
                else if (mediaResult.Type.Equals("game", StringComparison.OrdinalIgnoreCase))
                {
                    return GameMediaTemplate;
                }
            }
            else if (item is TLBotInlineResult result)
            {
                if (result.Type.Equals("photo", StringComparison.OrdinalIgnoreCase))
                {
                    return PhotoTemplate;
                }
                else if (result.Type.Equals("gif", StringComparison.OrdinalIgnoreCase))
                {
                    return GifTemplate;
                }
                else if (result.Type.Equals("audio", StringComparison.OrdinalIgnoreCase))
                {
                    return AudioTemplate;
                }
                else if (result.Type.Equals("voice", StringComparison.OrdinalIgnoreCase))
                {
                    return AudioTemplate;
                }
            }

            return ResultTemplate;
        }
    }
}
