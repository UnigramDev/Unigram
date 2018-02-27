using System;
using System.IO;
using System.Linq;
using Telegram.Api.TL;
using Windows.Foundation;

namespace Telegram.Api.TL
{
    public static class TLObjectExtensions
    {
        public static string Substr(this string source, int startIndex, int endIndex)
        {
            return source.Substring(startIndex, endIndex - startIndex);
        }
    }
}
