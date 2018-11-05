using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services.Settings;
using Unigram.ViewModels.Settings;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System.UserProfile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsNightModePage : Page
    {
        public SettingsNightModeViewModel ViewModel => DataContext as SettingsNightModeViewModel;

        public SettingsNightModePage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsNightModeViewModel>();

            // We have to do this as english copy contains a randomic \n at the end of the string.
            AutoNightLocation.Content = Strings.Resources.AutoNightLocation.TrimEnd('\n');

            FromPicker.ClockIdentifier = GlobalizationPreferences.Clocks.FirstOrDefault();
            ToPicker.ClockIdentifier = GlobalizationPreferences.Clocks.FirstOrDefault();
        }

        #region Binding

        private int ConvertMode(NightMode mode)
        {
            return (int)mode;
        }

        private void ConvertModeBack(int mode)
        {
            ViewModel.Mode = (NightMode)mode;
        }

        private Visibility ConvertModeVisibility(NightMode current, NightMode expected)
        {
            return current == expected ? Visibility.Visible : Visibility.Collapsed;
        }

        private string ConvertTimeSpan(TimeSpan time)
        {
            return BindConvert.Current.ShortTime.Format(new DateTime(1, 1, 1, time.Hours, time.Minutes, 0));
        }

        private string ConvertSunDate(BasicGeoposition location)
        {
            if (location.Latitude == 0 && location.Longitude == 0)
            {
                return null;
            }

            var t = SunDate.CalculateSunriseSunset(location.Latitude, location.Longitude);
            var sunrise = new DateTime(1, 1, 1, t[0] / 60, t[0] - (t[0] / 60) * 60, 0);
            var sunset = new DateTime(1, 1, 1, t[1] / 60, t[1] - (t[1] / 60) * 60, 0);

            return string.Format(Strings.Resources.AutoNightUpdateLocationInfo, BindConvert.Current.ShortTime.Format(sunset), BindConvert.Current.ShortTime.Format(sunrise));
        }

        #endregion

        private void UpdateTheme_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.UpdateTheme();
        }

        private void UpdateTheme_Picked(TimePickerFlyout sender, TimePickedEventArgs args)
        {
            ViewModel.UpdateTheme();
        }
    }
}
