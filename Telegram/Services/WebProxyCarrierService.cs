//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Td.Api;

namespace Telegram.Services
{
    public interface IWebProxyCarrierService
    {
    }

    // Each TDLib instance requests its own carriers; they are all hosted by the one WebProxyCarrierHost.
    public partial class WebProxyCarrierService : ServiceBase, IWebProxyCarrierService
    {
        public WebProxyCarrierService(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Aggregator.Subscribe<UpdateWebProxyCarrierOpened>(this, Handle)
                .Subscribe<UpdateWebProxyCarrierData>(Handle)
                .Subscribe<UpdateWebProxyCarrierClosed>(Handle);
        }

        public void Handle(UpdateWebProxyCarrierOpened update)
        {
            WebProxyCarrierHost.Current.Open(ClientService, update.Carrier);
        }

        public void Handle(UpdateWebProxyCarrierData update)
        {
            WebProxyCarrierHost.Current.Post(ClientService, update.CarrierId, update.Data);
        }

        public void Handle(UpdateWebProxyCarrierClosed update)
        {
            WebProxyCarrierHost.Current.Close(ClientService, update.CarrierId);
        }
    }
}
