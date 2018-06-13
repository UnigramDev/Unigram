using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.ViewModels.Settings;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls
{
    public sealed partial class ProxyStatusControl : UserControl
    {
        public ProxyStatusControl()
        {
            this.InitializeComponent();
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
