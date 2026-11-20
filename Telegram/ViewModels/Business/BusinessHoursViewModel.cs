//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Business.Popups;
using Telegram.Views.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Business
{
    public partial class BusinessHoursRange
    {
        public BusinessHoursRange(int startMinute, int endMinute)
        {
            StartMinute = startMinute;
            EndMinute = endMinute;
        }

        public static BusinessHoursRange FromMinutes(int start, int end)
        {
            return new BusinessHoursRange(start, end);
        }

        // Minutes from the start of the day the range belongs to. The end may run into the next
        // day, up to 2 * 24 * 60 - 1, and a day open around the clock ends at 23:59, not 24:00.
        public int StartMinute { get; set; }

        public int EndMinute { get; set; }

        public TimeSpan Start => TimeSpan.FromMinutes(StartMinute);

        public TimeSpan End => TimeSpan.FromMinutes(EndMinute);

        public override string ToString()
        {
            var start = Formatter.Time(DateTime.Today.Add(Start));
            var end = Formatter.Time(DateTime.Today.Add(End));

            if (EndMinute > 24 * 60)
            {
                end = string.Format(Strings.BusinessHoursNextDay, end);
            }

            return string.Format("{0} - {1}", start, end);
        }
    }

    public partial class BusinessDay
    {
        public BusinessDay(DayOfWeek dayOfWeek)
        {
            DayOfWeek = dayOfWeek;
        }

        public DayOfWeek DayOfWeek { get; }

        public int IndexOfWeek
        {
            get
            {
                return DayOfWeek switch
                {
                    DayOfWeek.Monday => 0,
                    DayOfWeek.Tuesday => 1,
                    DayOfWeek.Wednesday => 2,
                    DayOfWeek.Thursday => 3,
                    DayOfWeek.Friday => 4,
                    DayOfWeek.Saturday => 5,
                    DayOfWeek.Sunday => 6,
                    _ => 0
                };
            }
        }

        public int StartMinute => IndexOfWeek * 60 * 24;
        public int EndMinute => StartMinute + 60 * 24;

        public List<BusinessHoursRange> Ranges { get; } = new();

        public string Name => LocaleService.Current.CurrentCulture.DateTimeFormat.GetDayName(DayOfWeek);

        public string Description
        {
            get
            {
                if (Ranges.Count == 0)
                {
                    return Strings.BusinessHoursDayClosed;
                }
                else if (IsFull)
                {
                    return Strings.BusinessHoursDayFullOpened;
                }

                return string.Join(", ", Ranges);
            }
        }

        public bool IsFull
        {
            get
            {
                if (Ranges.Count == 0)
                {
                    return false;
                }

                var last = 0;

                foreach (var range in Ranges)
                {
                    if (last < range.StartMinute)
                    {
                        return false;
                    }

                    last = range.EndMinute;
                }

                return last == 24 * 60 - 1 || last == 24 * 60;
            }
        }

        public string Description2
        {
            get
            {
                if (Ranges.Count == 0)
                {
                    return Strings.BusinessHoursProfileClose;
                }
                else if (Ranges.Count == 1 && Ranges[0].Start == TimeSpan.Zero && Ranges[0].End == TimeSpan.FromHours(24))
                {
                    return Strings.BusinessHoursProfileOpen;
                }

                return string.Join("\n", Ranges);
            }
        }

        public string DescriptionAt(TimeSpan offset)
        {
            if (Ranges.Count == 0)
            {
                return Strings.BusinessHoursProfileClose;
            }
            else if (Ranges.Count == 1 && Ranges[0].Start == TimeSpan.Zero && Ranges[0].End == TimeSpan.FromHours(24))
            {
                return Strings.BusinessHoursProfileOpen;
            }

            return string.Join("\n", Ranges.Where(x => x.Start > offset || (x.Start < offset && x.End > offset)));
        }

        public bool IsOpen => Ranges.Count > 0;

        public bool IsOpen24 => Ranges.Count == 1 && Ranges[0].Start == TimeSpan.Zero && Ranges[0].End == TimeSpan.FromHours(24);

        public bool IsOpenAt(TimeSpan time)
        {
            return Ranges.Any(x => time.IsBetween(x.Start, x.End));
        }

        public static bool GetRelativeRange2(int start, int end, int rangeStart, int rangeEnd, int tolerance, out int newStart, out int newEnd, out bool consumed)
        {
            // Included, Included
            if (start > rangeStart && end <= rangeEnd)
            {
                newStart = start;
                newEnd = end;
                consumed = true;
            }
            // Before, Included
            else if (start <= rangeStart && end > rangeStart && end < rangeEnd)
            {
                newStart = rangeStart;
                newEnd = end;
                consumed = false;
            }
            // Included, After
            else if (start > rangeStart && start < rangeEnd && end > rangeEnd)
            {
                if (end <= rangeEnd + tolerance)
                {
                    newStart = start;
                    newEnd = end;
                    consumed = true;
                }
                else
                {
                    newStart = start;
                    newEnd = rangeEnd;
                    consumed = false;
                }
            }
            // Before, After
            else if (start <= rangeStart && end >= rangeEnd)
            {
                newStart = rangeStart;
                newEnd = rangeEnd;
                consumed = false;
            }
            else
            {
                newStart = -1;
                newEnd = end;
                consumed = false;
                return false;
            }

            return true;
        }
    }

    public partial class BusinessHoursViewModel : BusinessFeatureViewModelBase
    {
        private const int DayMinutes = 24 * 60;

        // Counted across the whole week, which is the server's limit on intervals.
        private const int PeriodsCountLimit = 28;

        // Indexed from Monday, the first day of the week in businessOpeningHoursInterval minutes.
        private readonly BusinessDay[] _days = new BusinessDay[]
        {
            new(DayOfWeek.Monday),
            new(DayOfWeek.Tuesday),
            new(DayOfWeek.Wednesday),
            new(DayOfWeek.Thursday),
            new(DayOfWeek.Friday),
            new(DayOfWeek.Saturday),
            new(DayOfWeek.Sunday)
        };

        public BusinessHoursViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        protected override async Task OnNavigatedToAsync(UserFullInfo cached, NavigationMode mode, NavigationState state)
        {
            var hours = cached?.BusinessInfo?.OpeningHours;
            if (hours != null)
            {
                Set(ref _isEnabled, true, nameof(IsEnabled));
                FillDays(hours);
            }
            else
            {
                // Turning the switch on for the first time starts from weekdays open around the clock.
                for (int i = 0; i < 5; i++)
                {
                    _days[i].Ranges.Add(new BusinessHoursRange(0, DayMinutes - 1));
                }
            }

            var response = await ClientService.SendAsync(new GetTimeZones());
            if (response is TimeZones zones)
            {
                Set(ref _timezone, zones.TimeZonesValue.FirstOrDefault(x => x.Id == hours?.TimeZoneId)
                    ?? GetSystemTimeZone(zones), nameof(TimeZone));
            }

            // Loading splits an interval that crosses midnight into one range per day, and saving
            // writes the halves back out, so the cached object is round-tripped to compare like for like.
            _cached = GetSettings();
            RaisePropertyChanged(nameof(HasChanged));
        }

        private static TimeZone GetSystemTimeZone(TimeZones zones)
        {
            // Unlike TimeZoneInfo, the WinRT calendar names the zone by its IANA identifier, as the server does.
            var id = new Windows.Globalization.Calendar().GetTimeZone();
            var offset = (int)DateTimeOffset.Now.Offset.TotalSeconds;

            return zones.TimeZonesValue.FirstOrDefault(x => x.Id == id)
                ?? zones.TimeZonesValue.FirstOrDefault(x => x.UtcTimeOffset == offset)
                ?? zones.TimeZonesValue.FirstOrDefault();
        }

        private void FillDays(BusinessOpeningHours hours)
        {
            var intervals = hours.OpeningHours;

            foreach (var interval in intervals)
            {
                var day = interval.StartMinute / DayMinutes % 7;
                var start = interval.StartMinute % DayMinutes;
                var end = start + (interval.EndMinute - interval.StartMinute);

                _days[day].Ranges.Add(new BusinessHoursRange(start, end));
            }

            for (int i = 0; i < 7; i++)
            {
                var dayStart = DayMinutes * i;
                var dayEnd = DayMinutes * (i + 1);

                var covered = dayStart;

                foreach (var interval in intervals)
                {
                    if (interval.StartMinute <= covered && interval.EndMinute >= covered)
                    {
                        covered = interval.EndMinute + 1;
                    }
                }

                var today = _days[i].Ranges;
                var previous = _days[(i + 6) % 7].Ranges;
                var next = _days[(i + 1) % 7].Ranges;

                if (covered >= dayEnd)
                {
                    // A day covered end to end becomes a single range that owns the hours past
                    // midnight, so the previous day's spill-over into it is cut at 23:59.
                    if (previous.Count > 0 && previous[^1].EndMinute >= DayMinutes)
                    {
                        previous[^1].EndMinute = DayMinutes - 1;
                    }

                    var end = Math.Min(covered - dayStart - 1, DayMinutes * 2 - 1);
                    if (end >= DayMinutes && next.Count > 0 && next[0].StartMinute < end - DayMinutes)
                    {
                        end = DayMinutes + next[0].StartMinute - 1;
                    }

                    today.Clear();
                    today.Add(new BusinessHoursRange(0, end));
                }
                else if (today.Count > 0 && next.Count > 0)
                {
                    var last = today[^1];
                    var first = next[0];

                    // A range that ends exactly where tomorrow's begins is split at midnight instead.
                    if (last.EndMinute > DayMinutes && last.EndMinute - DayMinutes + 1 == first.StartMinute)
                    {
                        last.EndMinute = DayMinutes - 1;
                        first.StartMinute = 0;
                    }
                }
            }
        }

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set => Invalidate(ref _isEnabled, value);
        }

        public BusinessDay Monday => _days[0];

        public BusinessDay Tuesday => _days[1];

        public BusinessDay Wednesday => _days[2];

        public BusinessDay Thursday => _days[3];

        public BusinessDay Friday => _days[4];

        public BusinessDay Saturday => _days[5];

        public BusinessDay Sunday => _days[6];

        private TimeZone _timezone;
        public TimeZone TimeZone
        {
            get => _timezone;
            set => Invalidate(ref _timezone, value);
        }

        public async void ChangeHours(BusinessDay day)
        {
            var index = day.IndexOfWeek;

            // A day's hours may start after the previous day's last range spills into it, and may
            // run into the next day up to a minute before that day's first range opens.
            var previousEnd = 0;

            foreach (var range in _days[(index + 6) % 7].Ranges)
            {
                previousEnd = Math.Max(previousEnd, range.EndMinute);
            }

            var nextStart = DayMinutes;

            foreach (var range in _days[(index + 1) % 7].Ranges)
            {
                nextStart = Math.Min(nextStart, range.StartMinute);
            }

            var min = Math.Max(0, previousEnd + 1 - DayMinutes);
            var max = DayMinutes + nextStart - 1;

            var popup = new ChooseHoursPopup(day, min, max, GetMaxPeriodsCount());

            var confirm = await ShowPopupAsync(popup);
            if (confirm == ContentDialogResult.Primary)
            {
                day.Ranges.Clear();
                day.Ranges.AddRange(popup.Ranges);

                RaisePropertyChanged(day.DayOfWeek.ToString());
                RaisePropertyChanged(nameof(HasChanged));
                AdaptPreviousDay(day);
            }
        }

        public void ToggleHours(BusinessDay day)
        {
            if (day.Ranges.Count > 0)
            {
                day.Ranges.Clear();
                RaisePropertyChanged(day.DayOfWeek.ToString());
            }
            else
            {
                day.Ranges.Add(new BusinessHoursRange(0, DayMinutes - 1));
                RaisePropertyChanged(day.DayOfWeek.ToString());
                AdaptPreviousDay(day);
            }

            RaisePropertyChanged(nameof(HasChanged));
        }

        private int GetMaxPeriodsCount()
        {
            var used = 0;

            foreach (var day in _days)
            {
                // A closed day still keeps one in reserve, for turning it on around the clock.
                used += Math.Max(1, day.Ranges.Count);
            }

            return PeriodsCountLimit - used;
        }

        private void AdaptPreviousDay(BusinessDay day)
        {
            if (day.Ranges.Count == 0)
            {
                return;
            }

            var previous = _days[(day.IndexOfWeek + 6) % 7];
            if (previous.Ranges.Count == 0)
            {
                return;
            }

            var last = previous.Ranges[^1];
            if (last.EndMinute > DayMinutes - 1)
            {
                last.EndMinute = DayMinutes - 1;

                if (last.StartMinute >= last.EndMinute)
                {
                    previous.Ranges.Remove(last);
                }

                RaisePropertyChanged(previous.DayOfWeek.ToString());
            }
        }

        public async void ChangeTimeZone()
        {
            var response = await ClientService.SendAsync(new GetTimeZones());
            if (response is TimeZones zones)
            {
                var popup = new ChooseTimeZonePopup(zones, TimeZone);

                var confirm = await ShowPopupAsync(popup);
                if (confirm == ContentDialogResult.Primary)
                {
                    TimeZone = popup.SelectedItem;
                }
            }
        }

        public override bool HasChanged => !_cached.AreTheSame(GetSettings());

        protected override async void ContinueImpl(NavigatingEventArgs args)
        {
            var settings = GetSettings();
            if (settings.AreTheSame(_cached))
            {
                _completed = true;
                NavigationService.GoBack(args);
                return;
            }

            var response = await ClientService.SendAsync(new SetBusinessOpeningHours(settings));
            if (response is Ok)
            {
                _completed = true;
                NavigationService.GoBack(args);
            }
            else if (response is Error error)
            {
                ShowToast(error);
            }
        }

        private BusinessOpeningHours _cached;
        private BusinessOpeningHours GetSettings()
        {
            if (!IsEnabled)
            {
                return null;
            }

            var intervals = new MutableVector<BusinessOpeningHoursInterval>();

            foreach (var day in _days)
            {
                foreach (var range in day.Ranges)
                {
                    // Ranges are relative to the day they belong to, and the end is allowed to spill
                    // over into the next one, which is what businessOpeningHoursInterval expects.
                    intervals.Add(new BusinessOpeningHoursInterval(
                        day.StartMinute + range.StartMinute,
                        day.StartMinute + range.EndMinute));
                }
            }

            if (intervals.Count == 0)
            {
                return null;
            }

            return new BusinessOpeningHours(TimeZone?.Id ?? string.Empty, intervals);
        }
    }
}
