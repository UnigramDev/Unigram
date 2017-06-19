using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Unigram.Common
{
    /// <summary>
    /// Helper class to "convert" text emoticons to emojis
    /// </summary>
    public static class Emoticon
    {
        private static readonly Regex _replaceRegex = new Regex("(?:^|[\\s\\'\\\".])(:\\)(?!\\))|:\\-\\)(?!\\))|:\\]|=\\)(?!\\))|\\(:|\\(=|:\\(|:\\-\\(|:\\[|=\\(|\\)=|;P|;\\-P|;\\-p|;p|:poop:|:P|:\\-P|:\\-p|:p|=P|=p|=D|:\\-D|:D|:o|:\\-O|:O|:\\-o|;\\)(?!\\))|;\\-\\)(?!\\))|8\\-\\)(?!\\))|B\\-\\)(?!\\))|B\\)|8\\)|>:\\(|>:\\-\\(|:\\/|:\\-\\/|:\\\\|:\\-\\\\|=\\/|=\\\\|:\\'\\(|:\\'\\-\\(|3:\\)(?!\\))|3:\\-\\)(?!\\))|O:\\)(?!\\))|O:\\-\\)(?!\\))|0:\\)(?!\\))|0:\\-\\)(?!\\))|:\\*|:\\-\\*|;\\*|;\\-\\*|<3|&lt;3|\\u2665|\\^_\\^|\\^~\\^|\\-_\\-|:\\-\\||:\\||>:o|>:O|>:\\-O|>:\\-o|>_<|>\\.<|<\\(\\\"\\)(?!\\))|\\(y\\)(?!\\))|O_O|o_o|0_0|T_T|T\\-T|ToT|\\'\\-_\\-|\\-3\\-|:like:|\\(Y\\)(?!\\))|\\(n\\)(?!\\))|\\(N\\)(?!\\)))(?:|'|\"|\\.|,|!|\\?|$)", RegexOptions.Compiled);
        public static Regex Pattern
        {
            get
            {
                return _replaceRegex;
            }
        }

        public static string Replace(string match)
        {
            switch (match)
            {
                case ":-)":
                case ":)":
                case ":]":
                case "(:":
                    return "🙂";
                case "=)":
                case "(=":
                case "^_^":
                case "^~^":
                    return "😊";
                case ":-(":
                case ":(":
                case ":[":
                case "=(":
                case ")=":
                    return "😞";
                case ";-P":
                case ";P":
                case ";-p":
                case ";p":
                    return "😜";
                case ":-P":
                case ":P":
                case ":-p":
                case ":p":
                case "=P":
                case "=p":
                    return "😛";
                case ":-D":
                case ":D":
                    return "😀";
                case "=D":
                    return "😃";
                case ":-O":
                case ":O":
                case ":-o":
                case ":o":
                    return "😮";
                case ";-)":
                case ";)":
                    return "😉";
                case ">:(":
                case ">:-(":
                    return "😠";
                case ":/":
                case ":-/":
                case ":\\":
                case ":-\\":
                case "=/":
                case "=\\":
                    return "😕";
                case ":'(":
                    return "😢";
                case "3:)":
                case "3:-)":
                    return "😈";
                case ":-*":
                case ":*":
                    return "😗";
                case ";-*":
                case ";*":
                    return "😘";
                case "<3":
                case "&lt;3":
                    return "❤";
                case ">:O":
                case ">:-O":
                case ">:o":
                case ">:-o":
                    return "😠";
                case ">_<":
                case ">.<":
                    return "😣";
                case "<(\")":
                    return "🐧";
                case ":like:":
                case "(y)":
                case "(Y)":
                    return "👍";
                case ":poop:":
                    return "💩";
                case "(n)":
                case "(N)":
                    return "👎";
                case "-_-":
                    return "😑";
                case ":-|":
                case ":|":
                    return "😐";
                case "8-)":
                case "8)":
                case "B-)":
                case "B)":
                    return "😎";
                case "O:)":
                case "O:-)":
                case "0:)":
                case "0:-)":
                    return "😇";
                case "O_O":
                case "o_o":
                case "0_0":
                    return "😳";
                case "T_T":
                case "T-T":
                case "ToT":
                    return "😭";
                case "'-_-":
                    return "😓";
                case "-3-":
                    return "😚";
            }

            return match;
        }
    }
}
