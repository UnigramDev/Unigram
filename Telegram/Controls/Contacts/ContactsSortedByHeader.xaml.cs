using Telegram.ViewModels;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Contacts
{
    public sealed partial class ContactsSortedByHeader : UserControl
    {
        public ContactsViewModel ViewModel => DataContext as ContactsViewModel;

        public ContactsSortedByHeader()
        {
            InitializeComponent();
        }

        private string ConvertSortedBy(bool epoch)
        {
            return epoch ? Strings.SortedByLastSeen : Strings.SortedByName;
        }
    }
}
