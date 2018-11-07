using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;
using Unigram.ViewModels;
using Telegram.Td.Api;

namespace Unigram.Converters
{
    public class ReplyInfoToGlyphConverter
    {
        public const string EditGlyph = "\uE104";
        public const string ReplyGlyph = "\uE248";
        public const string GlobeGlyph = "\uE12B";
        public const string ForwardGlyph = "\uE111";
        public const string LoadingGlyph = "\uE1CD";
        public const string SendGlyph = "\uE725";
        public const string ConfirmGlyph = "\uE10B";

        public const string AttachGlyph = "\uE917";
        public const string AttachEditGlyph = "\uE918";
    }
}
