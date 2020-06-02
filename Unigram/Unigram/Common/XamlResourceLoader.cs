using Unigram.Services;
using Windows.UI.Xaml.Resources;

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
