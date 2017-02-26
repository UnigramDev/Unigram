using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.Helpers;
using Template10.Common;
using Unigram.Core.Services;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SwitchAccountPage : Page
    {
        public SwitchAccountPage()
        {
            this.InitializeComponent();

            Loaded += SwitchAccountPage_Loaded;
        }

        private void SwitchAccountPage_Loaded(object sender, RoutedEventArgs e)
        {
            var app = BootStrapper.Current as App;
            if (app != null)
            {
                app.Locator.Configure();

                foreach (var frame in WindowWrapper.Current().NavigationServices.ToList())
                {
                    if (frame.FrameFacade.FrameId != "")
                    {
                        WindowWrapper.Current().NavigationServices.Remove(frame);
                    }
                }

                Frame.Navigate(typeof(MainPage));
                Frame.BackStack.Clear();
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Frame.BackStack.Clear();
            Frame.ForwardStack.Clear();

            var app = BootStrapper.Current as App;
            if (app != null)
            {
                var parameter = TLSerializationService.Current.Deserialize<string>((string)e.Parameter);
                SettingsHelper.SessionGuid = parameter;

                if (SettingsHelper.SessionGuid != parameter)
                {
                    FileUtils.CreateTemporaryFolder();
                }

                //app.Locator.Configure();
                //Frame.Navigate(typeof(MainPage));
                //Frame.BackStack.Clear();
            }
        }
    }
}
