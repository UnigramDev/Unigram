//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Unigram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public sealed partial class ProxyStatusControl : UserControl
    {
        public ProxyStatusControl()
        {
            InitializeComponent();
        }

        public ConnectionStatus Status
        {
            set => UpdateView(value);
        }

        private void UpdateView(ConnectionStatus value)
        {
            if (value is ConnectionStatusChecking)
            {
                UpdateTextAndState(Strings.Resources.Checking, nameof(Connected));
            }
            else if (value is ConnectionStatusConnecting)
            {
                UpdateTextAndState(Strings.Resources.Connecting, nameof(Connected));
            }
            else if (value is ConnectionStatusReady ready)
            {
                if (ready.IsConnected)
                {
                    if (ready.Seconds != 0)
                    {
                        UpdateTextAndState(string.Format(Strings.Resources.Connected + ", " + Strings.Resources.Ping, Math.Truncate(ready.Seconds * 1000)), nameof(Connected));
                    }
                    else
                    {
                        UpdateTextAndState(Strings.Resources.Connected, nameof(Connected));
                    }
                }
                else
                {
                    if (ready.Seconds != 0)
                    {
                        UpdateTextAndState(string.Format(Strings.Resources.Available + ", " + Strings.Resources.Ping, Math.Truncate(ready.Seconds * 1000)), nameof(Available));
                    }
                    else
                    {
                        UpdateTextAndState(Strings.Resources.Available, nameof(Available));
                    }
                }
            }
            else
            {
                UpdateTextAndState(Strings.Resources.Unavailable, nameof(Unavailable));
            }
        }

        private void UpdateTextAndState(string text, string state)
        {
            VisualStateManager.GoToState(this, state, false);

            Label.Text = text;
        }
    }
}
