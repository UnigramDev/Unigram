using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Controls;
using Telegram.ViewModels.Business;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Business.Popups
{
    public sealed partial class ChooseHoursPopup : ContentPopup
    {
        private ObservableCollection<BusinessHoursRange> _ranges;

        public ChooseHoursPopup(BusinessDay day)
        {
            InitializeComponent();

            Title = day.Name;

            _ranges = new ObservableCollection<BusinessHoursRange>(day.Ranges
                .Select(x => new BusinessHoursRange(x.Start, x.End)));

            Open.IsChecked = day.Ranges.Count > 0;

            if (Open.IsChecked is true)
            {
                ScrollingHost.ItemsSource = _ranges;
                Footer.Visibility = Visibility.Visible;
            }
            else
            {
                ScrollingHost.ItemsSource = null;
                Footer.Visibility = Visibility.Collapsed;
            }

            PrimaryButtonText = Strings.OK;
            SecondaryButtonText = Strings.Cancel;
        }

        public IList<BusinessHoursRange> Ranges => _ranges;

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is StackPanel content && args.Item is BusinessHoursRange range)
            {
                var start = content.Children[0] as TimePicker;
            }
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (Open.IsChecked is true)
            {
                ScrollingHost.ItemsSource = _ranges;
                Footer.Visibility = Visibility.Visible;
            }
            else
            {
                ScrollingHost.ItemsSource = null;
                Footer.Visibility = Visibility.Collapsed;
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (_ranges.Count > 0)
            {
                var last = _ranges[^1];
                var start = last.End + TimeSpan.FromMinutes(1);
                var end = start + TimeSpan.FromHours(8);

                if (end.TotalHours > 24)
                {
                    end = new TimeSpan(23, 59, 0);
                }

                _ranges.Add(new BusinessHoursRange(start, end));
            }
            else
            {
                _ranges.Add(new BusinessHoursRange(9, 18));
            }
        }
    }
}
