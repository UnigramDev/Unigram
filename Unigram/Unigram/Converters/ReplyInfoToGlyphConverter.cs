using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;
using Unigram.ViewModels;

namespace Unigram.Converters
{
    public class ReplyInfoToGlyphConverter : IValueConverter
    {
        public const string EditGlyph = "\uE104";
        public const string ReplyGlyph = "\uE248";
        public const string GlobeGlyph = "\uE12B";
        public const string ForwardGlyph = "\uE111";
        public const string LoadingGlyph = "\uE1CD";
        public const string SendGlyph = "\uE725";
        public const string ConfirmGlyph = "\uE10B";

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
            {
                if (parameter != null)
                {
                    return SendGlyph;
                }

                return null;
            }

            if (parameter != null)
            {
                var replyInfo = value as MessageEmbedData;
                if (replyInfo != null)
                {
                    return replyInfo.EditingMessage != null ? ConfirmGlyph : SendGlyph;
                }

                return SendGlyph;
            }
            else
            {
                var replyInfo = value as MessageEmbedData;
                if (replyInfo == null)
                {
                    return ReplyGlyph;
                }
                else
                {
                    if (replyInfo.WebPagePreview != null)
                    {
                        return GlobeGlyph;
                    }
                    else if (replyInfo.EditingMessage != null)
                    {
                        return EditGlyph;
                    }
                    else if (replyInfo.ReplyToMessage != null)
                    {
                        return ReplyGlyph;
                    }

                    return LoadingGlyph;
                }
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
