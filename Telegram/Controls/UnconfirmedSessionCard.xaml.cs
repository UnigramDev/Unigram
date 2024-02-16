//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public sealed partial class UnconfirmedSessionCard : UserControl
    {
        public UnconfirmedSessionCard()
        {
            InitializeComponent();
        }

        public void Update(UnconfirmedSession session)
        {
            // TODO: multiple sessions?
            Message.Text = string.Format(Strings.UnconfirmedAuthSingle, string.Format("{0}, {1}", session.DeviceModel, session.Location));
        }

        public event RoutedEventHandler ConfirmClick
        {
            add => Confirm.Click += value;
            remove => Confirm.Click -= value;
        }

        public event RoutedEventHandler DenyClick
        {
            add => Deny.Click += value;
            remove => Deny.Click -= value;
        }
    }
}
