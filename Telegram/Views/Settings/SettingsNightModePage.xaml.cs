//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Linq;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Telegram.ViewModels.Settings;
using Windows.System.UserProfile;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsNightModePage : HostedPage
    {
        public SettingsNightModeViewModel ViewModel => DataContext as SettingsNightModeViewModel;

        public SettingsNightModePage()
        {
            InitializeComponent();
            Title = Strings.AutoNightTheme;

            // We have to do this as english copy contains a randomic \n at the end of the string.
            AutoNightLocation.Content = Strings.AutoNightLocation.TrimEnd('\n');

            FromPicker.ClockIdentifier = GlobalizationPreferences.Clocks.FirstOrDefault();
            ToPicker.ClockIdentifier = GlobalizationPreferences.Clocks.FirstOrDefault();

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

        private async void Switch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radio && radio.Tag is ThemeInfoBase info)
            {
                await ViewModel.SetThemeAsync(info);
            }
        }

        #region Context menu

        private void Theme_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var theme = ScrollingHost.ItemFromContainer(sender) as ThemeInfoBase;

            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(ViewModel.CreateTheme, theme, Strings.CreateNewThemeMenu, Icons.Color);

            if (theme is ThemeCustomInfo custom)
            {
                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(ViewModel.ShareTheme, custom, Strings.ShareFile, Icons.Share);
                flyout.CreateFlyoutItem(ViewModel.EditTheme, custom, Strings.Edit, Icons.Edit);
                flyout.CreateFlyoutItem(ViewModel.DeleteTheme, custom, Strings.Delete, Icons.Delete, destructive: true);
            }

            flyout.ShowAt(sender, args);
        }

        #endregion

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new ListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += Theme_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var theme = args.Item as ThemeInfoBase;
            var radio = args.ItemContainer.ContentTemplateRoot as RadioButton;

            if (args.ItemContainer.ContentTemplateRoot is StackPanel root)
            {
                radio = root.Children[0] as RadioButton;
            }

            radio.RequestedTheme = theme.Parent == TelegramTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
            radio.Click -= Switch_Click;
            radio.Click += Switch_Click;

            if (theme is ThemeCustomInfo custom)
            {
                radio.IsChecked = SettingsService.Current.Appearance[theme.Parent].Type == TelegramThemeType.Custom && string.Equals(SettingsService.Current.Appearance[theme.Parent].Custom, custom.Path, StringComparison.OrdinalIgnoreCase);
            }
            else if (theme is ThemeAccentInfo accent)
            {
                radio.IsChecked = SettingsService.Current.Appearance[theme.Parent].Type == accent.Type && SettingsService.Current.Appearance.Accents[accent.Type] == accent.AccentColor;
            }
            else
            {
                radio.IsChecked = string.IsNullOrEmpty(SettingsService.Current.Appearance[theme.Parent].Custom) && SettingsService.Current.Appearance.RequestedTheme == theme.Parent;
            }
        }

        #endregion

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
            return Formatter.ShortTime.Format(new DateTime(2020, 1, 1) + time);
        }

        private string ConvertSunDate(bool enabled, Location location)
        {
            if (location == null || (location.Latitude == 0 && location.Longitude == 0) || !enabled)
            {
                return null;
            }

            var start = DateTime.Today;
            var end = DateTime.Today;

            var t = SunDate.CalculateSunriseSunset(location.Latitude, location.Longitude);
            var sunrise = new TimeSpan(t[0] / 60, t[0] - (t[0] / 60 * 60), 0);
            var sunset = new TimeSpan(t[1] / 60, t[1] - (t[1] / 60 * 60), 0);

            start = start.Add(sunset);
            end = end.Add(sunrise);

            return string.Format(Strings.AutoNightUpdateLocationInfo,
                Formatter.ShortTime.Format(start),
                Formatter.ShortTime.Format(end));
        }

        private string ConvertBrightness(float value)
        {
            return string.Format(Strings.AutoNightBrightnessInfo, value);
        }

        #endregion

        private void UpdateTheme_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Settings.Appearance.UpdateNightMode();
        }

        private void UpdateTheme_Picked(TimePickerFlyout sender, TimePickedEventArgs args)
        {
            ViewModel.Settings.Appearance.UpdateNightMode();
        }
    }
}
