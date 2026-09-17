//
// Copyright (c) Fela Ameghino 2015-2026
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
using Telegram.Views.Chats;
using Telegram.Views.Profile;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views
{
    public sealed partial class RevenuePage : HostedPage, INavigablePage
    {
        public RevenueViewModel ViewModel => DataContext as RevenueViewModel;

        private CompositionPropertySet _properties;

        private readonly ScrollViewerIncrementalLoader _loader;

        public RevenuePage()
        {
            InitializeComponent();
            InitializeScrolling();

            Title = Strings.Monetization;

            _loader = new ScrollViewerIncrementalLoader(ScrollingHost);
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

            if (ViewModel.SelectedItem is RevenueTabItem tab)
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
            if (e.PropertyName.Equals("SharedCount") && ViewModel.SelectedItem is RevenueTabItem tab)
            {
                MediaFrame.Navigate(tab.Type, null, new SuppressNavigationTransitionInfo());
            }
        }

        private long _itemsSourceToken;

        private void OnNavigating(object sender, NavigatingCancelEventArgs e)
        {
            // Ahead of the page that is leaving nulling its own source: the callback has to be
            // gone before that, or the teardown arrives as a change.
            _loader.ItemsSource = null;

            if (TryGetScrollingHost(MediaFrame.Content, out ListViewBase scrollingHost))
            {
                scrollingHost.UnregisterPropertyChangedCallback(ItemsControl.ItemsSourceProperty, ref _itemsSourceToken);
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

            if (!TryGetScrollingHost(e.Content, out ListViewBase scrollingHost))
            {
                return;
            }

            _loader.ItemsSource = scrollingHost.ItemsSource;
            scrollingHost.RegisterPropertyChangedCallback(ItemsControl.ItemsSourceProperty, OnItemsSourceChanged, ref _itemsSourceToken);
        }

        private void OnItemsSourceChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (sender is ListViewBase scrollingHost)
            {
                _loader.ItemsSource = scrollingHost.ItemsSource;
            }
        }

        private bool TryGetScrollingHost(object content, out ListViewBase scrollingHost)
        {
            if (content is Page page)
            {
                scrollingHost = page.FindName("ScrollingHost") as ListViewBase;
                return scrollingHost != null;
            }

            scrollingHost = null;
            return false;
        }

        private void ProfileHeader_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _properties.InsertScalar("ActualHeight", ProfileHeader.ActualSize.Y + 16);
            ViewModel.HeaderHeight = Math.Max(e.NewSize.Height, 48 + 10);
            MediaFrame.MinHeight = ScrollingHost.ActualHeight + e.NewSize.Height - 48;
        }

        private void Header_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is RevenueTabItem page && page.Type != MediaFrame.Content?.GetType())
            {
                MediaFrame.Navigate(page.Type, null, new SuppressNavigationTransitionInfo());
            }
        }

        private void Menu_ContextRequested(object sender, RoutedEventArgs e)
        {

        }
    }
}
