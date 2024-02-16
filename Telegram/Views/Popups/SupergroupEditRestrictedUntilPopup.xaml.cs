//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Linq;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Native;
using Windows.System.UserProfile;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class SupergroupEditRestrictedUntilPopup : ContentPopup
    {
        public SupergroupEditRestrictedUntilPopup(int until)
        {
            InitializeComponent();

            var date = until == 0 ? DateTime.Now.AddDays(1) : Formatter.ToLocalTime(until);
            Date.Date = date.Date;
            Time.Time = date.TimeOfDay;

            Date.CalendarIdentifier = GlobalizationPreferences.Calendars.FirstOrDefault();
            Time.ClockIdentifier = GlobalizationPreferences.Clocks.FirstOrDefault();

            Date.Language = NativeUtils.GetCurrentCulture();
            Time.Language = NativeUtils.GetCurrentCulture();

            Date.MinYear = DateTime.Today;

            Title = Strings.UserRestrictionsUntil;
            PrimaryButtonText = Strings.OK;
            SecondaryButtonText = Strings.UserRestrictionsUntilForever;
            CloseButtonText = Strings.Cancel;

            DefaultButton = ContentDialogButton.Primary;
        }

        public DateTime Value
        {
            get
            {
                return Date.Date.Add(Time.Time).UtcDateTime;
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
