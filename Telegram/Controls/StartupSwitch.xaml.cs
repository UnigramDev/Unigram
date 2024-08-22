//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading.Tasks;
using Telegram.Services;
using Telegram.ViewModels.Settings;
using Windows.ApplicationModel;
using Windows.Foundation.Metadata;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public sealed partial class StartupSwitch : UserControl
    {
        public SettingsAdvancedViewModel ViewModel => DataContext as SettingsAdvancedViewModel;

        public StartupSwitch()
        {
            InitializeComponent();

            var integrated = false;

            if (ApiInformation.IsTypePresent("Windows.ApplicationModel.FullTrustProcessLauncher"))
            {
                integrated = true;
                FindName(nameof(TraySwitch));
            }

            if (ApiInformation.IsApiContractPresent("Windows.ApplicationModel.StartupTaskContract", 2))
            {
                integrated = true;

#if DESKTOP_BRIDGE
                FindName(nameof(ToggleMinimized));
#endif

                OnLoaded();
            }

            Visibility = integrated
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private async void OnLoaded()
        {
            Toggle.Checked -= OnToggled;
            Toggle.Unchecked -= OnToggled;

            if (ToggleMinimized != null)
            {
                ToggleMinimized.Checked -= Minimized_Toggled;
                ToggleMinimized.Unchecked -= Minimized_Toggled;
            }

            var task = await GetTaskAsync();
            if (task == null || task.State == StartupTaskState.DisabledByUser)
            {
                Toggle.IsChecked = false;
                Toggle.IsEnabled = false;

                if (ToggleMinimized != null)
                {
                    ToggleMinimized.IsChecked = false;
                    ToggleMinimized.Visibility = Visibility.Collapsed;
                }

                Headered.Footer = Strings.AutoStartDisabledInfo;
                Visibility = Visibility.Visible;
            }
            else if (task.State == StartupTaskState.Enabled)
            {
                Toggle.IsChecked = true;
                Toggle.IsEnabled = true;

                if (ToggleMinimized != null)
                {
                    ToggleMinimized.IsChecked = SettingsService.Current.IsLaunchMinimized;
                    ToggleMinimized.Visibility = Visibility.Visible;
                }

                Headered.Footer = string.Empty;

                Visibility = Visibility.Visible;
            }
            else if (task.State == StartupTaskState.Disabled)
            {
                Toggle.IsChecked = false;
                Toggle.IsEnabled = true;

                if (ToggleMinimized != null)
                {
                    ToggleMinimized.IsChecked = false;
                    ToggleMinimized.Visibility = Visibility.Collapsed;
                }

                Headered.Footer = string.Empty;

                Visibility = Visibility.Visible;
            }
            else
            {
                Visibility = Visibility.Collapsed;
            }

            Toggle.Checked += OnToggled;
            Toggle.Unchecked += OnToggled;

            if (ToggleMinimized != null)
            {
                ToggleMinimized.Checked += Minimized_Toggled;
                ToggleMinimized.Unchecked += Minimized_Toggled;
            }
        }

        private async void OnToggled(object sender, RoutedEventArgs e)
        {
            var task = await GetTaskAsync();
            if (task == null)
            {
                return;
            }

            if (Toggle.IsChecked is true)
            {
                await task.RequestEnableAsync();
            }
            else
            {
                task.Disable();
            }

            OnLoaded();
        }

        private async Task<StartupTask> GetTaskAsync()
        {
            try
            {
                return await StartupTask.GetAsync("Telegram");
            }
            catch
            {
                return null;
            }
        }

        private void Minimized_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsService.Current.IsLaunchMinimized = ToggleMinimized.IsChecked is true;
        }
    }
}
