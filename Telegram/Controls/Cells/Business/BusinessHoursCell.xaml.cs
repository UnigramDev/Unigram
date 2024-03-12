using Telegram.ViewModels.Business;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Cells.Business
{
    public sealed partial class BusinessHoursCell : Grid
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

            Button.Content = value.Name;
            Button.Badge = value.Description;

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
