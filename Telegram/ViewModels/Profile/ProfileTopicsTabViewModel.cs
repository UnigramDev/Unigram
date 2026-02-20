//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Threading.Tasks;
using Telegram.Navigation.Services;
using Telegram.Services;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Profile
{
    public partial class ProfileTopicsTabViewModel : TopicListViewModel
    {
        public ProfileTopicsTabViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator, null, true, true)
        {
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is long chatId)
            {
                var chat = ClientService.GetChat(chatId);
                if (chat == null)
                {
                    return Task.CompletedTask;
                }

                SetChat(chat);
            }

            return Task.CompletedTask;
        }
    }
}
