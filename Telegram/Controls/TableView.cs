using System;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public partial class TableView : ItemsControl
    {
        private Border RootGrid;

        public TableView()
        {
            DefaultStyleKey = typeof(TableView);
        }

        protected override void OnApplyTemplate()
        {
            RootGrid = GetTemplateChild(nameof(RootGrid)) as Border;
        }

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            if (element is TableViewItem presenter)
            {
                presenter.ColumnWidthChanged += OnColumnWidthChanged;
            }

            base.PrepareContainerForItemOverride(element, item);
        }

        private void OnColumnWidthChanged(object sender, EventArgs e)
        {
            double width = 0;

            foreach (TableViewItem child in ItemsPanelRoot.Children)
            {
                width = Math.Max(width, child.ColumnWidth);
            }

            if (RootGrid.Margin.Left != width)
            {
                RootGrid.Margin = new Thickness(width, 0, 0, 0);

                foreach (TableViewItem child in ItemsPanelRoot.Children)
                {
                    child.ContentMargin = new Thickness(width, 0, 0, 0);
                }
            }
        }
    }

    public partial class TableViewPanel : Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            var y = 0d;
            var last = true;

            for (int i = Children.Count - 1; i >= 0; i--)
            {
                if (Children[i] is TableViewItem item && item.Visibility == Visibility.Visible)
                {
                    item.BorderThickness = new Thickness(0, 0, 0, last ? 0 : 1);

                    item.Measure(availableSize);
                    y += item.DesiredSize.Height;

                    last = false;
                }
            }

            return new Size(availableSize.Width, y);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var y = 0d;

            for (int i = 0; i < Children.Count; i++)
            {
                if (Children[i] is TableViewItem item && item.Visibility == Visibility.Visible)
                {
                    item.Arrange(new Rect(0, y, item.DesiredSize.Width, item.DesiredSize.Height));
                    y += item.DesiredSize.Height;
                }
            }

            return finalSize;
        }
    }

    public partial class TableViewItem : ContentControl
    {
        public TableViewItemPresenter RootGrid;

        public TableViewItem()
        {
            DefaultStyleKey = typeof(TableViewItem);
        }

        protected override void OnApplyTemplate()
        {
            RootGrid = GetTemplateChild(nameof(RootGrid)) as TableViewItemPresenter;
            RootGrid.Owner = this;
        }

        public event EventHandler ColumnWidthChanged;

        private double _columnWidth;
        public double ColumnWidth
        {
            get => _columnWidth;
            set
            {
                if (_columnWidth != value)
                {
                    _columnWidth = value;
                    ColumnWidthChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        #region Header

        public string Header
        {
            get { return (string)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register("Header", typeof(string), typeof(TableViewItem), new PropertyMetadata(string.Empty));

        #endregion

        #region ContentMargin

        public Thickness ContentMargin
        {
            get { return (Thickness)GetValue(ContentMarginProperty); }
            set { SetValue(ContentMarginProperty, value); }
        }

        public static readonly DependencyProperty ContentMarginProperty =
            DependencyProperty.Register("ContentMargin", typeof(Thickness), typeof(TableViewItem), new PropertyMetadata(default(Thickness)));

        #endregion
    }

    public partial class TableViewItemPresenter : Panel
    {
        public TableViewItem Owner { get; set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            var header = Children[0];
            var content = Children[1];
            header.Measure(availableSize);
            content.Measure(new Size(availableSize.Width, availableSize.Height));

            Owner.ColumnWidth = header.DesiredSize.Width;

            return new Size(availableSize.Width, Math.Max(32, content.DesiredSize.Height));
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var header = Children[0];
            var content = Children[1];

            header.Arrange(new Rect(0, 0, header.DesiredSize.Width, header.DesiredSize.Height));
            content.Arrange(new Rect(0, 0, content.DesiredSize.Width, content.DesiredSize.Height));

            return finalSize;
        }
    }
}
