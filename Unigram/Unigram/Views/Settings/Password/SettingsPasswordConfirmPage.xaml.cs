//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Unigram.ViewModels.Settings.Password;

namespace Unigram.Views.Settings.Password
{
    public sealed partial class SettingsPasswordConfirmPage : HostedPage
    {
        public SettingsPasswordConfirmViewModel ViewModel => DataContext as SettingsPasswordConfirmViewModel;

        public SettingsPasswordConfirmPage()
        {
            InitializeComponent();
        }

        #region Binding

        private string ConvertPattern(string pattern)
        {
            return string.Format("[Please enter code we've just emailed at **{0}**]", pattern);
        }

        #endregion

    }
}
