//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Native;
using Telegram.Td.Api;
using Windows.System.UserProfile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Business.Popups
{
    public sealed partial class ChooseAwayPopup : ContentPopup
    {
        public ChooseAwayPopup(string title, DateTime minDate, DateTime date)
        {
            InitializeComponent();

            Date.Date = date.Date;
            Time.Time = date.TimeOfDay;

            Date.CalendarIdentifier = GlobalizationPreferences.Calendars.FirstOrDefault();
            Time.ClockIdentifier = GlobalizationPreferences.Clocks.FirstOrDefault();

            Date.Language = NativeUtils.GetCurrentCulture();
            Time.Language = NativeUtils.GetCurrentCulture();

            Date.MinDate = minDate;
            Date.MaxDate = minDate.AddYears(1);

            Title = title;
            PrimaryButtonText = Strings.OK;
            SecondaryButtonText = Strings.Cancel;

            DefaultButton = ContentDialogButton.Primary;
        }

        public DateTime Value
        {
            get
            {
                if (Date.Date is DateTimeOffset date)
                {
                    return date.Add(Time.Time).UtcDateTime;
                }

                return DateTime.MinValue;
            }
        }

        public bool IsUntilOnline { get; private set; }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (IsUntilOnline)
            {
                return;
            }

            if (Date.Date == null || Date.Date < DateTime.Today)
            {
                VisualUtilities.ShakeView(Date);
                args.Cancel = true;
            }
            else if (Date.Date == DateTime.Today && Time.Time <= DateTime.Now.TimeOfDay)
            {
                VisualUtilities.ShakeView(Time);
                args.Cancel = true;
            }
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void Online_Click(object sender, RoutedEventArgs e)
        {
            IsUntilOnline = true;
            Hide(ContentDialogResult.Primary);
        }
    }
}
