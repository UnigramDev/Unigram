//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Controls;
using Telegram.Native;
using Telegram.Navigation;
using Windows.System.UserProfile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class CalendarPopup : ContentPopup
    {
        private bool _programmaticChange;

        public CalendarPopup(DateTimeOffset? date = null)
        {
            InitializeComponent();

            View.CalendarIdentifier = GlobalizationPreferences.Calendars.FirstOrDefault();
            View.FirstDayOfWeek = GlobalizationPreferences.WeekStartsOn;
            View.Language = NativeUtils.GetCurrentCulture();

            if (date.HasValue)
            {
                View.SelectedDates.Add(date.Value);
                View.SetDisplayDate(date.Value);
            }

            View.SelectedDatesChanged += OnSelectedDatesChanged;

            var multiple = true;

            PrimaryButtonText = multiple ? Strings.SelectDays : Strings.OK;
            SecondaryButtonText = Strings.Close;
        }

        private void OnSelectedDatesChanged(CalendarView sender, CalendarViewSelectedDatesChangedEventArgs args)
        {
            if (sender.SelectionMode == CalendarViewSelectionMode.Multiple && !_programmaticChange)
            {
                if (args.AddedDates.Count > 1)
                {
                    return;
                }
                else if (args.AddedDates.Count == 1)
                {
                    _programmaticChange = true;

                    if (sender.SelectedDates.Count == 2)
                    {
                        var min = sender.SelectedDates[0] > sender.SelectedDates[1] ? sender.SelectedDates[1] : sender.SelectedDates[0];
                        var max = sender.SelectedDates[0] > sender.SelectedDates[1] ? sender.SelectedDates[0] : sender.SelectedDates[1];

                        var diff = max - min;

                        for (int i = 1; i < diff.TotalDays; i++)
                        {
                            sender.SelectedDates.Add(min.AddDays(i));
                        }
                    }
                    else
                    {
                        sender.SelectedDates.Clear();
                        sender.SelectedDates.Add(args.AddedDates[0]);
                    }

                    _programmaticChange = false;
                }
                else if (args.RemovedDates.Count == 1)
                {
                    _programmaticChange = true;

                    sender.SelectedDates.Clear();
                    sender.SelectedDates.Add(args.RemovedDates[0]);

                    _programmaticChange = false;
                }
            }
            else if (sender.SelectionMode == CalendarViewSelectionMode.Single)
            {
                if (sender.SelectedDates.Count == 1)
                {
                    _programmaticChange = true;
                    Hide(ContentDialogResult.Primary);
                }
            }
        }

        public DateTimeOffset MinDate
        {
            get => View.MinDate;
            set => View.MinDate = value;
        }

        public DateTimeOffset MaxDate
        {
            get => View.MaxDate;
            set => View.MaxDate = value;
        }

        public IList<DateTimeOffset> SelectedDates => View.SelectedDates;

        public bool ClearHistory { get; private set; }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (View.SelectionMode == CalendarViewSelectionMode.Single && !_programmaticChange)
            {
                SetValue(PrimaryButtonStyleProperty, BootStrapper.Current.Resources["DangerButtonStyle"] as Style);

                PrimaryButtonText = Strings.ClearHistory;
                SecondaryButtonText = Strings.Cancel;

                DefaultButton = ContentDialogButton.None;

                View.SelectedDates.Clear();
                View.SelectionMode = CalendarViewSelectionMode.Multiple;
                args.Cancel = true;
            }
            else if (View.SelectionMode == CalendarViewSelectionMode.Multiple && SelectedDates.Count > 1)
            {
                ClearHistory = true;
            }
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (View.SelectionMode == CalendarViewSelectionMode.Multiple)
            {
                ClearValue(PrimaryButtonStyleProperty);

                PrimaryButtonText = Strings.SelectDays;
                SecondaryButtonText = Strings.Close;

                DefaultButton = ContentDialogButton.Primary;

                View.SelectionMode = CalendarViewSelectionMode.Single;
                View.SelectedDates.Clear();
                args.Cancel = true;
            }
        }
    }
}
