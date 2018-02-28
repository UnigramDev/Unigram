using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TdWindows;
using Unigram.Common;
using Unigram.Services;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

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
                var position = child.Tag as MosaicMediaPosition;

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

            var items = DataContext as MosaicMediaRow;
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

        public void UpdateLine(IProtoService protoService, MosaicMediaRow line, Action<object> click)
        {
            Children.Clear();

            foreach (var position in line)
            {
                UIElement content = null;
                object item = position.Item;
                
                if (item is InlineQueryResultAnimation inlineAnimation)
                {
                    item = inlineAnimation.Animation;
                }
                else if (item is InlineQueryResultPhoto inlinePhoto)
                {
                    item = inlinePhoto.Photo;
                }

                if (item is Animation animation && animation.Thumbnail != null)
                {
                    content = new Image { Source = new BitmapImage(new Uri("file:///" + animation.Thumbnail.Photo.Local.Path)), Stretch = Stretch.UniformToFill };

                    if (!animation.Thumbnail.Photo.Local.IsDownloadingCompleted)
                    {
                        protoService.Send(new DownloadFile(animation.Thumbnail.Photo.Id, 1));
                    }
                }
                else if (item is Photo photo)
                {
                    var small = photo.GetSmall();
                    if (small != null)
                    {
                        content = new Image { Source = new BitmapImage(new Uri("file:///" + small.Photo.Local.Path)), Stretch = Stretch.UniformToFill };

                        if (!small.Photo.Local.IsDownloadingCompleted)
                        {
                            protoService.Send(new DownloadFile(small.Photo.Id, 1));
                        }
                    }
                }

                var button = new Button { Content = content, Tag = position, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, Style = App.Current.Resources["GridViewButtonStyle"] as Style };
                button.Click += (s, args) => click?.Invoke(position.Item);

                Children.Add(button);
            }
        }

        public void UpdateFile(MosaicMediaRow line, File file)
        {
            if (!file.Local.IsDownloadingCompleted)
            {
                return;
            }

            foreach (ContentControl child in Children)
            {
                if (child.Tag is MosaicMediaPosition position && child.Content is Image image)
                {
                    var item = position.Item;
                    if (item is InlineQueryResultAnimation inlineAnimation)
                    {
                        item = inlineAnimation.Animation;
                    }
                    else if (item is InlineQueryResultPhoto inlinePhoto)
                    {
                        item = inlinePhoto.Photo;
                    }

                    if (item is Animation animation && animation.Thumbnail != null && animation.UpdateFile(file))
                    {
                        image.Source = new BitmapImage(new Uri("file:///" + animation.Thumbnail.Photo.Local.Path));
                    }
                    else if (item is Photo photo && photo.UpdateFile(file))
                    {
                        var small = photo.GetSmall();
                        if (small != null)
                        {
                            image.Source = new BitmapImage(new Uri("file:///" + small.Photo.Local.Path));
                        }
                    }
                }
            }
        }
    }
}
