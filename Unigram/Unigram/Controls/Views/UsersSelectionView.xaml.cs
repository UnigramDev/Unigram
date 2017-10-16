using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Unigram.ViewModels;
using Unigram.ViewModels.Settings;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls.Views
{
    public sealed partial class UsersSelectionView : Grid
    {
        public UsersSelectionViewModel ViewModel => DataContext as UsersSelectionViewModel;

        public UsersSelectionView()
        {
            if (Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                DataContext = new SettingsBlockUserViewModel(null, null, null);
            }

            InitializeComponent();

            var observable = Observable.FromEventPattern<TextChangedEventArgs>(SearchField, "TextChanged");
            var throttled = observable.Throttle(TimeSpan.FromMilliseconds(Constants.TypingTimeout)).ObserveOnDispatcher().Subscribe(x =>
            {
                if (string.IsNullOrWhiteSpace(SearchField.Text))
                {
                    ViewModel.Search.Clear();
                    return;
                }

                ViewModel.SearchAsync(SearchField.Text);
            });
        }

        public void Attach()
        {
            ViewModel.SelectedItems.CollectionChanged += SelectedItems_CollectionChanged;
        }

        private void SelectedItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (ViewModel.SelectionMode == ListViewSelectionMode.None)
            {
                return;
            }

            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                List.SelectionChanged -= ListView_SelectionChanged;
                foreach (var item in e.NewItems)
                {
                    var listItem = List.Items?.SingleOrDefault(li => li is TLUser user && (item as TLUser).Id == user.Id);
                    if (listItem != null)
                    {
                        List.SelectedItems.Add(item);
                    }
                }
                List.SelectionChanged += ListView_SelectionChanged;
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                List.SelectionChanged -= ListView_SelectionChanged;
                foreach (var item in e.OldItems)
                {
                    var listItem = List.Items?.SingleOrDefault(li => li is TLUser user && (item as TLUser).Id == user.Id);
                    if (listItem != null)
                    {
                        List.SelectedItems.Remove(item);
                    }
                }
                List.SelectionChanged += ListView_SelectionChanged;
            }
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel.SelectionMode == ListViewSelectionMode.None)
            {
                return;
            }

            if (e.AddedItems != null)
            {
                foreach (var item in e.AddedItems)
                {
                    if (item is TLUser user && ViewModel.SelectedItems.All(selectedUser => selectedUser.Id != user.Id))
                    {
                        ViewModel.SelectedItems.Add(user);
                    }
                }
            }

            if (e.RemovedItems != null)
            {
                foreach (var item in e.RemovedItems)
                {
                    if (item is TLUser user && ViewModel.SelectedItems.Any(selectedUser => selectedUser.Id == user.Id))
                    {
                        ViewModel.SelectedItems.Remove(user);
                    }
                }
            }
        }

        private void SearchListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel.SelectionMode == ListViewSelectionMode.None)
            {
                return;
            }

            if (e.AddedItems != null)
            {
                foreach (var item in e.AddedItems)
                {
                    if (item is TLUser user)
                    {
                        ViewModel.SelectedItems.Add(user);
                    }
                }
            }
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (ViewModel.SelectionMode == ListViewSelectionMode.None)
            {
                ViewModel.SelectedItems.Clear();
                ViewModel.SelectedItems.Add(e.ClickedItem as TLUser);
                ViewModel.SendCommand.Execute();
            }
        }

        private void SearchField_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchList.Visibility = string.IsNullOrEmpty(SearchField.Text) ? Visibility.Collapsed : Visibility.Visible;
        }

        private void TagsTextBox_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ScrollingHost.ChangeView(null, ScrollingHost.ScrollableHeight, null);
        }

        #region Binding

        private Visibility ConvertMaximum(int maximum, bool infinite)
        {
            return (maximum == int.MaxValue && infinite) || maximum == 1 ? Visibility.Collapsed : Visibility.Visible;
        }

        #endregion

        public object Header { get; set; }

        public DataTemplate HeaderTemplate { get; set; }
    }
}
