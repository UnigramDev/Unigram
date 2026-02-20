//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.ViewModels.Business;
using Windows.UI.Xaml;

namespace Telegram.Controls.Cells.Business
{
    public sealed partial class BusinessHoursCell : SettingsButton
    {
        public BusinessHoursViewModel ViewModel => DataContext as BusinessHoursViewModel;

        public BusinessHoursCell()
        {
            InitializeComponent();
        }

        private BusinessDay _day;
        public BusinessDay Day
        {
            get => _day;
            set => SetDay(value);
        }

        private void SetDay(BusinessDay value)
        {
            _day = value;

            Content = value.Name;
            Description = value.Description;

            Switch.Toggled -= Switch_Toggled;
            Switch.IsOn = value.IsOpen;
            Switch.Toggled += Switch_Toggled;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ChangeHours(Day);
        }

        private void Switch_Toggled(object sender, RoutedEventArgs e)
        {
            ViewModel.ToggleHours(Day);
        }
    }
}
