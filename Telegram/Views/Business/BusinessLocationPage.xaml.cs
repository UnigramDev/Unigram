//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Td.Api;
using Telegram.ViewModels.Business;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Business
{
    public sealed partial class BusinessLocationPage : HostedPage
    {
        public BusinessLocationViewModel ViewModel => DataContext as BusinessLocationViewModel;

        public BusinessLocationPage()
        {
            InitializeComponent();
            Title = Strings.BusinessLocation;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged += OnPropertyChanged;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ADDRESS_INVALID")
            {
                VisualUtilities.ShakeView(Address);
            }
            else if (e.PropertyName == nameof(ViewModel.IsLocationValid) || e.PropertyName == nameof(ViewModel.Location))
            {
                UpdateLocation(ViewModel.IsLocationValid, ViewModel.Location);
            }
        }

        #region Binding

        private void UpdateLocation(bool valid, Location location)
        {
            if (valid)
            {
                Map.XamlRoot = XamlRoot;
                Map.SetSource(ViewModel.ClientService, location, 200, 200, 0);
            }
        }

        private Visibility ConvertClear(string address, bool valid)
        {
            return string.IsNullOrEmpty(address) && !valid
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        #endregion

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateLocation(ViewModel.IsLocationValid, ViewModel.Location);

            Address.Focus(FocusState.Pointer);
            Address.SelectionStart = int.MaxValue;
        }
    }
}
