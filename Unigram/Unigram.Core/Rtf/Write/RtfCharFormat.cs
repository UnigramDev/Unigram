using System;
using System.Collections.Generic;
using System.Text;

namespace Unigram.Core.Rtf.Write
{
    /// <summary>
    /// Summary description for RtfCharFormat
    /// </summary>
    public class RtfCharFormat
    {
        private int _begin;
        private int _end;
        private FontDescriptor _font;
        private FontDescriptor _ansiFont;
        private float _fontSize;
        private FontStyle _fontStyle;
        private ColorDescriptor _backgroundColor;
        private ColorDescriptor _foregroundColor;
        private TwoInOneStyle _twoInOneStyle;
        private string _bookmark;
        private string _localHyperlink;
        private string _localHyperlinkTip;


        internal RtfCharFormat(int begin, int end, int textLength)
        {
            // Note:
            // In the condition that ``_begin == _end == -1'',
            // the character formatting is applied to the whole paragraph.
            _begin = -1;
            _end = -1;
            _font = null;	// do not specify font (use default one)
            _ansiFont = null;	// do not specify font (use default one)
            _fontSize = -1;			// do not specify font size (use default one)
            _fontStyle = new FontStyle();
            _backgroundColor = null;
            _foregroundColor = null;
            _twoInOneStyle = TwoInOneStyle.NotEnabled;
            _bookmark = "";
            setRange(begin, end, textLength);
        }
        
        internal void copyFrom(RtfCharFormat src)
        {
            if (src == null) {
                return;
            }
            _begin = src._begin;
            _end = src._end;
            if (_font == null && src._font != null) {
                _font = new FontDescriptor(src._font.Value);
            }
            if (_ansiFont == null && src._ansiFont != null) {
                _ansiFont = new FontDescriptor(src._ansiFont.Value);
            }
            if (_fontSize < 0 && src._fontSize >= 0) {
                _fontSize = src._fontSize;
            }
            if (_fontStyle.IsEmpty && !src._fontStyle.IsEmpty) {
                _fontStyle = new FontStyle(src._fontStyle);
            }
            if (_backgroundColor == null && src._backgroundColor != null) {
                _backgroundColor = new ColorDescriptor(src._backgroundColor.Value);
            }
            if (_foregroundColor == null && src._foregroundColor != null) {
                _foregroundColor = new ColorDescriptor(src._foregroundColor.Value);
            }
        }

        private void setRange(int begin, int end, int textLength)
        {
            if (begin > end) {
                throw new Exception("Invalid range: (" + begin + ", " + end + ")");
            } else if (begin < 0 || end < 0) {
                if (begin != -1 || end != -1) {
                    throw new Exception("Invalid range: (" + begin + ", " + end + ")");
                }
            }
            if (end >= textLength) {
                throw new Exception("Range ending out of range: " + end);
            }
            _begin = begin;
            _end = end;
        }
        
        internal int Begin
        {
            get
            {
                return _begin;
            }
        }
        
        internal int End
        {
            get
            {
                return _end;
            }
        }

        public string Bookmark
        {
            get
            {
                return _bookmark;
            }
            set
            {
                _bookmark = value;
            }
        }

        public string LocalHyperlink
        {
            get
            {
                return _localHyperlink;
            }
            set
            {
                _localHyperlink = value;
            }
        }

        public string LocalHyperlinkTip
        {
            get
            {
                return _localHyperlinkTip;
            }
            set
            {
                _localHyperlinkTip = value;
            }
        }

        public FontDescriptor Font
        {
            get
            {
                return _font;
            }
            set
            {
                _font = value;
            }
        }
        
        public FontDescriptor AnsiFont
        {
            get
            {
                return _ansiFont;
            }
            set
            {
                _ansiFont = value;
            }
        }
        
        public float FontSize
        {
            get
            {
                return _fontSize;
            }
            set
            {
                _fontSize = value;
            }
        }
        
        public FontStyle FontStyle
        {
            get
            {
                return _fontStyle;
            }
        }
        
        public ColorDescriptor ForegroundColor
        {
            get
            {
                return _foregroundColor;
            }
            set
            {
                _foregroundColor = value;
            }
        }

        public ColorDescriptor BackgroundColor
        {
            get
            {
                return _backgroundColor;
            }
            set
            {
                _backgroundColor = value;
            }
        }
        
        public TwoInOneStyle TwoInOneStyle
        {
            get
            {
                return _twoInOneStyle;
            }
            set
            {
                _twoInOneStyle = value;
            }
        }

        internal string renderHead()
        {
            StringBuilder result = new StringBuilder("{");
            
            if (!string.IsNullOrEmpty(_localHyperlink)) {
                //result.Append(@"{\field{\*\fldinst HYPERLINK \\l ");
                result.Append(@"{\field{\*\fldinst HYPERLINK ");
                result.Append("\"" + _localHyperlink + "\"");
                if (!string.IsNullOrEmpty(_localHyperlinkTip)) result.Append(" \\\\o \"" + _localHyperlinkTip + "\"");
                result.Append(@"}{\fldrslt{");
            }


            if (_font != null || _ansiFont != null) {
                if (_font == null) {
                    result.Append(@"\f" + _ansiFont.Value);
                } else if (_ansiFont == null) {
                    result.Append(@"\f" + _font.Value);
                } else {
                    result.Append(@"\loch\af" + _ansiFont.Value + @"\hich\af" + _ansiFont.Value
                                  + @"\dbch\af" + _font.Value);
                }
            }
            if (_fontSize > 0) {
                result.Append(@"\fs" + RtfUtility.pt2HalfPt(_fontSize));
            }
            if (_foregroundColor != null) {
                result.Append(@"\cf" + _foregroundColor.Value);
            }

            // Removes underline and hacks foreground
            result.Append(@"\cf1\ulnone");

            if (_backgroundColor != null) {
                result.Append(@"\chshdng0\chcbpat" + _backgroundColor.Value + @"\cb" + _backgroundColor.Value);
            }
            
            foreach(var fontStyle in _fontStyleMap)
            {
                if (FontStyle.containsStyleAdd(fontStyle.Key)) {
                    result.Append(@"\" + fontStyle.Value);
                } else if(FontStyle.containsStyleRemove(fontStyle.Key)) {
                    result.Append(@"\" + fontStyle.Value + "0");
                }
            }
            if (_twoInOneStyle != TwoInOneStyle.NotEnabled) {
                result.Append(@"\twoinone");
                switch (_twoInOneStyle) {
                    case TwoInOneStyle.None:
                        result.Append("0");
                        break;
                    case TwoInOneStyle.Parentheses:
                        result.Append("1");
                        break;
                    case TwoInOneStyle.SquareBrackets:
                        result.Append("2");
                        break;
                    case TwoInOneStyle.AngledBrackets:
                        result.Append("3");
                        break;
                    case TwoInOneStyle.Braces:
                        result.Append("4");
                        break;
                }
            }

            if (result.ToString().Contains(@"\")) {
                result.Append(" ");
            }

            if (!string.IsNullOrEmpty(_bookmark)) {
                result.Append(@"{\*\bkmkstart " + _bookmark + "}");
            }

            return result.ToString();
        }

        internal string renderTail()
        {
            StringBuilder result = new StringBuilder("");

            if (!string.IsNullOrEmpty(_bookmark)) {
                result.Append(@"{\*\bkmkend " + _bookmark + "}");
            }
            
            if (!string.IsNullOrEmpty(_localHyperlink)) {
                result.Append(@"}}}");
            }

            result.Append("}");
            return result.ToString();
        }

        private static IDictionary<FontStyleFlag, string> _fontStyleMap = new Dictionary<FontStyleFlag, string>
        {
            {FontStyleFlag.Bold, "b"},
            {FontStyleFlag.Italic, "i"},
            {FontStyleFlag.Scaps, "scaps"},
            {FontStyleFlag.Strike, "strike"},
            {FontStyleFlag.Sub, "sub"},
            {FontStyleFlag.Super, "super"},
            {FontStyleFlag.Underline, "ul"}
        };
    }
}
