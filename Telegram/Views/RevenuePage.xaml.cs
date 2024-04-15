//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.ComponentModel;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Profile;
using Telegram.Views.Chats;
using Telegram.Views.Profile;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views
{
    public sealed partial class RevenuePage : HostedPage, INavigablePage
    {
        public RevenueViewModel ViewModel => DataContext as RevenueViewModel;

        private CompositionPropertySet _properties;

        public RevenuePage()
        {
            InitializeComponent();
            InitializeScrolling();

            Title = Strings.Monetization;
        }

        private void InitializeScrolling()
        {
            var properties = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(ScrollingHost);
            var visual = ElementComposition.GetElementVisual(HeaderPanel);
            var border = ElementComposition.GetElementVisual(CardBackground);
            var clipper = ElementComposition.GetElementVisual(ClipperBackground);

            ElementCompositionPreview.SetIsTranslationEnabled(HeaderPanel, true);

            ProfileHeader.Height = 48 - 16;

            _properties = visual.Compositor.CreatePropertySet();
            _properties.InsertScalar("ActualHeight", ProfileHeader.ActualSize.Y + 16);

            var translation = visual.Compositor.CreateExpressionAnimation(
                "properties.ActualHeight > 16 ? scrollViewer.Translation.Y > -properties.ActualHeight ? 0 : -scrollViewer.Translation.Y - properties.ActualHeight : -scrollViewer.Translation.Y");
            translation.SetReferenceParameter("scrollViewer", properties);
            translation.SetReferenceParameter("properties", _properties);

            var fadeOut = visual.Compositor.CreateExpressionAnimation(
                "properties.ActualHeight > 16 ? scrollViewer.Translation.Y > -(properties.ActualHeight - 16) ? 1 : 1 - ((-scrollViewer.Translation.Y - (properties.ActualHeight - 16)) / 16) : 0");
            fadeOut.SetReferenceParameter("scrollViewer", properties);
            fadeOut.SetReferenceParameter("properties", _properties);

            var fadeIn = visual.Compositor.CreateExpressionAnimation(
                "properties.ActualHeight > 16 ? scrollViewer.Translation.Y > -(properties.ActualHeight - 16) ? 0 : ((-scrollViewer.Translation.Y - (properties.ActualHeight - 16)) / 16) : 1");
            fadeIn.SetReferenceParameter("scrollViewer", properties);
            fadeIn.SetReferenceParameter("properties", _properties);

            visual.StartAnimation("Translation.Y", translation);

            border.StartAnimation("Opacity", fadeOut);
            clipper.StartAnimation("Opacity", fadeIn);
        }

        public void OnBackRequested(BackRequestedRoutedEventArgs args)
        {
            if (MediaFrame.Content is ProfileTabPage tabPage)
            {
                tabPage.OnBackRequested(args);
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged += OnPropertyChanged;

            if (ViewModel.SelectedItem is ProfileTabItem tab)
            {
                MediaFrame.Navigate(tab.Type, null, new SuppressNavigationTransitionInfo());
            }

            if (ViewModel.ClientService.TryGetChat((long)ViewModel.NavigationService.CurrentPageParam, out Chat chat))
            {
                Title = chat.Title;
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("SharedCount") && ViewModel.SelectedItem is ProfileTabItem tab)
            {
                MediaFrame.Navigate(tab.Type, null, new SuppressNavigationTransitionInfo());
            }
        }

        private long _itemsSourceToken;
        private long _selectionModeToken;

        private void OnNavigating(object sender, NavigatingCancelEventArgs e)
        {
            if (MediaFrame.Content is ProfileTabPage tabPage)
            {
                tabPage.ScrollingHost.UnregisterPropertyChangedCallback(ItemsControl.ItemsSourceProperty, ref _itemsSourceToken);
                tabPage.ScrollingHost.UnregisterPropertyChangedCallback(ListViewBase.SelectionModeProperty, ref _selectionModeToken);
            }
        }

        private void OnNavigated(object sender, NavigationEventArgs e)
        {
            if (e.Content is ChatStatisticsPage statistics)
            {
                statistics.DataContext = ViewModel.Statistics;
            }
            else if (e.Content is ChatBoostsPage boosts)
            {
                boosts.DataContext = ViewModel.Boosts;
            }
            else if (e.Content is ChatRevenuePage revenue)
            {
                revenue.DataContext = ViewModel.Revenue;
            }

            if (e.Content is not ProfileTabPage tabPage)
            {
                return;
            }

            if (tabPage.ScrollingHost.ItemsSource != null)
            {
                LoadMore(tabPage.ScrollingHost);
            }
            else
            {
                tabPage.ScrollingHost.RegisterPropertyChangedCallback(ItemsControl.ItemsSourceProperty, OnItemsSourceChanged, ref _itemsSourceToken);
            }
        }

        private void OnItemsSourceChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (MediaFrame.Content is not ProfileTabPage tabPage || tabPage.ScrollingHost is not ListViewBase scrollingHost)
            {
                return;
            }

            LoadMore(scrollingHost);
        }

        private void ProfileHeader_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _properties.InsertScalar("ActualHeight", ProfileHeader.ActualSize.Y + 16);
            ViewModel.HeaderHeight = Math.Max(e.NewSize.Height, 48 + 10);
            MediaFrame.MinHeight = ScrollingHost.ActualHeight + e.NewSize.Height - 48;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            MediaFrame.MinHeight = Header.ActualHeight + e.NewSize.Height - 48;

            if (MediaFrame.Content is not ProfileTabPage tabPage || tabPage.ScrollingHost is not ListViewBase scrollingHost)
            {
                return;
            }

            LoadMore(scrollingHost);
        }

        private void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (MediaFrame.Content is not ProfileTabPage tabPage || tabPage.ScrollingHost is not ListViewBase scrollingHost)
            {
                return;
            }

            LoadMore(scrollingHost);
        }

        private bool _loadingMore;

        private async void LoadMore(ListViewBase scrollingHost)
        {
            if (_loadingMore)
            {
                return;
            }

            _loadingMore = true;

            uint loadedMore = 0;
            int lastCacheIndex = scrollingHost.ItemsPanelRoot switch
            {
                ItemsStackPanel stackPanel => stackPanel.LastCacheIndex,
                ItemsWrapGrid wrapGrid => wrapGrid.LastCacheIndex,
                _ => -1
            };

            var needsMore = lastCacheIndex == scrollingHost.Items.Count - 1;
            needsMore |= scrollingHost.ActualHeight < ScrollingHost.ActualHeight;

            if (needsMore && scrollingHost.ItemsSource is ISupportIncrementalLoading supportIncrementalLoading && supportIncrementalLoading.HasMoreItems)
            {
                var result = await supportIncrementalLoading.LoadMoreItemsAsync(50);
                loadedMore = result.Count;
            }

            _loadingMore = false;

            if (loadedMore > 0)
            {
                LoadMore(scrollingHost);
            }
        }

        private void Header_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ProfileTabItem page && page.Type != MediaFrame.Content?.GetType())
            {
                MediaFrame.Navigate(page.Type, null, new SuppressNavigationTransitionInfo());
            }
        }

        private void Menu_ContextRequested(object sender, RoutedEventArgs e)
        {

        }
    }
}
