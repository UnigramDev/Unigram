using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unigram.Common
{
    public static class Language
    {
        public static string Declension(int number, string nominativeSingular, string nominativePlural, string genitiveSingular, string genitivePlural, string enException = null, string format = null)
        {
            if (!string.IsNullOrEmpty(enException))
            {
                return enException;
            }

            return EnDeclension(number, nominativeSingular, nominativePlural, format);
        }

        public static string EnDeclension(int number, string singular, string plural, string format)
        {
            return string.Format("{2}{0}{2} {1}", number, (number == 1 || number == 0) ? singular : plural, format);
        }
    }
}
