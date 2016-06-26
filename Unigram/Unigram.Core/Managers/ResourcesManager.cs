using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;

namespace Unigram.Core.Managers
{
    /// <summary>
    /// This class let you retrieve a value from the current Resources.resw by passing a key to the method GetString
    /// </summary>
    public class ResourcesManager
    {

        public static string GetString(string name)
        {

            ResourceLoader resourceLoader = null;
            try
            {
                resourceLoader = ResourceLoader.GetForViewIndependentUse();
            }
            catch
            {
                resourceLoader = ResourceLoader.GetForCurrentView();
            }
            if (resourceLoader != null)
            {
                name = name.Replace('.', '/');
                return resourceLoader.GetString(name);
            }
            return null;
        }
    }
}
