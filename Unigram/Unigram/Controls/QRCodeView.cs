using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.DirectX;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using ZXing;
using ZXing.QrCode.Internal;

namespace Unigram.Controls
{
    [TemplatePart(Name = "Canvas", Type = typeof(CanvasControl))]
    public class QRCodeView : Control
    {
        private CanvasControl Canvas;
        private string CanvasPartName = "Canvas";

        private string _text;
        private CanvasBitmap _overlay;

        public QRCodeView()
        {
            DefaultStyleKey = typeof(QRCodeView);
        }

        protected override void OnApplyTemplate()
        {
            var canvas = GetTemplateChild(CanvasPartName) as CanvasControl;
            if (canvas == null)
            {
                return;
            }

            Canvas = canvas;
            Canvas.Unloaded += OnUnloaded;
            Canvas.CreateResources += OnCreateResources;
            Canvas.Draw += OnDraw;

            base.OnApplyTemplate();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Canvas.Unloaded -= OnUnloaded;
            Canvas.CreateResources -= OnCreateResources;
            Canvas.Draw -= OnDraw;
            Canvas.RemoveFromVisualTree();
            Canvas = null;
        }

        private void OnCreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
        {
            args.TrackAsyncAction(OnCreateResourcesAsync(sender).AsAsyncAction());
        }

        private async Task OnCreateResourcesAsync(CanvasControl sender)
        {
            _overlay = await CanvasBitmap.LoadAsync(sender, new Uri("ms-appx:///Assets/Images/WalletGem.png"));
        }

        private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (_text == null)
            {
                return;
            }

            var writer = new BarcodeWriterPixelData();
            writer.Options.Hints[EncodeHintType.ERROR_CORRECTION] = ErrorCorrectionLevel.H;
            writer.Options.Width = 768;
            writer.Options.Height = 768;
            writer.Format = BarcodeFormat.QR_CODE;

            var data = writer.Write(_text);
            var bitmap = CanvasBitmap.CreateFromBytes(sender, data.Pixels, data.Width, data.Height, DirectXPixelFormat.B8G8R8A8UIntNormalized);

            args.DrawingSession.Transform = System.Numerics.Matrix3x2.CreateScale(256f / 768f);

            args.DrawingSession.DrawImage(bitmap);

            if (_overlay != null)
            {
                args.DrawingSession.DrawImage(_overlay, new System.Numerics.Vector2((data.Width - _overlay.SizeInPixels.Width) / 2f, (data.Height - _overlay.SizeInPixels.Height) / 2f));
            }
        }

        private void OnTextChanged(string newValue, string oldValue)
        {
            var canvas = Canvas;
            if (canvas == null)
            {
                _text = newValue;
                return;
            }

            if (newValue == null)
            {
                canvas.Invalidate();
                return;
            }

            if (string.Equals(newValue, oldValue, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(newValue, _text, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _text = newValue;
            canvas.Invalidate();
        }

        #region Text

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(QRCodeView), new PropertyMetadata(null, OnTextChanged));

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((QRCodeView)d).OnTextChanged((string)e.NewValue, (string)e.OldValue);
        }

        #endregion
    }
}
