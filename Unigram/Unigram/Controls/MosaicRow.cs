using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Common;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    public class MosaicRow : Grid
    {
        public MosaicRow()
        {
            //SizeChanged += OnSizeChanged;
            //DataContextChanged += OnDataContextChanged;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width != e.PreviousSize.Width)
            {
                Height = e.NewSize.Width / 5;
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(availableSize.Width, 80);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var left = 0d;

            for (int i = 0; i < Children.Count; i++)
            {
                var child = Children[i] as FrameworkElement;
                var position = child.DataContext as MosaicMediaPosition;

                child.Arrange(new Rect(left * finalSize.Width, 0, position.Width * finalSize.Width, 80));
                left += position.Width;
            }

            return finalSize;
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            Children.Clear();
            ColumnDefinitions.Clear();

            for (int i = 0; i < Children.Count; i++)
            {
                var child = Children[i] as FrameworkElement;
            }

            var items = DataContext as IList<MosaicMediaPosition>;
            if (items == null)
            {
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var border = new Border();
                border.Background = new SolidColorBrush(Colors.Red);
                border.Margin = new Thickness(2);

                SetColumn(border, i);
                ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(item.Width, GridUnitType.Star) });
                Children.Add(border);
            }
        }
    }
}
