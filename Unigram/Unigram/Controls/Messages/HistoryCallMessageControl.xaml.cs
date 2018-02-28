using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Strings;
using Unigram.ViewModels;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls.Messages
{
    public sealed partial class HistoryCallMessageControl : HistoryCallMessageControlBase
    {
        public HistoryCallMessageControl()
        {
            InitializeComponent();
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (ViewModel != null && ViewModel != _oldValue) Bindings.Update();
            if (ViewModel == null) Bindings.StopTracking();

            _oldValue = ViewModel;
        }

        private string ConvertCount(TLCallGroup call, int count)
        {
            VisualStateManager.GoToState(LayoutRoot, call.IsFailed ? "Missed" : "Default", false);

            var title = call.Peer.GetFullName();
            if (count > 1)
            {
                return $"{title} ({count})";
            }

            return title;
        }
    }
}
