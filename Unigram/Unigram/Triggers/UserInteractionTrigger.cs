using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace Unigram.Triggers
{
    public class UserInteractionTrigger : StateTriggerBase
    {
        public UserInteractionTrigger()
        {
            Window.Current.SizeChanged += SizeChanged;
        }

        private UserInteractionMode _mode;
        public UserInteractionMode Mode
        {
            get
            {
                return _mode;
            }
            set
            {
                _mode = value;
            }
        }

        private async void SizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                SetActive(UIViewSettings.GetForCurrentView().UserInteractionMode == _mode);
            });
        }
    }
}
