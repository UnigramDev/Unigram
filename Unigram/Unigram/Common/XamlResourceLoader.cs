using Microsoft.UI.Xaml.Resources;
using Unigram.Services;

namespace Unigram.Common
{
    public class XamlResourceLoader : CustomXamlResourceLoader
    {
        protected override object GetResource(string resourceId, string objectType, string propertyName, string propertyType)
        {
            return LocaleService.Current.GetString(resourceId);
        }
    }
}
