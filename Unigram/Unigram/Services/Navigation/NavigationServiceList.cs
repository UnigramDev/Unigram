using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace Unigram.Services.Navigation
{
    public class NavigationServiceList : List<INavigationService>
    {
        public INavigationService GetByFrameId(string frameId) => this.FirstOrDefault(x => x.FrameFacade.FrameId == frameId);
        public INavigationService RemoveByFrameId(string frameId)
        {
            var service = GetByFrameId(frameId);
            if (service != null)
            {
                this.Remove(service);
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
                    this.Remove(service);
                }
            }
        }
    }
}
