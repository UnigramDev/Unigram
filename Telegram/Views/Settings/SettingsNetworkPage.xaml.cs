//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.ComponentModel;
using Telegram.Converters;
using Telegram.ViewModels.Settings;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsNetworkPage : HostedPage
    {
        public SettingsNetworkViewModel ViewModel => DataContext as SettingsNetworkViewModel;

        public SettingsNetworkPage()
        {
            InitializeComponent();
            Title = Strings.NetworkUsage;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged += OnPropertyChanged;

            UpdateTotalBytes(ViewModel.TotalBytes);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.TotalBytes))
            {
                UpdateTotalBytes(ViewModel.TotalBytes);
            }
        }

        #region Binding

        private void UpdateTotalBytes(long totalBytes)
        {
            var readable = FileSizeConverter.Convert(totalBytes, true).Split(' ');

            SizeLabel.Text = readable[0];
            UnitLabel.Text = readable[1];
        }

        private string ConvertSinceDate(DateTime sinceDate, long totalBytes)
        {
            if (sinceDate == DateTime.MinValue)
            {
                return null;
            }

            if (totalBytes == 0)
            {
                return string.Format(Strings.NoNetworkUsageSince, Formatter.DateAt(sinceDate));
            }

            return string.Format(Strings.YourNetworkUsageSince, Formatter.DateAt(sinceDate));
        }

        #endregion
    }
}
