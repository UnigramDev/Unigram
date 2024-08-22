//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.ViewModels.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Telegram.Controls.Cells
{
    public sealed partial class ProxyCell : Grid
    {
        public ProxyCell()
        {
            InitializeComponent();
        }

        public string DisplayName
        {
            set => DisplayNameLabel.Text = value;
        }

        public bool IsEnabled
        {
            set => EnabledLabel.Visibility = value
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public ConnectionStatus Status
        {
            set => UpdateView(value);
        }

        private void UpdateView(ConnectionStatus value)
        {
            if (value is ConnectionStatusChecking)
            {
                UpdateTextAndState(Strings.Checking, nameof(Connected));
            }
            else if (value is ConnectionStatusConnecting)
            {
                UpdateTextAndState(Strings.Connecting, nameof(Connected));
            }
            else if (value is ConnectionStatusReady ready)
            {
                if (ready.IsConnected)
                {
                    if (ready.Seconds != 0)
                    {
                        UpdateTextAndState(string.Format(Strings.Connected + ", " + Strings.Ping, Math.Truncate(ready.Seconds * 1000)), nameof(Connected));
                    }
                    else
                    {
                        UpdateTextAndState(Strings.Connected, nameof(Connected));
                    }
                }
                else
                {
                    if (ready.Seconds != 0)
                    {
                        UpdateTextAndState(string.Format(Strings.Available + ", " + Strings.Ping, Math.Truncate(ready.Seconds * 1000)), nameof(Available));
                    }
                    else
                    {
                        UpdateTextAndState(Strings.Available, nameof(Available));
                    }
                }
            }
            else
            {
                UpdateTextAndState(Strings.Unavailable, nameof(Unavailable));
            }
        }

        private void UpdateTextAndState(string text, string state)
        {
            VisualStateManager.GoToState(LayoutRoot, state, false);
            StatusLabel.Text = text;
        }
    }
}
