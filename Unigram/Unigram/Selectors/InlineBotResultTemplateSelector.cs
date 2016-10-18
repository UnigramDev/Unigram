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
        public DataTemplate AudioResultTemplate { get; set; }
        public DataTemplate ContactResultTemplate { get; set; }
        public DataTemplate GameResultTemplate { get; set; }
        public DataTemplate GeoResultTemplate { get; set; }
        public DataTemplate GifResultTemplate { get; set; }
        public DataTemplate GifTemplate { get; set; }
        public DataTemplate PhotoResultTemplate { get; set; }
        public DataTemplate PhotoTemplate { get; set; }
        public DataTemplate ResultTemplate { get; set; }
        public DataTemplate StickerResultTemplate { get; set; }
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

            var mediaResult = item as TLBotInlineMediaResult;
            if (mediaResult != null)
            {
                if (mediaResult.Type.Equals("photo", StringComparison.OrdinalIgnoreCase))
                {
                    return PhotoTemplate;
                }
                else if (mediaResult.Type.Equals("sticker", StringComparison.OrdinalIgnoreCase))
                {
                    return StickerResultTemplate;
                }
                else if (mediaResult.Type.Equals("gif", StringComparison.OrdinalIgnoreCase))
                {
                    return GifTemplate;
                }
                else if (mediaResult.Type.Equals("audio", StringComparison.OrdinalIgnoreCase))
                {
                    return AudioResultTemplate;
                }
                else if (mediaResult.Type.Equals("voice", StringComparison.OrdinalIgnoreCase))
                {
                    return AudioResultTemplate;
                }
                else if (mediaResult.Type.Equals("game", StringComparison.OrdinalIgnoreCase))
                {
                    return GameResultTemplate;
                }
            }

            var result = item as TLBotInlineResult;
            if (result != null)
            {
                if (result.Type.Equals("photo", StringComparison.OrdinalIgnoreCase))
                {
                    return PhotoResultTemplate;
                }
                else if (result.Type.Equals("gif", StringComparison.OrdinalIgnoreCase))
                {
                    return GifResultTemplate;
                }
                else if (result.Type.Equals("audio", StringComparison.OrdinalIgnoreCase))
                {
                    return AudioResultTemplate;
                }
                else if (result.Type.Equals("voice", StringComparison.OrdinalIgnoreCase))
                {
                    return AudioResultTemplate;
                }
            }

            return ResultTemplate;
        }
    }
}
