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
using TimeZone = Telegram.Td.Api.TimeZone;

namespace Telegram.ViewModels.Business
{
    public partial class BusinessHoursRange
    {
        public BusinessHoursRange(TimeSpan start, TimeSpan end)
        {
            Start = start;
            End = end;
        }

        public BusinessHoursRange(int start, int end)
        {
            Start = TimeSpan.FromHours(start);
            End = TimeSpan.FromHours(end);
        }

        public static BusinessHoursRange FromMinutes(int start, int end)
        {
            return new BusinessHoursRange(TimeSpan.FromMinutes(start), TimeSpan.FromMinutes(end));
        }

        public TimeSpan Start { get; set; }

        public TimeSpan End { get; set; }

        public override string ToString()
        {
            var start = Formatter.Time(DateTime.Today.Add(Start));
            var end = Formatter.Time(DateTime.Today.Add(End));

            if (End.TotalHours > 24)
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
                else if (Ranges.Count == 1 && Ranges[0].Start == TimeSpan.Zero && Ranges[0].End == TimeSpan.FromHours(24))
                {
                    return Strings.BusinessHoursDayFullOpened;
                }

                return string.Join(", ", Ranges);
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
        public BusinessHoursViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            //TimeZone = Timezone.Current;
        }

        protected override async Task OnNavigatedToAsync(UserFullInfo cached, NavigationMode mode, NavigationState state)
        {
            var hours = cached?.BusinessInfo?.OpeningHours;
            if (hours != null)
            {
                IsEnabled = true;

                foreach (var interval in hours.OpeningHours)
                {
                    void FillDay(BusinessDay day)
                    {
                        if (TextStyleRun.GetRelativeRange(interval.StartMinute, interval.EndMinute - interval.StartMinute, day.StartMinute, day.EndMinute - day.StartMinute, out int startMinute, out int lengthMinute))
                        {
                            day.Ranges.Add(BusinessHoursRange.FromMinutes(startMinute, startMinute + lengthMinute));
                        }
                    }

                    FillDay(Monday);
                    FillDay(Tuesday);
                    FillDay(Wednesday);
                    FillDay(Thursday);
                    FillDay(Friday);
                    FillDay(Saturday);
                    FillDay(Sunday);
                }
                //}
                //{
                var response = await ClientService.SendAsync(new GetTimeZones());
                if (response is TimeZones zones)
                {
                    TimeZone = zones.TimeZonesValue.FirstOrDefault(x => x.Id == hours.TimeZoneId);
                }
            }
        }

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set => Set(ref _isEnabled, value);
        }

        private BusinessDay _monday = new(DayOfWeek.Monday);
        public BusinessDay Monday
        {
            get => _monday;
            set => Set(ref _monday, value);
        }

        private BusinessDay _tuesday = new(DayOfWeek.Tuesday);
        public BusinessDay Tuesday
        {
            get => _tuesday;
            set => Set(ref _tuesday, value);
        }

        private BusinessDay _wednesday = new(DayOfWeek.Wednesday);
        public BusinessDay Wednesday
        {
            get => _wednesday;
            set => Set(ref _wednesday, value);
        }

        private BusinessDay _thursday = new(DayOfWeek.Thursday);
        public BusinessDay Thursday
        {
            get => _thursday;
            set => Set(ref _thursday, value);
        }

        private BusinessDay _friday = new(DayOfWeek.Friday);
        public BusinessDay Friday
        {
            get => _friday;
            set => Set(ref _friday, value);
        }

        private BusinessDay _saturday = new(DayOfWeek.Saturday);
        public BusinessDay Saturday
        {
            get => _saturday;
            set => Set(ref _saturday, value);
        }

        private BusinessDay _sunday = new(DayOfWeek.Sunday);
        public BusinessDay Sunday
        {
            get => _sunday;
            set => Set(ref _sunday, value);
        }

        private TimeZone _timezone;
        public TimeZone TimeZone
        {
            get => _timezone;
            set => Set(ref _timezone, value);
        }

        public async void ChangeHours(BusinessDay day)
        {
            var popup = new ChooseHoursPopup(day);

            var confirm = await ShowPopupAsync(popup);
            if (confirm == ContentDialogResult.Primary)
            {
                day.Ranges.Clear();
                day.Ranges.AddRange(popup.Ranges);

                RaisePropertyChanged(day.DayOfWeek.ToString());
            }
        }

        public void ToggleHours(BusinessDay day)
        {
            if (day.Ranges.Count > 0)
            {
                day.Ranges.Clear();
            }
            else
            {
                day.Ranges.Add(new BusinessHoursRange(TimeSpan.Zero, TimeSpan.FromHours(24)));
            }

            RaisePropertyChanged(day.DayOfWeek.ToString());
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

        public override void Continue()
        {
            throw new NotImplementedException();
        }
    }
}
