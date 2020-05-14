using System;
using Unigram.Converters;
using Unigram.ViewModels.Settings;
using Windows.UI.Xaml.Controls;

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

        #region Binding

        private string ConvertSinceDate(DateTime sinceDate)
        {
            if (sinceDate == DateTime.MinValue)
            {
                return null;
            }

            return string.Format(Strings.Resources.NetworkUsageSince, string.Format(Strings.Resources.formatDateAtTime, BindConvert.Current.ShortDate.Format(sinceDate), BindConvert.Current.ShortTime.Format(sinceDate)));
        }

        #endregion
    }
}
