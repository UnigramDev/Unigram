//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Converters;
using Telegram.ViewModels.Settings;

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

        #region Binding

        private string ConvertSinceDate(DateTime sinceDate)
        {
            if (sinceDate == DateTime.MinValue)
            {
                return null;
            }

            return string.Format(Strings.NetworkUsageSince, Formatter.DateAt(sinceDate));
        }

        #endregion
    }
}
