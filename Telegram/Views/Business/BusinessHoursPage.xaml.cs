//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.ViewModels.Business;

namespace Telegram.Views.Business
{
    public sealed partial class BusinessHoursPage : HostedPage
    {
        public BusinessHoursViewModel ViewModel => DataContext as BusinessHoursViewModel;

        public BusinessHoursPage()
        {
            InitializeComponent();
            Title = Strings.BusinessHours;
        }
    }
}
