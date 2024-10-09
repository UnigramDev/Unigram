//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Native;
using Windows.System.UserProfile;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class ChooseDateTimeToast : TeachingTip
    {
        private readonly TaskCompletionSource<ContentDialogResult> _tsc = new();

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

            ActionButtonClick += OnActionButtonClick;
            Closed += OnClosed;
        }

        private void OnActionButtonClick(TeachingTip sender, object args)
        {
            _tsc.TrySetResult(ContentDialogResult.Primary);
            IsOpen = false;
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

        private void OnClosed(TeachingTip sender, TeachingTipClosedEventArgs args)
        {
            _tsc.TrySetResult(ContentDialogResult.Secondary);
        }

        public Task<ContentDialogResult> ShowAsync()
        {
            IsOpen = true;
            return _tsc.Task;
        }
    }
}
