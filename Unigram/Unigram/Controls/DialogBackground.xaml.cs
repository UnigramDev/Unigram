using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls
{
    public sealed partial class DialogBackground : UserControl
    {
        public DialogBackground()
        {
            InitializeComponent();

            SizeChanged += OnSizeChanged;
        }

        private const double IMAGE_WIDTH = 480;
        private const double IMAGE_HEIGHT = 750;

        private Stack<Image> _recycled = new Stack<Image>();

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var columns = (int)Math.Ceiling(e.NewSize.Width / IMAGE_WIDTH);
            var rows = (int)Math.Ceiling(e.NewSize.Height / IMAGE_HEIGHT);

            for (int i = 0; i < columns * rows; i++)
            {
                var y = i / columns;
                var x = i % columns;

                Image image;

                if (i < Canvas.Children.Count)
                {
                    image = Canvas.Children[i] as Image;
                }
                else
                {
                    if (_recycled.Count > 0)
                    {
                        image = _recycled.Pop();
                    }
                    else
                    {
                        image = new Image();
                        image.Style = Resources["BackgroundStyle"] as Style;
                        image.Width = IMAGE_WIDTH;
                        image.Height = IMAGE_HEIGHT;
                    }

                    Canvas.Children.Add(image);
                }

                Canvas.SetLeft(image, x * IMAGE_WIDTH);
                Canvas.SetTop(image, y * IMAGE_HEIGHT);
            }

            //for (int i = columns * rows; i < Canvas.Children.Count; i++)
            //{
            //    var image = Canvas.Children[i] as Image;
            //    if (image != null)
            //    {
            //        Canvas.Children.RemoveAt(i);
            //        VisualTreeHelper.DisconnectChildrenRecursive(image);

            //        _recycled.Push(image);
            //    }
            //}
        }

        //private float _logicalDpi;
        //private CanvasBitmap _backgroundImage;
        //private CanvasImageBrush _backgroundBrush;

        //private void BackgroundCanvas_CreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
        //{
        //    args.TrackAsyncAction(Task.Run(async () =>
        //    {
        //        _backgroundImage = await CanvasBitmap.LoadAsync(sender, new Uri("ms-appx:///Assets/Images/DefaultBackground.png"));
        //        _backgroundBrush = new CanvasImageBrush(sender, _backgroundImage);
        //        _backgroundBrush.ExtendX = _backgroundBrush.ExtendY = CanvasEdgeBehavior.Wrap;
        //    }).AsAsyncAction());
        //}

        //private void BackgroundCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        //{
        //    args.DrawingSession.FillRectangle(new Rect(new Point(), sender.RenderSize), _backgroundBrush);
        //}

    }
}
