using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Unigram.ViewModels;
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
            InitializeComponent();
        }

        public void Attach()
        {
            ViewModel.SelectedItems.CollectionChanged += SelectedItems_CollectionChanged;
        }

        private void SelectedItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                foreach (var item in e.NewItems)
                {
                    List.SelectedItems.Add(item);
                }
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                foreach (var item in e.OldItems)
                {
                    List.SelectedItems.Remove(item);
                }
            }
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems != null)
            {
                foreach (var item in e.AddedItems)
                {
                    ViewModel.SelectedItems.Add(item as TLUser);
                }
            }

            if (e.RemovedItems != null)
            {
                foreach (var item in e.RemovedItems)
                {
                    ViewModel.SelectedItems.Remove(item as TLUser);
                }
            }
        }

        private void TagsTextBox_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ScrollingHost.ChangeView(null, ScrollingHost.ScrollableHeight, null);
        }

        #region Binding

        private Visibility ConvertMaximum(int maximum)
        {
            return maximum == int.MaxValue ? Visibility.Collapsed : Visibility.Visible;
        }

        #endregion
    }
}
