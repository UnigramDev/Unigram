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
