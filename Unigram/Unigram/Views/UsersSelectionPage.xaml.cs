using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Template10.Utils;
using Unigram.Common;
using Unigram.Core.Dependency;
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

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class UsersSelectionPage : Page
    {
        public UsersSelectionViewModel ViewModel => DataContext as UsersSelectionViewModel;

        public UsersSelectionPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType(UnigramNavigationService.ViewModels.Dequeue());

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
    }
}
