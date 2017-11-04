using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class OrientableListView : ZoomableGridView
    {
        private bool _needUpdate;

        public ScrollViewer ScrollingHost { get; private set; }

        public OrientableListView()
        {
            DefaultStyleKey = typeof(OrientableListView);
            ChoosingItemContainer += OnChoosingItemContainer;
        }

        protected override void OnApplyTemplate()
        {
            ScrollingHost = (ScrollViewer)GetTemplateChild("ScrollViewer");

            base.OnApplyTemplate();
        }

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (_needUpdate)
            {
                var root = ItemsPanelRoot as ItemsStackPanel;
                if (root != null)
                {
                    root.Orientation = IsHorizontal ? Orientation.Horizontal : Orientation.Vertical;
                    _needUpdate = false;
                }
            }
        }

        #region IsHorizontal

        public bool IsHorizontal
        {
            get { return (bool)GetValue(IsHorizontalProperty); }
            set { SetValue(IsHorizontalProperty, value); }
        }

        public static readonly DependencyProperty IsHorizontalProperty =
            DependencyProperty.Register("IsHorizontal", typeof(bool), typeof(OrientableListView), new PropertyMetadata(false, OnIsHorizontalChanged));

        private static void OnIsHorizontalChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((OrientableListView)d).OnIsHorizontalChanged((bool)e.NewValue, (bool)e.OldValue);
        }

        #endregion

        private void OnIsHorizontalChanged(bool newValue, bool oldValue)
        {
            ScrollViewer.SetVerticalScrollBarVisibility(this, newValue ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto);
            ScrollViewer.SetVerticalScrollMode(this, newValue ? ScrollMode.Disabled : ScrollMode.Auto);
            ScrollViewer.SetIsVerticalRailEnabled(this, newValue ? false : true);
            ScrollViewer.SetHorizontalScrollBarVisibility(this, newValue ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled);
            ScrollViewer.SetHorizontalScrollMode(this, newValue ? ScrollMode.Auto : ScrollMode.Disabled);
            ScrollViewer.SetIsHorizontalRailEnabled(this, newValue ? true : false);

            var root = ItemsPanelRoot as ItemsStackPanel;
            if (root != null)
            {
                root.Orientation = newValue ? Orientation.Horizontal : Orientation.Vertical;
            }
            else
            {
                _needUpdate = true;
            }
        }
    }
}
