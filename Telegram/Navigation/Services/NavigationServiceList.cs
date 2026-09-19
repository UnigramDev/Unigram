//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Telegram.Navigation.Services
{
    public partial class NavigationServiceList : IReadOnlyList<INavigationService>
    {
        private readonly List<INavigationService> _items = new();

        public INavigationService this[int index] => _items[index];

        public int Count => _items.Count;

        public void Add(INavigationService navigationService)
        {
            _items.Add(navigationService);
            navigationService.Connect();
        }

        public void Remove(INavigationService navigationService)
        {
            _items.Remove(navigationService);
            navigationService?.Disconnect();
        }

        public void Clear()
        {
            foreach (var service in _items)
            {
                service.Disconnect();
            }

            _items.Clear();
        }

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

        public IEnumerator<INavigationService> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
