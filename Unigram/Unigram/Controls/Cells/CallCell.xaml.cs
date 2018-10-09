using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services;
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

namespace Unigram.Controls.Cells
{
    public sealed partial class CallCell : StackPanel
    {
        private TLCallGroup _call;

        public CallCell()
        {
            InitializeComponent();
        }

        public void UpdateCall(IProtoService protoService, TLCallGroup call)
        {
            _call = call;

            DisplayLabel.Text = ConvertCount(call);
            DateLabel.Text = BindConvert.Current.DateExtended(call.Message.Date);
            TypeLabel.Text = call.DisplayType;

            Photo.Source = PlaceholderHelper.GetUser(protoService, call.Peer, 36, 36);

            VisualStateManager.GoToState(LayoutRoot, call.IsFailed ? "Missed" : "Default", false);
        }

        private string ConvertCount(TLCallGroup call)
        {
            var title = call.Peer.GetFullName();
            if (call.Items.Count > 1)
            {
                return $"{title} ({call.Items.Count})";
            }

            return title;
        }

        private void ToolTip_Opened(object sender, RoutedEventArgs e)
        {
            var tooltip = sender as ToolTip;
            if (tooltip != null && _call != null)
            {
                var date = BindConvert.Current.DateTime(_call.Message.Date);
                var text = $"{BindConvert.Current.LongDate.Format(date)} {BindConvert.Current.LongTime.Format(date)}";

                tooltip.Content = text;
            }
        }
    }
}
