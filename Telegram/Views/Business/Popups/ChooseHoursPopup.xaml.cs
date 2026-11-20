//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.ViewModels.Business;
using Telegram.Views.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Business.Popups
{
    public sealed partial class ChooseHoursPopup : ContentPopup
    {
        private const int DayMinutes = 24 * 60;

        // The spacing of the suggested times; any minute can still be typed.
        private const int TimeStep = 30;

        private readonly ObservableCollection<BusinessHoursRange> _ranges;

        private readonly int _min;
        private readonly int _max;
        private readonly int _maxCount;

        private bool _updating;

        public ChooseHoursPopup(BusinessDay day, int min, int max, int maxCount)
        {
            InitializeComponent();

            Title = day.Name;

            _ranges = new ObservableCollection<BusinessHoursRange>(day.Ranges
                .Select(x => new BusinessHoursRange(x.StartMinute, x.EndMinute)));

            _min = min;
            _max = max;
            _maxCount = maxCount;

            Open.IsChecked = _ranges.Count > 0;
            UpdateItems();

            PrimaryButtonText = Strings.OK;
            SecondaryButtonText = Strings.Cancel;
        }

        public IList<BusinessHoursRange> Ranges => _ranges;

        private bool IsOpen24 => _ranges.Count == 1 && _ranges[0].StartMinute == 0 && _ranges[0].EndMinute == DayMinutes - 1;

        private bool CanAdd
        {
            get
            {
                if (_ranges.Count >= _maxCount)
                {
                    return false;
                }

                return _ranges.Count == 0 || IsOpen24 || _ranges[^1].EndMinute < Math.Min(DayMinutes - 2, _max - 2);
            }
        }

        private void UpdateItems()
        {
            var open = Open.IsChecked is true;
            var open24 = open && IsOpen24;

            // A day open around the clock is shown by the check and the strip, with no times to edit.
            ScrollingHost.ItemsSource = open && !open24 ? _ranges : null;

            Day.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            Footer.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            AddButton.Visibility = open && CanAdd ? Visibility.Visible : Visibility.Collapsed;

            UpdateRows(null);
            UpdateStrip();
        }

        private void UpdateStrip()
        {
            // The strip's bindings are one-time, so it is rebound to redraw after an edit.
            Strip.ItemsSource = null;
            Strip.ItemsSource = _ranges;
        }

        public static GridLength StripBefore(int startMinute)
        {
            return new GridLength(StripMinute(startMinute), GridUnitType.Star);
        }

        public static GridLength StripLength(int startMinute, int endMinute)
        {
            return new GridLength(Math.Max(0, StripMinute(endMinute) - StripMinute(startMinute)), GridUnitType.Star);
        }

        public static GridLength StripAfter(int endMinute)
        {
            return new GridLength(DayMinutes - StripMinute(endMinute), GridUnitType.Star);
        }

        private static int StripMinute(int minute)
        {
            // The strip is today only, and a range ending at 23:59 is drawn to the edge.
            return minute >= DayMinutes - 1 ? DayMinutes : minute;
        }

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TableListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is Grid row && args.Item is BusinessHoursRange range)
            {
                UpdateRow(row, range, null);
            }
        }

        private void UpdateRows(ComboBox except)
        {
            // Each list is bounded by its neighbours, so an edit anywhere can move every other list's
            // bounds. Rows not realized yet are filled in by ContainerContentChanging.
            for (int i = 0; i < _ranges.Count; i++)
            {
                if (ScrollingHost.ContainerFromIndex(i) is ListViewItem container && container.ContentTemplateRoot is Grid row)
                {
                    UpdateRow(row, _ranges[i], except);
                }
            }
        }

        private void UpdateRow(Grid row, BusinessHoursRange range, ComboBox except)
        {
            var index = _ranges.IndexOf(range);
            if (index < 0)
            {
                return;
            }

            _updating = true;

            if (row.Children[0] is ComboBox start && start != except)
            {
                GetStartBounds(index, out int min, out int max);
                SetTimes(start, min, max, range.StartMinute);
            }

            if (row.Children[2] is ComboBox end && end != except)
            {
                GetEndBounds(index, out int min, out int max);
                SetTimes(end, min, max, range.EndMinute);
            }

            _updating = false;

            // The badge is the last child of the row, under the closing time.
            if (row.Children[row.Children.Count - 1] is TextBlock badge)
            {
                badge.Visibility = range.EndMinute > DayMinutes ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void GetStartBounds(int index, out int min, out int max)
        {
            min = index > 0 ? _ranges[index - 1].EndMinute + 1 : _min;
            max = Math.Min(_ranges[index].EndMinute - 1, DayMinutes - 1);
        }

        private void GetEndBounds(int index, out int min, out int max)
        {
            min = _ranges[index].StartMinute + 1;
            max = index < _ranges.Count - 1 ? _ranges[index + 1].StartMinute - 1 : _max;
        }

        private static void SetTimes(ComboBox box, int min, int max, int current)
        {
            var minutes = new List<int>();

            for (int minute = (min + TimeStep - 1) / TimeStep * TimeStep; minute <= max; minute += TimeStep)
            {
                minutes.Add(minute);
            }

            // A day that ends before the next one opens at midnight closes at 23:59, off the grid.
            if (max == DayMinutes - 1)
            {
                minutes.Add(max);
            }

            if (!minutes.Contains(current))
            {
                minutes.Add(current);
                minutes.Sort();
            }

            var items = new ObservableCollection<SettingsOptionItem<int>>();
            var selected = default(SettingsOptionItem<int>);

            foreach (var minute in minutes)
            {
                var item = CreateTime(minute);
                items.Add(item);

                if (minute == current)
                {
                    selected = item;
                }
            }

            box.ItemsSource = items;
            box.SelectedItem = selected;
        }

        private static SettingsOptionItem<int> CreateTime(int minute)
        {
            return new SettingsOptionItem<int>(minute, Formatter.Time(DateTime.Today.AddMinutes(minute)));
        }

        private static void SelectTime(ComboBox box, int minute)
        {
            if (box.ItemsSource is not ObservableCollection<SettingsOptionItem<int>> items)
            {
                return;
            }

            var index = 0;

            for (; index < items.Count; index++)
            {
                if (items[index].Value == minute)
                {
                    box.SelectedItem = items[index];
                    return;
                }
                else if (items[index].Value > minute)
                {
                    break;
                }
            }

            // A typed time off the grid joins the list, which must be observable for the
            // ComboBox to select an item inserted during TextSubmitted.
            var item = CreateTime(minute);
            items.Insert(index, item);
            box.SelectedItem = item;
        }

        private void Start_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updating || sender is not ComboBox box || box.SelectedItem is not SettingsOptionItem<int> item)
            {
                return;
            }

            if (box.Parent is Grid row && row.DataContext is BusinessHoursRange range && range.StartMinute != item.Value)
            {
                range.StartMinute = item.Value;
                UpdateAfterEdit(box);
            }
        }

        private void End_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updating || sender is not ComboBox box || box.SelectedItem is not SettingsOptionItem<int> item)
            {
                return;
            }

            if (box.Parent is Grid row && row.DataContext is BusinessHoursRange range && range.EndMinute != item.Value)
            {
                range.EndMinute = item.Value;
                UpdateAfterEdit(box);
            }
        }

        private void Start_TextSubmitted(ComboBox sender, ComboBoxTextSubmittedEventArgs args)
        {
            // Handled either way: a time that doesn't parse leaves the previous one selected.
            args.Handled = true;

            if (sender.Parent is not Grid row || row.DataContext is not BusinessHoursRange range)
            {
                return;
            }

            var index = _ranges.IndexOf(range);
            if (index >= 0 && TryParseTime(args.Text, out int minute))
            {
                GetStartBounds(index, out int min, out int max);
                SelectTime(sender, Clamp(minute, min, max));
            }
        }

        private void End_TextSubmitted(ComboBox sender, ComboBoxTextSubmittedEventArgs args)
        {
            args.Handled = true;

            if (sender.Parent is not Grid row || row.DataContext is not BusinessHoursRange range)
            {
                return;
            }

            var index = _ranges.IndexOf(range);
            if (index >= 0 && TryParseTime(args.Text, out int minute))
            {
                // A typed time has no day: closing at or before opening means the next day.
                if (minute <= range.StartMinute)
                {
                    minute += DayMinutes;
                }

                GetEndBounds(index, out int min, out int max);
                SelectTime(sender, Clamp(minute, min, max));
            }
        }

        private static bool TryParseTime(string text, out int minute)
        {
            minute = 0;
            text = text?.Trim();

            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            // Digits alone are hours, or hours and minutes: 9, 930, 2130.
            if (text.Length <= 4 && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value))
            {
                var hours = text.Length <= 2 ? value : value / 100;
                var minutes = text.Length <= 2 ? 0 : value % 100;

                if (hours < 24 && minutes < 60)
                {
                    minute = hours * 60 + minutes;
                    return true;
                }

                return false;
            }

            if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.NoCurrentDateDefault, out DateTime time)
                || DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.NoCurrentDateDefault, out time))
            {
                minute = (int)time.TimeOfDay.TotalMinutes;
                return true;
            }

            return false;
        }

        private void UpdateAfterEdit(ComboBox source)
        {
            // Rows stay put while a time is edited, even if the day becomes open around the clock.
            UpdateRows(source);

            AddButton.Visibility = CanAdd ? Visibility.Visible : Visibility.Collapsed;
            UpdateStrip();
        }

        private static int Clamp(int value, int min, int max)
        {
            // The lower bound wins when the bounds cross, so a range never ends before it starts.
            return Math.Max(Math.Min(value, max), min);
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            _ranges.Clear();

            if (Open.IsChecked is true)
            {
                _ranges.Add(new BusinessHoursRange(0, DayMinutes - 1));
            }

            UpdateItems();
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.DataContext is not BusinessHoursRange range)
            {
                return;
            }

            _ranges.Remove(range);

            // Removing the last set of hours leaves the day open, around the clock.
            if (_ranges.Count == 0)
            {
                _ranges.Add(new BusinessHoursRange(0, DayMinutes - 1));
            }

            UpdateItems();
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (_ranges.Count == 0 || IsOpen24)
            {
                _ranges.Clear();

                var start = Clamp(8 * 60, _min, _max - 1);
                var end = Clamp(20 * 60, start + 1, _max);

                _ranges.Add(new BusinessHoursRange(start, end));
            }
            else
            {
                var lastEnd = _ranges[^1].EndMinute;

                var start = Clamp(lastEnd + 30, _min, _max - 1);
                var end = Clamp((lastEnd + 26 * 60) / 2, start + 1, _max);

                _ranges.Add(new BusinessHoursRange(start, end));
            }

            UpdateItems();
        }
    }
}
