using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views
{
    public sealed partial class BlankPage : Page
    {
        public BlankPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (App.InMemoryState.ForwardMessages != null)
            {
                Overlay.Visibility = Visibility.Visible;
                EmptyLabel.Text = "Choose a recipient...";
            }
            else if (App.DataPackage != null)
            {
                Overlay.Visibility = Visibility.Visible;
                EmptyLabel.Text = "Choose a recipient...";
            }
            else
            {
                Overlay.Visibility = Visibility.Collapsed;
                EmptyLabel.Text = "Please select a chat to start messaging";
            }
        }
    }
}
