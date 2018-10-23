using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Controls;
using Unigram.Services;
using Unigram.Services.Settings;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System.Profile;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Host
{
    public sealed partial class StandalonePage : Page
    {
        public StandalonePage(Frame frame)
        {
            if (!SettingsService.Current.Appearance.RequestedTheme.HasFlag(TelegramTheme.Default))
            {
                RequestedTheme = SettingsService.Current.Appearance.RequestedTheme.HasFlag(TelegramTheme.Dark) ? ElementTheme.Dark : ElementTheme.Light;
            }

            InitializeComponent();

            InitializeTitleBar();

            Grid.SetRow(frame, 1);
            LayoutRoot.Children.Add(frame);
        }

        private void InitializeTitleBar()
        {
            var sender = CoreApplication.GetCurrentView().TitleBar;

            if (string.Equals(AnalyticsInfo.VersionInfo.DeviceFamily, "Windows.Desktop") && UIViewSettings.GetForCurrentView().UserInteractionMode == UserInteractionMode.Mouse)
            {
                // If running on PC and tablet mode is disabled, then titlebar is most likely visible
                // So we're going to force it
                Navigation.Padding = new Thickness(0, 32, 0, 0);
            }
            else
            {
                Navigation.Padding = new Thickness(0, sender.IsVisible ? sender.Height : 0, 0, 0);
            }

            sender.ExtendViewIntoTitleBar = true;
            sender.IsVisibleChanged += CoreTitleBar_LayoutMetricsChanged;
            sender.LayoutMetricsChanged += CoreTitleBar_LayoutMetricsChanged;
        }

        private void CoreTitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            Navigation.Padding = new Thickness(0, sender.IsVisible ? sender.Height : 0, 0, 0);

            var popups = VisualTreeHelper.GetOpenPopups(Window.Current);
            foreach (var popup in popups)
            {
                if (popup.Child is ContentDialogBase contentDialog)
                {
                    contentDialog.Padding = new Thickness(0, sender.IsVisible ? sender.Height : 0, 0, 0);
                }
            }
        }
    }
}
