using System;
using Unigram.Converters;
using Unigram.ViewModels.Settings;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsNetworkPage : HostedPage
    {
        public SettingsNetworkViewModel ViewModel => DataContext as SettingsNetworkViewModel;

        public SettingsNetworkPage()
        {
            InitializeComponent();
        }

        #region Binding

        private string ConvertSinceDate(DateTime sinceDate)
        {
            if (sinceDate == DateTime.MinValue)
            {
                return null;
            }

            return string.Format(Strings.Resources.NetworkUsageSince, string.Format(Strings.Resources.formatDateAtTime, Converter.ShortDate.Format(sinceDate), Converter.ShortTime.Format(sinceDate)));
        }

        #endregion
    }
}
