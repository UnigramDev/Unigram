using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Unigram.Services.Settings
{
    public class FiltersSettings : SettingsServiceBase
    {
        public FiltersSettings(ApplicationDataContainer container = null)
            : base(container)
        {
        }
    }
}
