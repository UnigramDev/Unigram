using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class OrientableListView : GridView
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
                    root.Orientation = Orientation;
                    _needUpdate = false;
                }
            }
        }

        #region Orientation

        public Orientation Orientation
        {
            get { return (Orientation)GetValue(OrientationProperty); }
            set { SetValue(OrientationProperty, value); }
        }

        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register("Orientation", typeof(Orientation), typeof(OrientableListView), new PropertyMetadata(Orientation.Vertical, OnOrientationChanged));

        private static void OnOrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((OrientableListView)d).OnOrientationChanged((Orientation)e.NewValue, (Orientation)e.OldValue);
        }

        #endregion

        private void OnOrientationChanged(Orientation newValue, Orientation oldValue)
        {
            if (newValue == oldValue)
            {
                return;
            }

            var horizontal = newValue == Orientation.Horizontal;
            ScrollViewer.SetVerticalScrollBarVisibility(this, horizontal ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto);
            ScrollViewer.SetVerticalScrollMode(this, horizontal ? ScrollMode.Disabled : ScrollMode.Auto);
            ScrollViewer.SetIsVerticalRailEnabled(this, horizontal ? false : true);
            ScrollViewer.SetHorizontalScrollBarVisibility(this, horizontal ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled);
            ScrollViewer.SetHorizontalScrollMode(this, horizontal ? ScrollMode.Auto : ScrollMode.Disabled);
            ScrollViewer.SetIsHorizontalRailEnabled(this, horizontal ? true : false);

            var root = ItemsPanelRoot as ItemsStackPanel;
            if (root != null)
            {
                root.Orientation = newValue;
            }
            else
            {
                _needUpdate = true;
            }
        }
    }
}
