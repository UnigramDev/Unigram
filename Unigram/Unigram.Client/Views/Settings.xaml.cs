namespace Unigram.Client.Views
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices.WindowsRuntime;
    using Windows.Foundation;
    using Windows.Foundation.Collections;
    using Windows.UI.Core;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Controls.Primitives;
    using Windows.UI.Xaml.Data;
    using Windows.UI.Xaml.Input;
    using Windows.UI.Xaml.Media;
    using Windows.UI.Xaml.Navigation;

    public sealed partial class Settings : Page
    {
        public Settings()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            SystemNavigationManager systemNavigatonManager = SystemNavigationManager.GetForCurrentView();
            if (this.Frame.CanGoBack)
            {
                systemNavigatonManager.AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
                systemNavigatonManager.BackRequested += SystemNavigatonManager_BackRequested;
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            SystemNavigationManager systemNavigatonManager = SystemNavigationManager.GetForCurrentView();
            systemNavigatonManager.AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
            systemNavigatonManager.BackRequested -= SystemNavigatonManager_BackRequested;
        }

        private void SystemNavigatonManager_BackRequested(object sender, BackRequestedEventArgs e)
        {
            this.Frame.GoBack();
            e.Handled = true;
        }
    }
}
