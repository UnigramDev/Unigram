using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class InitialNameStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string s = value.ToString();
            string initialText = string.Empty;
            if (s.Length == 1)
            {
                initialText = s;
            }
            else if (s.Length > 1)
            {
                if (s.Split(' ').Length < 2)
                {
                    initialText += s.Substring(0, 2).ToUpper();
                }
                else
                {
                    for (int i = 0; i < s.Split(' ').Length && initialText.Length < 2; i++)
                    {
                        initialText += s.Split(' ')[i][0];
                    }
                }
            }

            return initialText;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException();
        }
    }
}
