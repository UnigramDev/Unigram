using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Converters;
using Unigram.ViewModels.Settings;
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
    public sealed partial class SettingsNetworkPage : Page
    {
        public SettingsNetworkViewModel ViewModel => DataContext as SettingsNetworkViewModel;

        public SettingsNetworkPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsNetworkViewModel>();
        }

        private void Header_BackRequested(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewBackRequestedEventArgs args)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        #region Binding

        private string ConvertSinceDate(DateTime sinceDate)
        {
            if (sinceDate == DateTime.MinValue)
            {
                return null;
            }

            return string.Format(Strings.Resources.NetworkUsageSince, string.Format(Strings.Resources.FormatDateAtTime, BindConvert.Current.ShortDate.Format(sinceDate), BindConvert.Current.ShortTime.Format(sinceDate)));
        }

        #endregion
    }
}
