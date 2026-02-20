//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Controls;
using Telegram.ViewModels.Settings;
using Windows.System;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsPowerSavingPage : HostedPage
    {
        public SettingsPowerSavingViewModel ViewModel => DataContext as SettingsPowerSavingViewModel;

        public SettingsPowerSavingPage()
        {
            InitializeComponent();
            Title = Strings.PowerUsage;
        }

        #region Binding

        private string ConvertAnimationsFooter(bool enabled)
        {
            if (enabled)
            {
                return Strings.LiteOptionsAnimationEffectsInfo;
            }

            return Strings.LiteOptionsAnimationEffectsDisabled;
        }

        #endregion

        private async void HeaderedControl_Click(object sender, TextUrlClickEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("ms-settings://easeofaccess-visualeffects"));
        }
    }
}
