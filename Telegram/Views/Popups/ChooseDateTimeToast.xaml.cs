//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Controls;
using Telegram.Native;
using Telegram.Views.Host;
using Windows.System.UserProfile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class ChooseDateTimeToast : TeachingTipEx
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

        public Task<ContentDialogResult> ShowAsync(XamlRoot xamlRoot)
        {
            if (xamlRoot.Content is not IToastHost host)
            {
                return Task.FromResult(ContentDialogResult.None);
            }

            XamlRoot = xamlRoot;
            Closed += (s, args) =>
            {
                host.ToastClosed(s);
            };

            host.ToastOpened(this);

            IsOpen = true;
            return _tsc.Task;
        }
    }
}
