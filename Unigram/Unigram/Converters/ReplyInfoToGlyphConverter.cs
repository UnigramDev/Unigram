using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;
using Telegram.Api.TL;

namespace Unigram.Converters
{
    public class ReplyInfoToGlyphConverter : IValueConverter
    {
        public const string EditGlyph = "\uE104";
        public const string ReplyGlyph = "\uE248";
        public const string GlobeGlyph = "\uE12B";
        public const string ForwardGlyph = "\uE111";
        public const string LoadingGlyph = "\uE1CD";

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
            {
                return null;
            }

            var replyInfo = value as ReplyInfo;
            if (replyInfo == null)
            {
                return ReplyGlyph;
            }
            else
            {
                if (replyInfo.Reply == null)
                {
                    return LoadingGlyph;
                }

                var container = replyInfo.Reply as TLMessagesContainter;
                if (container != null)
                {
                    return GetMessagesContainerTemplate(container);
                }

                if (replyInfo.ReplyToMsgId == null || replyInfo.ReplyToMsgId.Value == 0)
                {
                    return ReplyGlyph;
                }

                return ReplyGlyph;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }

        private string GetMessagesContainerTemplate(TLMessagesContainter container)
        {
            if (container.WebPageMedia != null)
            {
                var webpageMedia = container.WebPageMedia as TLMessageMediaWebPage;
                if (webpageMedia != null)
                {
                    return GlobeGlyph;
                }
            }

            if (container.FwdMessages != null)
            {
                return ForwardGlyph;
            }

            if (container.EditMessage != null)
            {
                return EditGlyph;
            }

            return ReplyGlyph;
        }
    }
}
