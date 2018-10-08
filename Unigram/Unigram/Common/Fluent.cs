using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml;

namespace Unigram.Common
{
    public class Fluent : ResourceDictionary
    {
        public Fluent()
        {
            if (ApiInformation.IsTypePresent("Windows.UI.Xaml.Media.AcrylicBrush"))
            {
                MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///Themes/Fluent.xaml") });
            }
            else
            {
                MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///Themes/Plain.xaml") });
            }

            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 7))
            {
                this["EllipsisButtonPadding"] = new Thickness(16, 19, 16, 0);
                this["GlyphButtonFontSize"] = 16d;
            }
            else
            {
                this["EllipsisButtonPadding"] = new Thickness(16, 23, 16, 0);
                this["GlyphButtonFontSize"] = 20d;
            }
        }
    }
}
