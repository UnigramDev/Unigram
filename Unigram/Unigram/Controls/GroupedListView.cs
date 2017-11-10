using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Common;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Unigram.Controls
{
    public class GroupedListView : SelectListView
    {
        public ScrollViewer ScrollingHost { get; private set; }
        public ItemsStackPanel ItemsStack { get; private set; }

        public GroupedListView()
        {
            Loaded += OnLoaded;

            RegisterPropertyChangedCallback(ItemsSourceProperty, OnItemsSourceChanged);
        }

        private async void OnItemsSourceChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (IncrementalSource != null && IncrementalSource.HasMoreItems && !_isAlreadyLoading)
            {
                _isAlreadyLoading = true;
                await IncrementalSource.LoadMoreItemsAsync(0);
                _isAlreadyLoading = false;
            }
        }

        protected override void OnApplyTemplate()
        {
            ScrollingHost = (ScrollViewer)GetTemplateChild("ScrollViewer");
            ScrollingHost.ViewChanged += OnViewChanged;

            base.OnApplyTemplate();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            var panel = ItemsPanelRoot as ItemsStackPanel;
            if (panel != null)
            {
                ItemsStack = panel;
                ItemsStack.SizeChanged += Panel_SizeChanged;
            }

            if (IncrementalSource != null && IncrementalSource.HasMoreItems && !_isAlreadyLoading)
            {
                _isAlreadyLoading = true;
                await IncrementalSource.LoadMoreItemsAsync(0);
                _isAlreadyLoading = false;
            }
        }

        private async void Panel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ScrollingHost.ScrollableHeight < 120)
            {
                if (IncrementalSource != null && IncrementalSource.HasMoreItems && !_isAlreadyLoading)
                {
                    _isAlreadyLoading = true;
                    await IncrementalSource.LoadMoreItemsAsync(0);
                    _isAlreadyLoading = false;
                }
            }
        }

        private async void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (ScrollingHost.VerticalOffset / ScrollingHost.ScrollableHeight >= 0.9)
            {
                if (IncrementalSource != null && IncrementalSource.HasMoreItems && !_isAlreadyLoading && !e.IsIntermediate)
                {
                    _isAlreadyLoading = true;
                    await IncrementalSource.LoadMoreItemsAsync(0);
                    _isAlreadyLoading = false;
                }
            }
        }

        public IGroupSupportIncrementalLoading IncrementalSource
        {
            get
            {
                return ViewSource?.Source as IGroupSupportIncrementalLoading;
            }
        }

        public CollectionViewSource ViewSource
        {
            get { return (CollectionViewSource)GetValue(ViewSourceProperty); }
            set { SetValue(ViewSourceProperty, value); }
        }

        public static readonly DependencyProperty ViewSourceProperty =
            DependencyProperty.Register("ViewSource", typeof(CollectionViewSource), typeof(GroupedListView), new PropertyMetadata(null));

        private bool _isAlreadyLoading;
    }
}
