using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources.Core;

namespace Unigram.Helpers
{
    public class ResourceHelper
    {
        /// <summary>
        /// Get Localized strings from the resource-file
        /// </summary>
        /// <param name="resourceString"> // Put in string Resource like this --> "nameOfTheResourceString"</param>
        /// <returns>This function will retrieve and fill in the text in the proper language for the user </returns>
        public string GetString(string resourceString)
        {
            // Connect to the resource-files
            ResourceContext defaultContext = ResourceContext.GetForCurrentView();
            ResourceMap srm = ResourceManager.Current.MainResourceMap.GetSubtree("Resources");

            // Put in string Resource like this --> "nameOfTheResourceString"
            // Get back the localized string
            string text = srm.GetValue(resourceString, defaultContext).ValueAsString;
            return text;
        }
    }
}
