using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Services;
using Windows.UI.Xaml.Resources;

namespace Unigram.Common
{
    public class XamlResourceLoader : CustomXamlResourceLoader
    {
        protected override object GetResource(string resourceId, string objectType, string propertyName, string propertyType)
        {
            if (resourceId.StartsWith("Additional."))
                return Strings.Additional.Resource.GetString(resourceId.Substring(11));
            return LocaleService.Current.GetString(resourceId);
        }
    }
}
