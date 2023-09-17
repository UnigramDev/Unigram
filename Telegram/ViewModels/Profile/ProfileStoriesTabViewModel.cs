using Telegram.Services;
using Telegram.ViewModels.Chats;

namespace Telegram.ViewModels.Profile
{
    public class ProfileStoriesTabViewModel : ChatStoriesViewModel
    {
        public ProfileStoriesTabViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }
    }
}
