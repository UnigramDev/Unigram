//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading.Tasks;
using Windows.UI.StartScreen;

namespace Telegram.Services
{
    public interface IContactsService
    {
        Task JumpListAsync();
    }

    public partial class ContactsService : IContactsService
    {
        private readonly IClientService _clientService;
        private readonly ISettingsService _settingsService;
        private readonly IEventAggregator _aggregator;

        public ContactsService(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
        {
            _clientService = clientService;
            _settingsService = settingsService;
            _aggregator = aggregator;
        }

        public async Task JumpListAsync()
        {
            _clientService.Send(new Td.Api.SetOption("x_user_data_account", new Td.Api.OptionValueEmpty()));
            _clientService.Send(new Td.Api.SetOption("x_contact_list", new Td.Api.OptionValueEmpty()));
            _clientService.Send(new Td.Api.SetOption("x_annotation_list", new Td.Api.OptionValueEmpty()));

            try
            {
                if (JumpList.IsSupported())
                {
                    var current = await JumpList.LoadCurrentAsync();
                    current.SystemGroupKind = JumpListSystemGroupKind.None;
                    current.Items.Clear();

                    var cloud = JumpListItem.CreateWithArguments(string.Format("from_id={0}", _clientService.Options.MyId), Strings.SavedMessages);
                    cloud.Logo = new Uri("ms-appx:///Assets/JumpList/SavedMessages/SavedMessages.png");

                    current.Items.Add(cloud);

                    await current.SaveAsync();
                }
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }
    }
}
