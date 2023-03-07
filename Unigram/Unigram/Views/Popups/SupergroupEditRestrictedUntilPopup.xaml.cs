//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using Unigram.Controls;
using Unigram.Converters;
using Windows.System.UserProfile;

namespace Unigram.Views.Popups
{
    public sealed partial class SupergroupEditRestrictedUntilPopup : ContentPopup
    {
        public SupergroupEditRestrictedUntilPopup(int until)
        {
            InitializeComponent();

            var date = until == 0 ? DateTime.Now.AddDays(1) : Converter.DateTime(until);
            Date.Date = date.Date;
            Time.Time = date.TimeOfDay;

            Date.CalendarIdentifier = GlobalizationPreferences.Calendars.FirstOrDefault();
            Time.ClockIdentifier = GlobalizationPreferences.Clocks.FirstOrDefault();

            Date.Language = Native.NativeUtils.GetCurrentCulture();
            Time.Language = Native.NativeUtils.GetCurrentCulture();

            Date.MinYear = DateTime.Today;

            Title = Strings.Resources.UserRestrictionsUntil;
            PrimaryButtonText = Strings.Resources.OK;
            SecondaryButtonText = Strings.Resources.UserRestrictionsUntilForever;
            CloseButtonText = Strings.Resources.Cancel;

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
