using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Template10.Services.NavigationService;
using Unigram.Common;
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
        public StandalonePage(INavigationService navigationService)
        {
            if (SettingsService.Current.Appearance.RequestedTheme != ElementTheme.Default)
            {
                RequestedTheme = SettingsService.Current.Appearance.GetCalculatedElementTheme();
            }

            InitializeComponent();

            InitializeTitleBar();

            Grid.SetRow(navigationService.Frame, 1);
            LayoutRoot.Children.Add(navigationService.Frame);

            if (navigationService is TLNavigationService service && service.ProtoService != null)
            {
                var user = service.ProtoService.GetUser(service.ProtoService.Options.MyId);
                if (user != null)
                {
                    StatusLabel.Text = string.Format("{0} - {1}", user.GetFullName(), "Unigram");
                    ApplicationView.GetForCurrentView().Title = user.GetFullName();
                }
            }
        }

        private void InitializeTitleBar()
        {
            var sender = CoreApplication.GetCurrentView().TitleBar;

            if (string.Equals(AnalyticsInfo.VersionInfo.DeviceFamily, "Windows.Desktop") && UIViewSettings.GetForCurrentView().UserInteractionMode == UserInteractionMode.Mouse)
            {
                // If running on PC and tablet mode is disabled, then titlebar is most likely visible
                // So we're going to force it
                Navigation.Padding = new Thickness(48, 0, 0, 0);
                Navigation.Height = 32;
            }
            else
            {
                Navigation.Padding = new Thickness(sender.IsVisible ? sender.SystemOverlayLeftInset : 0, 0, 0, 0);
                Navigation.Height = sender.IsVisible ? sender.Height : 0;
            }

            sender.ExtendViewIntoTitleBar = true;
            sender.IsVisibleChanged += CoreTitleBar_LayoutMetricsChanged;
            sender.LayoutMetricsChanged += CoreTitleBar_LayoutMetricsChanged;
        }

        private void CoreTitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            Navigation.Padding = new Thickness(sender.IsVisible ? sender.SystemOverlayLeftInset : 0, 0, 0, 0);
            Navigation.Height = sender.IsVisible ? sender.Height : 0;

            var popups = VisualTreeHelper.GetOpenPopups(Window.Current);
            foreach (var popup in popups)
            {
                if (popup.Child is OverlayPage contentDialog)
                {
                    contentDialog.Padding = new Thickness(0, sender.IsVisible ? sender.Height : 0, 0, 0);
                }
            }
        }
    }
}
