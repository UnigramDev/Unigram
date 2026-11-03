//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Services;
using Telegram.ViewModels.Settings;
using Windows.ApplicationModel;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

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

            if (ApiInfo.HasPackageIdentity && ApiInformation.IsApiContractPresent("Windows.ApplicationModel.StartupTaskContract", 2))
            {
                integrated = true;

#if WINUI3
                FindName(nameof(ToggleMinimized));
#endif

                OnLoaded();
            }
#if UNPACKAGED
            else if (!ApiInfo.HasPackageIdentity)
            {
                integrated = true;
                FindName(nameof(ToggleMinimized));
                OnLoaded();
            }
#endif
            else
            {
                // A StartupTask is declared in the manifest and this device has none, so the toggle
                // is hidden rather than left as a switch that does nothing.
                Toggle.Visibility = Visibility.Collapsed;
            }

            if (TraySwitch != null && ToggleMinimized != null)
            {
                TraySwitch.Checked += Tray_Toggled;
                TraySwitch.Unchecked += Tray_Toggled;
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

            var state = await GetStateAsync();
            if (state is null or StartupTaskState.DisabledByUser or StartupTaskState.DisabledByPolicy)
            {
                Toggle.IsChecked = false;
                Toggle.IsEnabled = false;

                if (ToggleMinimized != null)
                {
                    ToggleMinimized.IsChecked = false;
                    ToggleMinimized.Visibility = Visibility.Collapsed;
                }

                Headered.Footer = state == StartupTaskState.DisabledByPolicy
                    ? Strings.AutoStartDisabledByPolicyInfo
                    : Strings.AutoStartDisabledInfo;

                Visibility = Visibility.Visible;
            }
            else if (state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy)
            {
                Toggle.IsChecked = true;
                Toggle.IsEnabled = state == StartupTaskState.Enabled;

                if (ToggleMinimized != null)
                {
                    ToggleMinimized.IsChecked = AppSettings.IsLaunchMinimized;
                    ToggleMinimized.Visibility = AppSettings.IsTrayVisible
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }

                Headered.Footer = state == StartupTaskState.EnabledByPolicy
                    ? Strings.AutoStartEnabledByPolicyInfo
                    : string.Empty;

                Visibility = Visibility.Visible;
            }
            else if (state == StartupTaskState.Disabled)
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
                // An unknown future state: TraySwitch shares this control, so disable rather than collapse.
                Toggle.IsChecked = false;
                Toggle.IsEnabled = false;

                if (ToggleMinimized != null)
                {
                    ToggleMinimized.IsChecked = false;
                    ToggleMinimized.Visibility = Visibility.Collapsed;
                }

                Headered.Footer = string.Empty;
            }

            Toggle.Checked += OnToggled;
            Toggle.Unchecked += OnToggled;

            if (ToggleMinimized != null)
            {
                ToggleMinimized.Checked += Minimized_Toggled;
                ToggleMinimized.Unchecked += Minimized_Toggled;
            }
        }

        // Launch minimized starts the app behind the notification area icon, so without the icon
        // it has nothing to do. Read from the checkbox: the setting may not be written back yet.
        private void Tray_Toggled(object sender, RoutedEventArgs e)
        {
            ToggleMinimized.Visibility = Toggle.IsChecked is true && TraySwitch.IsChecked is true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private async void OnToggled(object sender, RoutedEventArgs e)
        {
            await SetEnabledAsync(Toggle.IsChecked is true);
            OnLoaded();
        }

        private async Task<StartupTaskState?> GetStateAsync()
        {
#if UNPACKAGED
            if (!ApiInfo.HasPackageIdentity)
            {
                return UnpackagedStartup.GetState();
            }
#endif

            var task = await GetTaskAsync();
            return task?.State;
        }

        private async Task SetEnabledAsync(bool enabled)
        {
#if UNPACKAGED
            if (!ApiInfo.HasPackageIdentity)
            {
                UnpackagedStartup.SetEnabled(enabled);
                return;
            }
#endif

            var task = await GetTaskAsync();
            if (task == null)
            {
                return;
            }

            if (enabled)
            {
                await task.RequestEnableAsync();
            }
            else
            {
                task.Disable();
            }
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
            AppSettings.IsLaunchMinimized = ToggleMinimized.IsChecked is true;
        }
    }
}
