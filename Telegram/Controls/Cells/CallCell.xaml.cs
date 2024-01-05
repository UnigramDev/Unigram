//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Converters;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Cells
{
    public sealed partial class CallCell : StackPanel
    {
        private TLCallGroup _call;

        public CallCell()
        {
            InitializeComponent();
        }

        public void UpdateCall(IClientService clientService, TLCallGroup call)
        {
            _call = call;

            DisplayLabel.Text = ConvertCount(call);
            DateLabel.Text = Formatter.DateExtended(call.Message.Date);
            TypeLabel.Text = call.DisplayType;

            Photo.SetUser(clientService, call.Peer, 36);

            VisualStateManager.GoToState(LayoutRoot, call.IsFailed ? "Missed" : "Default", false);
        }

        private string ConvertCount(TLCallGroup call)
        {
            var title = call.Peer.FullName();
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
                var date = Formatter.ToLocalTime(_call.Message.Date);
                var text = $"{Formatter.LongDate.Format(date)} {Formatter.LongTime.Format(date)}";

                tooltip.Content = text;
            }
        }
    }
}
