using System;
using System.Text;

namespace Unigram.Core.Rtf.Write
{
    /// <summary>
    /// Summary description for RtfUtility
    /// </summary>
    public static class RtfUtility
    {
        public static float mm2Points( float mm )
        {
            return mm * (float) 2.836;
        }

        public static int mm2Twips( float mm )
        {
            var inches = mm * 0.0393700787;
            return Convert.ToInt32( inches * 1440 );
        }

        public static int pt2Twip(float pt)
        {
            return !float.IsNaN( pt ) ? Convert.ToInt32( pt * 20 ) : 0;
        }

        public static int pt2HalfPt(float pt)
        {
            return Convert.ToInt32(pt * 2);
        }
        
        private static int[] paperDimensions(PaperSize paperSize)
        {
            switch(paperSize) {
                case PaperSize.A4:
                    return new int[] { 11906, 16838 };
                case PaperSize.Letter:
                    return new int[] { 15840, 12240 };
                case PaperSize.A3:
                    return new int[] { 16838, 23811 };
                default:
                    throw new Exception("Unknow paper size.");
            }
        }
        
        public static int paperWidthInTwip(PaperSize paperSize, PaperOrientation orientation)
        {
            int[] d = paperDimensions(paperSize);
            if (orientation == PaperOrientation.Portrait) {
                if (d[0] < d[1]) {
                    return d[0];
                } else {
                    return d[1];
                }
            } else { // landscape
                if (d[0] < d[1]) {
                    return d[1];
                } else {
                    return d[0];
                }
            }
        }
        
        public static int paperHeightInTwip(PaperSize paperSize, PaperOrientation orientation)
        {
            int[] d = paperDimensions(paperSize);
            if (orientation == PaperOrientation.Portrait) {
                if (d[0] < d[1]) {
                    return d[1];
                } else {
                    return d[0];
                }
            } else { // landscape
                if (d[0] < d[1]) {
                    return d[0];
                } else {
                    return d[1];
                }
            }
        }

        public static float paperWidthInPt(PaperSize paperSize, PaperOrientation orientation)
        {
            return (float) paperWidthInTwip(paperSize, orientation) / 20.0F;
        }

        public static float paperHeightInPt(PaperSize paperSize, PaperOrientation orientation)
        {
            return (float)paperHeightInTwip(paperSize, orientation) / 20.0F;
        }

        public static string unicodeEncode(string str)
        {
            StringBuilder result = new StringBuilder();
            int unicode;

            for (int i = 0; i < str.Length; i++) {
                unicode = (int)str[i];
                if (str[i] == '\n') {
                    result.AppendLine(@"\line");
                } else if (str[i] == '\r') {
                    // ignore '\r'
                } else if (str[i] == '\t') {
                    result.Append(@"\tab ");
                } else if (unicode <= 0xff) {
                    if (unicode == 0x5c || unicode == 0x7b || unicode == 0x7d) {
                        result.Append(@"\'" + string.Format("{0:x2}", unicode));
                    } else if (0x00 <= unicode && unicode < 0x20) {
                        result.Append(@"\'" + string.Format("{0:x2}", unicode));
                    } else if (0x20 <= unicode && unicode < 0x80) {
                        result.Append(str[i]);
                    } else { // 0x80 <= unicode <= 0xff
                        result.Append(@"\'" + string.Format("{0:x2}", unicode));
                    }
                } else if (0xff < unicode && unicode <= 0x8000) {
                    result.Append(@"\uc1\u" + unicode + "*");
                } else if (0x8000 < unicode && unicode <= 0xffff) {
                    result.Append(@"\uc1\u" + (unicode - 0x10000) + "*");
                } else {
                    result.Append(@"\uc1\u9633*");
                }
            }
            return result.ToString();
        }

        /// <summary>
        /// big5 encoding (preserve this function for failure restoration)
        /// </summary>
        /// <param name="str">string to be encoded</param>
        /// <returns>encoded string</returns>
        public static string big5Encode(string str)
        {
            string result = "";
            Encoding big5 = Encoding.GetEncoding(950);
            Encoding ascii = Encoding.ASCII;
            Byte[] buf = big5.GetBytes(str);
            Byte c;

            for (int i = 0; i < buf.Length; i++) {
                c = buf[i];
                if ((0x00 <= c && c < 0x20) || (0x80 <= c && c <= 0xff)
                    || c == 0x5c || c == 0x7b || c == 0x7d) {
                    result += string.Format(@"\'{0:x2}", c);
                } else {
                    result += ascii.GetChars(new byte[] { c })[0];
                }
            }
            return result;
        }
    }
}
