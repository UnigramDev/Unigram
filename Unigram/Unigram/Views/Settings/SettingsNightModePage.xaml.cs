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
using Windows.Devices.Sensors;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
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

            if (ApiInformation.IsEnumNamedValuePresent("Windows.UI.Xaml.Controls.Primitives.FlyoutPlacementMode", "BottomEdgeAlignedLeft"))
            {
                FromPicker.Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft;
                ToPicker.Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft;
            }

            //var sensor = LightSensor.GetDefault();
            //if (sensor != null)
            //{
            //    Automatic.Visibility = Visibility.Visible;
            //    AutomaticSeparator.Visibility = Visibility.Visible;

            //    sensor.ReportInterval = 50000;
            //    sensor.ReadingChanged += LightSensor_ReadingChanged;
            //}
            //else
            //{
            //    Automatic.Visibility = Visibility.Collapsed;
            //    AutomaticSeparator.Visibility = Visibility.Collapsed;
            //}
        }

        //private const float MAXIMUM_LUX_BREAKPOINT = 500.0f;

        //private void LightSensor_ReadingChanged(LightSensor sender, LightSensorReadingChangedEventArgs args)
        //{
        //    var lux = args.Reading.IlluminanceInLux;
        //    if (lux <= 0)
        //    {
        //        lux = 0.1f;
        //    }

        //    var last = lux;
        //    if (lux > MAXIMUM_LUX_BREAKPOINT)
        //    {
        //        last = 1.0f;
        //    }
        //    else
        //    {
        //        last = (float)Math.Ceiling(9.9323f * Math.Log(lux) + 27.059f) / 100.0f;
        //    }

        //    this.BeginOnUIThread(() =>
        //    {
        //        Lux.Value = last * 100;
        //    });
        //}

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

        private string ConvertSunDate(bool enabled, BasicGeoposition location)
        {
            if (location.Latitude == 0 && location.Longitude == 0 || !enabled)
            {
                return null;
            }

            var t = SunDate.CalculateSunriseSunset(location.Latitude, location.Longitude);
            var sunrise = new DateTime(1, 1, 1, t[0] / 60, t[0] - (t[0] / 60) * 60, 0);
            var sunset = new DateTime(1, 1, 1, t[1] / 60, t[1] - (t[1] / 60) * 60, 0);

            return string.Format(Strings.Resources.AutoNightUpdateLocationInfo, BindConvert.Current.ShortTime.Format(sunset), BindConvert.Current.ShortTime.Format(sunrise));
        }

        private string ConvertBrightness(float value)
        {
            return string.Format(Strings.Resources.AutoNightBrightnessInfo, value);
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
