using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
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
    public sealed partial class StartupSwitch : UserControl
    {
        public StartupSwitch()
        {
            InitializeComponent();

            if (ApiInformation.IsApiContractPresent("Windows.ApplicationModel.StartupTaskContract", 2))
            {
                OnLoaded();
            }
            else
            {
                Visibility = Visibility.Collapsed;
            }
        }

        private async void OnLoaded()
        {
            Toggle.Toggled -= OnToggled;

            var task = await StartupTask.GetAsync("Telegram");
            if (task.State == StartupTaskState.Enabled)
            {
                Toggle.IsOn = true;
                Toggle.IsEnabled = true;
                Label.Visibility = Visibility.Collapsed;

                Visibility = Visibility.Visible;
            }
            else if (task.State == StartupTaskState.Disabled)
            {
                Toggle.IsOn = false;
                Toggle.IsEnabled = true;
                Label.Visibility = Visibility.Collapsed;

                Visibility = Visibility.Visible;
            }
            else if (task.State == StartupTaskState.DisabledByUser)
            {
                Toggle.IsOn = false;
                Toggle.IsEnabled = false;
                Label.Visibility = Visibility.Visible;

                Visibility = Visibility.Visible;
            }
            else
            {
                Visibility = Visibility.Collapsed;
            }

            Toggle.Toggled += OnToggled;
        }

        private async void OnToggled(object sender, RoutedEventArgs e)
        {
            var task = await StartupTask.GetAsync("Telegram");
            if (Toggle.IsOn)
            {
                await task.RequestEnableAsync();
            }
            else
            {
                task.Disable();
            }

            OnLoaded();
        }
    }
}
