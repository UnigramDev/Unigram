//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.ComponentModel;
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsStoragePage : HostedPage
    {
        public SettingsStorageViewModel ViewModel => DataContext as SettingsStorageViewModel;

        public SettingsStoragePage()
        {
            InitializeComponent();
            Title = Strings.StorageUsage;

            InitializeKeepMediaTicks();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged += OnPropertyChanged;

            UpdateTotalBytes(ViewModel.TotalBytes, ViewModel.SystemCapacity, ViewModel.SystemFreeSpace);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.TotalBytes))
            {
                UpdateTotalBytes(ViewModel.TotalBytes, ViewModel.SystemCapacity, ViewModel.SystemFreeSpace);
            }
        }

        private void UpdateTotalBytes(long totalBytes, ulong totalDeviceSize, ulong totalDeviceFreeSize)
        {
            if (totalBytes < 0)
            {
                SizeLabel.Text = string.Empty;
                UnitLabel.Text = string.Empty;

                TextBlockHelper.SetMarkdown(Subtitle, Strings.StorageUsageCalculating);

                FindName(nameof(Ring));
            }
            else
            {
                var readable = FileSizeConverter.Convert(totalBytes, true).Split(' ');

                var percent = totalDeviceSize <= 0 ? 0 : (float)totalBytes / totalDeviceSize;
                var usedPercent = totalDeviceFreeSize <= 0 || totalDeviceSize <= 0 ? 0 : (float)(totalDeviceSize - totalDeviceFreeSize) / totalDeviceSize;

                if (percent < 0.01f)
                {
                    TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.StorageUsageTelegramLess, Formatter.Percent(percent)));
                }
                else
                {
                    TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.StorageUsageTelegram, Formatter.Percent(percent)));
                }

                SizeLabel.Text = readable[0];
                UnitLabel.Text = readable[1];

                UnloadObject(Ring);
            }
        }

        private void InitializeKeepMediaTicks()
        {
            int j = 0;
            for (int i = 0; i < 4; i++)
            {
                var label = new TextBlock { Text = ConvertKeepMediaTick(i), TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch, Style = BootStrapper.Current.Resources["InfoCaptionTextBlockStyle"] as Style };
                Grid.SetColumn(label, j);

                KeepMediaTicks.ColumnDefinitions.Add(1, GridUnitType.Auto);

                if (i < 3)
                {
                    KeepMediaTicks.ColumnDefinitions.Add(1, GridUnitType.Star);
                }

                KeepMediaTicks.Children.Add(label);
                j += 2;
            }

            Grid.SetColumnSpan(KeepMedia, KeepMediaTicks.ColumnDefinitions.Count);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ProfileCell content)
            {
                if (args.Item is StorageStatisticsByChat statistics && statistics.ByFileType == null)
                {
                    args.ItemContainer.Opacity = (10 - args.ItemIndex) / 10d;
                    content.ShowHideSkeleton(true);
                }
                else
                {
                    args.ItemContainer.Opacity = 1;
                    content.ShowHideSkeleton(false);

                    content.UpdateStatisticsByChat(ViewModel.ClientService, args, OnContainerContentChanging);
                }
            }
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is StorageStatisticsByChat { ByFileType: not null } statistics)
            {
                ViewModel.Clear(statistics);
            }
        }

        #region Binding

        private string ConvertTtl(int days)
        {
            if (days < 1)
            {
                return Strings.KeepMediaForever;
            }
            else if (days < 7)
            {
                return Locale.Declension(Strings.R.Days, days);
            }
            else if (days < 30)
            {
                return Locale.Declension(Strings.R.Weeks, 1);
            }

            return Locale.Declension(Strings.R.Months, 1);
        }

        private bool ConvertEnabled(object value)
        {
            return value != null;
        }

        private int ConvertKeepMedia(int value)
        {
            switch (Math.Max(0, Math.Min(30, value)))
            {
                case 0:
                default:
                    return 3;
                case 3:
                    return 0;
                case 7:
                    return 1;
                case 30:
                    return 2;
            }
        }

        private void ConvertKeepMediaBack(double value)
        {
            switch (value)
            {
                case 0:
                    ViewModel.KeepMedia = 3;
                    break;
                case 1:
                    ViewModel.KeepMedia = 7;
                    break;
                case 2:
                    ViewModel.KeepMedia = 30;
                    break;
                case 3:
                    ViewModel.KeepMedia = 0;
                    break;
            }
        }

        private string ConvertKeepMediaTick(double value)
        {
            var days = 0;
            switch (value)
            {
                case 0:
                    days = 3;
                    break;
                case 1:
                    days = 7;
                    break;
                case 2:
                    days = 30;
                    break;
                case 3:
                    days = 0;
                    break;
            }

            if (days < 1)
            {
                return Strings.KeepMediaForever;
            }
            else if (days < 7)
            {
                return Locale.Declension(Strings.R.Days, days);
            }
            else if (days < 30)
            {
                return Locale.Declension(Strings.R.Weeks, 1);
            }

            return Locale.Declension(Strings.R.Months, 1);
        }

        #endregion

        private void StorageChartItem_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox check || check.DataContext is not StorageChartItem item)
            {
                return;
            }

            var index = Chart.Items.IndexOf(item);
            if (index < 0)
            {
                return;
            }

            if (item.IsVisible && Chart.Items.Except(new[] { item }).Any(x => x.IsVisible))
            {
                item.IsVisible = false;
                check.IsChecked = false;

                Chart.Update(index, item.IsVisible);
            }
            else if (!item.IsVisible)
            {
                item.IsVisible = true;
                check.IsChecked = true;

                Chart.Update(index, item.IsVisible);
            }
            else
            {
                VisualUtilities.ShakeView(check);
            }

            var size = Chart.Items.Where(x => x.IsVisible).Sum(x => x.TotalBytes);
            var formatted = FileSizeConverter.Convert(size, true);
            var readable = formatted.Split(' ');

            SizeLabel.Text = readable[0];
            UnitLabel.Text = readable[1];

            ClearSize.Text = formatted;
        }

        private void Menu_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();
            if (ViewModel.StatisticsFast == null)
            {
                flyout.CreateFlyoutItem(ViewModel.ClearDatabase, Strings.Loading, Icons.Delete, destructive: true);
            }
            else
            {
                flyout.CreateFlyoutItem(ViewModel.ClearDatabase, Strings.ClearLocalDatabase, Icons.Delete, destructive: true);
            }
            flyout.ShowAt(sender as UIElement, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }
    }
}
