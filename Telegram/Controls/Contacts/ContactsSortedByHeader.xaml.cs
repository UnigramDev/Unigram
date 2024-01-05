//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
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
