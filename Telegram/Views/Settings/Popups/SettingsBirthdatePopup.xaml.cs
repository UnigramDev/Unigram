//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Settings.Popups
{
    public sealed partial class SettingsBirthdatePopup : ContentPopup
    {
        private readonly ObservableCollection<int> _days = new();
        private readonly ObservableCollection<int> _months = new();

        public SettingsBirthdatePopup(Birthdate date, UserPrivacySettingRule primaryRule = null)
        {
            InitializeComponent();

            var dayPosition = 0;
            var monthPosition = 2;
            var yearPosition = 4;

            var parts = LocaleService.Current.CurrentCulture.DateTimeFormat.ShortDatePattern.Split(LocaleService.Current.CurrentCulture.DateTimeFormat.DateSeparator);
            if (parts.Length != 3)
            {
                parts = new[] { "dd", "MM", "yyyy" };
            }

            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].StartsWith("d", StringComparison.OrdinalIgnoreCase))
                {
                    dayPosition = i * 2;
                }
                else if (parts[i].StartsWith("M", StringComparison.OrdinalIgnoreCase))
                {
                    monthPosition = i * 2;
                }
                else if (parts[i].StartsWith("y", StringComparison.OrdinalIgnoreCase))
                {
                    yearPosition = i * 2;
                }
            }

            var first = dayPosition == 0
                ? DayHost
                : monthPosition == 0
                ? MonthHost
                : YearHost;

            var last = dayPosition == 4
                ? DayHost
                : monthPosition == 4
                ? MonthHost
                : YearHost;

            first.Padding = new Thickness(4, 140, 0, 140);
            last.Padding = new Thickness(0, 140, 4, 140);

            Grid.SetColumn(DayHost, dayPosition);
            Grid.SetColumn(DayOverlay, dayPosition);

            Grid.SetColumn(MonthHost, monthPosition);
            Grid.SetColumn(MonthOverlay, monthPosition);

            Grid.SetColumn(YearHost, yearPosition);
            Grid.SetColumn(YearOverlay, yearPosition);

            LayoutRoot.ColumnDefinitions[dayPosition].Width = new GridLength(78, GridUnitType.Pixel);
            LayoutRoot.ColumnDefinitions[monthPosition].Width = new GridLength(1, GridUnitType.Star);
            LayoutRoot.ColumnDefinitions[yearPosition].Width = new GridLength(78, GridUnitType.Pixel);

            DayHost.TabIndex = dayPosition / 2;
            MonthHost.TabIndex = monthPosition / 2;
            YearHost.TabIndex = yearPosition / 2;

            var selectedDay = date?.Day ?? 1;
            var selectedMonth = date?.Month ?? 1;
            var selectedYear = date?.Year ?? 0;

            YearHost.ItemsSource = GetYears();
            YearHost.SelectedItem = selectedYear;

            UpdateMonths(selectedYear);
            MonthHost.SelectedItem = selectedMonth;

            UpdateDays(selectedYear, selectedMonth);
            DayHost.SelectedItem = selectedDay;

            if (primaryRule != null)
            {
                PrivacyInfo.Text = primaryRule is UserPrivacySettingRuleAllowContacts
                ? Strings.EditProfileBirthdayInfoContacts
                : Strings.EditProfileBirthdayInfo;
            }
            else
            {
                PrivacyInfo.Visibility = Visibility.Collapsed;
            }

            Title = Strings.EditProfileBirthdayTitle;
            PrimaryButtonText = Strings.EditProfileBirthdayButton;
            SecondaryButtonText = Strings.Cancel;

            DefaultButton = ContentDialogButton.Primary;
        }

        private void DayHost_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var scrollingHost = DayHost.GetScrollViewer();
            if (scrollingHost != null)
            {
                scrollingHost.ViewChanged += DayHost_ViewChanged;
            }

            DayHost_SelectionChanged(sender, null);
        }

        private void MonthHost_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var scrollingHost = MonthHost.GetScrollViewer();
            if (scrollingHost != null)
            {
                scrollingHost.ViewChanged += MonthHost_ViewChanged;
            }

            MonthHost_SelectionChanged(sender, null);
        }

        private void YearHost_Loaded(object sender, RoutedEventArgs e)
        {
            var scrollingHost = YearHost.GetScrollViewer();
            if (scrollingHost != null)
            {
                scrollingHost.ViewChanged += YearHost_ViewChanged;
            }

            YearHost_SelectionChanged(sender, null);
        }

        private void DayHost_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (e.IsIntermediate || sender is not ScrollViewer scrollingHost)
            {
                return;
            }

            var index = (int)(scrollingHost.VerticalOffset / 40);
            if (index >= 0 && index < DayHost.Items.Count)
            {
                DayHost.SelectionChanged -= DayHost_SelectionChanged;
                DayHost.SelectedIndex = index;
                DayHost.SelectionChanged += DayHost_SelectionChanged;

                SelectionChanged(false, false);
            }
        }

        private void MonthHost_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (e.IsIntermediate || sender is not ScrollViewer scrollingHost)
            {
                return;
            }

            var index = (int)(scrollingHost.VerticalOffset / 40);
            if (index >= 0 && index < MonthHost.Items.Count)
            {
                MonthHost.SelectionChanged -= MonthHost_SelectionChanged;
                MonthHost.SelectedIndex = index;
                MonthHost.SelectionChanged += MonthHost_SelectionChanged;

                SelectionChanged(updateMonth: false);
            }
        }

        private void YearHost_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (e.IsIntermediate || sender is not ScrollViewer scrollingHost)
            {
                return;
            }

            var index = (int)(scrollingHost.VerticalOffset / 40);
            if (index >= 0 && index < YearHost.Items.Count)
            {
                YearHost.SelectionChanged -= YearHost_SelectionChanged;
                YearHost.SelectedIndex = index;
                YearHost.SelectionChanged += YearHost_SelectionChanged;

                SelectionChanged();
            }
        }

        private void DayHost_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var scrollingHost = DayHost.GetScrollViewer();
            if (scrollingHost != null && DayHost.SelectedIndex != -1)
            {
                if (e == null)
                {
                    scrollingHost.ViewChanged -= DayHost_ViewChanged;
                    DayHost.ScrollIntoView(DayHost.SelectedItem);
                    scrollingHost.UpdateLayout();
                    scrollingHost.ViewChanged += DayHost_ViewChanged;
                }

                scrollingHost?.ChangeView(null, DayHost.SelectedIndex * 40, null, e == null);

                SelectionChanged(false, false);
            }
        }

        private void MonthHost_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var scrollingHost = MonthHost.GetScrollViewer();
            if (scrollingHost != null && MonthHost.SelectedIndex != -1)
            {
                if (e == null)
                {
                    scrollingHost.ViewChanged -= MonthHost_ViewChanged;
                    MonthHost.ScrollIntoView(MonthHost.SelectedItem);
                    scrollingHost.UpdateLayout();
                    scrollingHost.ViewChanged += MonthHost_ViewChanged;
                }

                scrollingHost?.ChangeView(null, MonthHost.SelectedIndex * 40, null, e == null);

                SelectionChanged(updateMonth: false);
            }
        }

        private void YearHost_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var scrollingHost = YearHost.GetScrollViewer();
            if (scrollingHost != null && YearHost.SelectedIndex != -1)
            {
                if (e == null)
                {
                    scrollingHost.ViewChanged -= YearHost_ViewChanged;
                    YearHost.ScrollIntoView(YearHost.SelectedItem);
                    scrollingHost.UpdateLayout();
                    scrollingHost.ViewChanged += YearHost_ViewChanged;
                }

                scrollingHost?.ChangeView(null, YearHost.SelectedIndex * 40, null, e == null);

                SelectionChanged();
            }
        }

        private void SelectionChanged(bool updateDay = true, bool updateMonth = true)
        {
            if (YearHost.SelectedItem is int selectedYear
                && MonthHost.SelectedItem is int selectedMonth
                && DayHost.SelectedItem is int selectedDay)
            {
                MonthHost.SelectionChanged -= MonthHost_SelectionChanged;
                DayHost.SelectionChanged -= DayHost_SelectionChanged;

                if (updateMonth)
                {
                    UpdateMonths(selectedYear);
                    MonthHost.SelectedItem = selectedMonth = Math.Min(selectedMonth, _months.Count);
                }

                if (updateDay)
                {
                    UpdateDays(selectedYear, selectedMonth);
                    DayHost.SelectedItem = selectedDay = Math.Min(selectedDay, _days.Count);
                }

                MonthHost.SelectionChanged += MonthHost_SelectionChanged;
                DayHost.SelectionChanged += DayHost_SelectionChanged;

                Value = new Birthdate(selectedDay, selectedMonth, selectedYear);
            }
        }

        private void DayHost_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue || args.Item is not int value)
            {
                return;
            }

            args.ItemContainer.Content = value;
            args.ItemContainer.HorizontalContentAlignment = HorizontalAlignment.Center;
            args.Handled = true;
        }

        private void MonthHost_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue || args.Item is not int value)
            {
                return;
            }

            args.ItemContainer.Content = LocaleService.Current.CurrentCulture.DateTimeFormat.GetMonthName(value);
            args.Handled = true;
        }

        private void YearHost_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue || args.Item is not int value)
            {
                return;
            }

            args.ItemContainer.Content = value == 0 ? "\u2014" : value;
            args.ItemContainer.HorizontalContentAlignment = HorizontalAlignment.Center;
            args.Handled = true;
        }

        private IList<int> GetYears()
        {
            var items = new List<int>(101);
            var today = DateTime.Today.Year;

            for (int i = 0; i <= 100; i++)
            {
                items.Add(today - 100 + i);
            }

            items.Add(0);
            return items;
        }

        private void UpdateMonths(int year)
        {
            var count = year == DateTime.Today.Year ? DateTime.Today.Month : 12;
            if (count < _months.Count)
            {
                for (int i = _months.Count; i > count; i--)
                {
                    _months.RemoveAt(_months.Count - 1);
                }
            }
            else if (count > _months.Count)
            {
                for (int i = _months.Count; i < count; i++)
                {
                    _months.Add(_months.Count + 1);
                }
            }

            MonthHost.ItemsSource ??= _months;
        }

        private void UpdateDays(int year, int month)
        {
            if (year == 0)
            {
                // Always leap year if year is unselected
                year = 2020;
            }

            var count = year == DateTime.Today.Year && month == DateTime.Today.Month ? DateTime.Today.Day : DateTime.DaysInMonth(year, month);
            if (count < _days.Count)
            {
                for (int i = _days.Count; i > count; i--)
                {
                    _days.RemoveAt(_days.Count - 1);
                }
            }
            else if (count > _days.Count)
            {
                for (int i = _days.Count; i < count; i++)
                {
                    _days.Add(_days.Count + 1);
                }
            }

            DayHost.ItemsSource ??= _days;
        }

        public Birthdate Value { get; private set; }

        public bool ShowPrivacySettings { get; private set; }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (Value == null)
            {
                args.Cancel = true;
            }
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void PrivacyInfo_Click(object sender, TextUrlClickEventArgs e)
        {
            ShowPrivacySettings = true;
            Hide(ContentDialogResult.Secondary);
        }
    }
}
