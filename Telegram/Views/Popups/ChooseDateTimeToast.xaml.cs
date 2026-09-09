//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Linq;
using Telegram.Controls;
using Telegram.Native;
using Windows.System.UserProfile;
using Windows.UI.Xaml;

namespace Telegram.Views.Popups
{
    public sealed partial class ChooseDateTimeToast : ModalPopup
    {
        public ChooseDateTimeToast()
        {
            InitializeComponent();

            var date = DateTime.Now.AddMinutes(10);
            Date.Date = date.Date;
            Time.Time = date.TimeOfDay;

            Date.CalendarIdentifier = GlobalizationPreferences.Calendars.FirstOrDefault();
            Time.ClockIdentifier = GlobalizationPreferences.Clocks.FirstOrDefault();

            Date.Language = NativeUtils.GetCurrentCulture();
            Time.Language = NativeUtils.GetCurrentCulture();

            Date.MinDate = DateTime.Today;
            Date.MaxDate = DateTime.Today.AddYears(1);
        }

        public UIElement Footer
        {
            get => FooterRoot.Child;
            set => FooterRoot.Child = value;
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
    }
}
