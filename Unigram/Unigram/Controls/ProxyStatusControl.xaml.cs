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

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls
{
    public sealed partial class ProxyStatusControl : UserControl
    {
        public ProxyStatusControl()
        {
            this.InitializeComponent();
        }

        public ProxyStatus Status
        {
            set => UpdateView(value);
        }

        private void UpdateView(ProxyStatus value)
        {
            if (value == null)
            {
                Label.Text = Strings.Resources.Checking;
            }
            else if (value.Error == null)
            {
                Label.Text = string.Format(Strings.Resources.Ping, value.Seconds * 1000);
            }
            else
            {
                Label.Text = Strings.Resources.Unavailable;
            }
        }
    }
}
