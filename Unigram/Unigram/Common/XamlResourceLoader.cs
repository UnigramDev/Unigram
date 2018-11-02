using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.UI.Xaml.Resources;

namespace Unigram.Common
{
    public class XamlResourceLoader : CustomXamlResourceLoader
    {
        private readonly ResourceLoader _loader;

        public XamlResourceLoader()
        {
            _loader = ResourceLoader.GetForViewIndependentUse("Resources");
        }

        protected override object GetResource(string resourceId, string objectType, string propertyName, string propertyType)
        {
#if DEBUG
            var text = _loader.GetString(resourceId);
            if (text.Length > 0)
            {
                return text;
            }

            return resourceId;
#else
            return _loader.GetString(resourceId);
#endif
        }
    }
}
