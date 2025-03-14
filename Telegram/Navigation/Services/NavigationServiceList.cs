//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using System.Linq;

namespace Telegram.Navigation.Services
{
    public partial class NavigationServiceList : List<INavigationService>
    {
        public INavigationService GetByFrameId(string frameId) => this.FirstOrDefault(x => x.FrameFacade.FrameId == frameId);
        public INavigationService RemoveByFrameId(string frameId)
        {
            var service = GetByFrameId(frameId);
            if (service != null)
            {
                Remove(service);
                return service;
            }

            return null;
        }

        public void RemoveBySessionId(int session)
        {
            foreach (var service in this.ToList())
            {
                if (service.SessionId == session)
                {
                    Remove(service);
                }
            }
        }
    }
}
