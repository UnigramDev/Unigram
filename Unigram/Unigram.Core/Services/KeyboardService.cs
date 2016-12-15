using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace Unigram.Core.Services
{
    public interface IKeyboardService
    {
        Rect LastKnownOccludedRect { get;}
    }

    public class KeyboardService : IKeyboardService
    {
        public KeyboardService()
        {
            InputPane.GetForCurrentView().Showing += InputPane_Showing;
        }

        private void InputPane_Showing(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            _lastKnownOccludedRect = args.OccludedRect;
        }

        private Rect _lastKnownOccludedRect;
        public Rect LastKnownOccludedRect
        {
            get
            {
                if (_lastKnownOccludedRect == null)
                    _lastKnownOccludedRect = new Rect(0, 0, Window.Current.Bounds.Width, 320);

                return _lastKnownOccludedRect;
            }
        }
    }
}
