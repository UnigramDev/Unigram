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

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsPrivacyPage : Page
    {
        public SettingsPrivacyPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            while (Frame.BackStackDepth > 1)
            {
                Frame.BackStack.RemoveAt(1);
            }
        }

        private void Sessions_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsSessionsPage));
        }

        private void BlockedUsers_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsBlockedUsersPage));
        }
    }
}
