using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Unigram.Helpers;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views.Settings
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsPasscodeLockPage : Page
    {
        /// <summary>
        /// When toggle switches are set for the first time they will trigger their toggled event which causes redundancy.
        /// Thus this variable indicates if the page is opened for the first time.
        /// </summary>
        private bool _isFirstTimeOpened = true;
        private ApplicationDataContainer _localsettingsDataContainer;
        private const string AuthenticationKey = "AuthenticationType";

        public SettingsPasscodeLockPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _localsettingsDataContainer = ApplicationData.Current.LocalSettings;
            GetAuthenticationToggleStatus();
        }

        /// <summary>
        /// Sets Initial Status of Toggle Buttons
        /// </summary>
        private void GetAuthenticationToggleStatus()
        {
            if (_localsettingsDataContainer.Values.ContainsKey(AuthenticationKey))
            {
                if (_localsettingsDataContainer.Values[AuthenticationKey].ToString().Equals("Local"))
                {
                    PasscodeLock.IsOn = true;
                }
                else if (_localsettingsDataContainer.Values[AuthenticationKey].ToString().Equals("WindowsHello"))
                {
                    WindowsHello.IsOn = true;
                }
            }
            _isFirstTimeOpened = false;
        }

        private async void PasscodeLock_OnToggled(object sender, RoutedEventArgs e)
        {
            if (_isFirstTimeOpened)
            {
                _isFirstTimeOpened = false;
                return;
            }
            if (PasscodeLock.IsOn)
            {
                if (WindowsHello.IsOn) WindowsHello.IsOn = false;
                _localsettingsDataContainer.Values[AuthenticationKey] = "Local";

                var result = await AuthenticationHelper.SaveLocalPasscode();
                if (!result)
                {
                    PasscodeLock.IsOn = false;
                }
            }
            else
            {
                if (_localsettingsDataContainer.Values.ContainsKey(AuthenticationKey))
                {
                    _localsettingsDataContainer.Values.Remove(AuthenticationKey);
                    await AuthenticationHelper.RemoveLocalPasscode();
                }
            }
        }

        private void WindowsHello_OnToggled(object sender, RoutedEventArgs e)
        {
            if (_isFirstTimeOpened)
            {
                _isFirstTimeOpened = false;
                return;
            }
            if (WindowsHello.IsOn)
            {
                if (PasscodeLock.IsOn) PasscodeLock.IsOn = false;
                _localsettingsDataContainer.Values[AuthenticationKey] = "WindowsHello";
            }
            else
            {
                if (_localsettingsDataContainer.Values.ContainsKey(AuthenticationKey))
                {
                    _localsettingsDataContainer.Values.Remove(AuthenticationKey);
                }
            }
        }
    }
}
