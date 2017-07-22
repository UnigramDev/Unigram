using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.ViewModels.Channels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls.Views
{
    public sealed partial class ChannelAdminLogFilterContentView : UserControl
    {
        public ChannelAdminLogFilterViewModel ViewModel => DataContext as ChannelAdminLogFilterViewModel;

        public ChannelAdminLogFilterContentView()
        {
            InitializeComponent();

            DataContextChanged += (s, args) =>
            {
                Bindings.Update();
            };

            foreach (CheckBox check in EventFilters.Children)
            {
                check.IsChecked = true;
                check.Checked += Event_Toggled;
                check.Unchecked += Event_Toggled;
            }
        }

        private void Event_Toggled(object sender, RoutedEventArgs e)
        {
            if (EventFilters == null)
            {
                return;
            }

            var all = EventFilters.Children.OfType<CheckBox>().All(x => x.IsChecked == true);
            var none = EventFilters.Children.OfType<CheckBox>().All(x => x.IsChecked == false);

            FieldAllEvents.Checked -= AllEvents_Toggled;
            FieldAllEvents.Unchecked -= AllEvents_Toggled;

            FieldAllEvents.IsChecked = all ? true : none ? new bool?(false) : null;
            FieldAllEvents.Checked += AllEvents_Toggled;
            FieldAllEvents.Unchecked += AllEvents_Toggled;

            var previous = new bool?();

            foreach (CheckBox check in EventFilters.Children)
            {
                previous = check.IsChecked;
            }
        }

        private void AllEvents_Toggled(object sender, RoutedEventArgs e)
        {
            if (EventFilters == null)
            {
                return;
            }

            foreach (CheckBox check in EventFilters.Children)
            {
                check.Checked -= Event_Toggled;
                check.Unchecked -= Event_Toggled;

                check.IsChecked = FieldAllEvents.IsChecked == true;
                check.Checked += Event_Toggled;
                check.Unchecked += Event_Toggled;
            }
        }
    }
}
