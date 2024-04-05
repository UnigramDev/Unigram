using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Business;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells
{
    public sealed partial class ProfileHoursCell : SettingsExpander
    {
        private IClientService _clientService;
        private BusinessOpeningHours _hours;
        private bool _switchToMyTime;

        public ProfileHoursCell()
        {
            InitializeComponent();
            ExpandedChanged += OnExpandedChanged;
        }

        private void OnExpandedChanged(object sender, EventArgs e)
        {
            if (_clientService != null && _hours != null)
            {
                UpdateHours(_clientService, _hours);
            }
        }

        public void UpdateHours(IClientService clientService, BusinessOpeningHours hours)
        {
            _clientService = clientService;
            _hours = hours;

            Switch.Text = _switchToMyTime
                ? Strings.BusinessHoursProfileSwitchMy
                : Strings.BusinessHoursProfileSwitchLocal;

            clientService.TryGetTimeZone(hours.TimeZoneId, out Td.Api.TimeZone timeZone);

            var intervals = new List<BusinessOpeningHoursInterval>();
            var local = new List<BusinessOpeningHoursInterval>();

            var today = DateTimeOffset.Now;
            var offset = today.Offset - TimeSpan.FromSeconds(timeZone.UtcTimeOffset);
            var offsetMinute = (int)offset.TotalMinutes;

            foreach (var interval in hours.OpeningHours)
            {
                intervals.Add(new BusinessOpeningHoursInterval(interval.StartMinute, interval.EndMinute));
                local.Add(new BusinessOpeningHoursInterval(interval.StartMinute + offsetMinute, interval.EndMinute + offsetMinute));
            }

            SwitchButton.Visibility = offsetMinute == 0 || hours.Is24x7()
                ? Windows.UI.Xaml.Visibility.Collapsed
                : Windows.UI.Xaml.Visibility.Visible;

            var days = new List<BusinessDay>(7);
            var target = (_switchToMyTime ? local : intervals).ToList();
            var shifted = today.AddMinutes(_switchToMyTime ? -offsetMinute : -offsetMinute);

            for (int i = 0; i < 7; i++)
            {
                days.Add(new BusinessDay((DayOfWeek)(((int)today.DayOfWeek + i) % 7)));
            }

            foreach (var day in days)
            {
                for (int i = 0; i < hours.OpeningHours.Count; i++)
                {
                    if (hours.OpeningHours[i].StartMinute <= day.StartMinute && hours.OpeningHours[i].EndMinute >= day.EndMinute - 1)
                    {
                        day.Ranges.Add(BusinessHoursRange.FromMinutes(0, 60 * 24));
                    }
                }
            }

            for (int j = 0; j < days.Count; j++)
            {
                var day = days[j];
                if (day.IsOpen24)
                {
                    continue;
                }

                var next24 = days[j < days.Count - 1 ? j + 1 : 0].IsOpen24;

                for (int i = 0; i < target.Count; i++)
                {
                    BusinessOpeningHoursInterval interval = target[i];

                    if (BusinessDay.GetRelativeRange2(interval.StartMinute, interval.EndMinute, day.StartMinute, day.EndMinute, next24 ? 0 : 360, out int startMinute, out int endMinute, out bool consumed))
                    {
                        day.Ranges.Add(BusinessHoursRange.FromMinutes(startMinute - day.StartMinute, endMinute - day.StartMinute));

                        if (consumed)
                        {
                            target.RemoveAt(i);
                            i--;
                        }
                    }
                }
            }

            for (int i = 0; i < 7; i++)
            {
                if (i == 0)
                {
                    if (hours.Is24x7())
                    {
                        DateToday.Text = Strings.BusinessHoursProfileNowOpen;
                        DateToday.Foreground = BootStrapper.Current.Resources["SystemFillColorSuccessBrush"] as Brush;

                        TimeToday.Text = Strings.BusinessHoursProfileFullOpen;
                    }
                    else if (days[i].IsOpenAt(shifted.TimeOfDay))
                    {
                        DateToday.Text = Strings.BusinessHoursProfileNowOpen;
                        DateToday.Foreground = BootStrapper.Current.Resources["SystemFillColorSuccessBrush"] as Brush;

                        TimeToday.Text = days[i].DescriptionAt(shifted.TimeOfDay);
                    }
                    else
                    {
                        DateToday.Text = Strings.BusinessHoursProfileNowClosed;
                        DateToday.Foreground = BootStrapper.Current.Resources["SystemFillColorCriticalBrush"] as Brush;

                        if (IsExpanded)
                        {
                            TimeToday.Text = days[i].DescriptionAt(shifted.TimeOfDay);
                        }
                        else
                        {
                            var timeOfWeek = new BusinessDay(today.DayOfWeek).StartMinute + today.TimeOfDay.TotalMinutes;
                            var nextInterval = local.FirstOrDefault(x => x.StartMinute > timeOfWeek);
                            nextInterval ??= local.LastOrDefault(x => x.EndMinute < timeOfWeek);

                            if (nextInterval != null)
                            {
                                var opensPeriodTime = nextInterval.StartMinute;
                                var nowPeriodTime = (int)timeOfWeek;

                                var diff = opensPeriodTime < nowPeriodTime ? opensPeriodTime + (7 * 24 * 60 - nowPeriodTime) : opensPeriodTime - nowPeriodTime;
                                if (diff < 60)
                                {
                                    TimeToday.Text = Locale.Declension(Strings.R.BusinessHoursProfileOpensInMinutes, diff);
                                }
                                else if (diff < 24 * 60)
                                {
                                    TimeToday.Text = Locale.Declension(Strings.R.BusinessHoursProfileOpensInHours, (int)Math.Ceiling(diff / 60f));
                                }
                                else
                                {
                                    TimeToday.Text = Locale.Declension(Strings.R.BusinessHoursProfileOpensInDays, (int)Math.Ceiling(diff / 60f / 24f));
                                }
                            }
                        }
                    }
                }
                else
                {
                    var date = FindName($"Date{i}") as TextBlock;
                    var time = FindName($"Time{i}") as TextBlock;

                    date.Text = days[i].Name;
                    time.Text = days[i].Description2;
                }
            }
        }

        private void Switch_Click(Windows.UI.Xaml.Documents.Hyperlink sender, Windows.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            _switchToMyTime = !_switchToMyTime;

            if (IsExpanded)
            {
                UpdateHours(_clientService, _hours);
            }
            else
            {
                IsExpanded = true;
            }
        }
    }
}
