using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Composition;
using System.Numerics;
using Telegram.Native.Composition;
using Telegram.Navigation;
using Telegram.Services;
using Windows.Foundation;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Controls.Cells
{
    public sealed partial class CaptureSessionItemCell : UserControl
    {
        public CaptureSessionItemCell()
        {
            InitializeComponent();
            Presenter.Constraint = new Size(16, 10);
        }

        private WindowVisual _windowVisual;
        private GraphicsCaptureItemVisual _displayVisual;

        public void UpdateCell(CaptureSessionItem item)
        {
            if (item != null)
            {
                DisplayName.Text = item.DisplayName;

                if (item is WindowCaptureSessionItem window)
                {
                    _windowVisual = WindowVisual.Create(window.WindowId);
                    _displayVisual = null;

                    if (_windowVisual != null)
                    {
                        ElementCompositionPreview.SetElementChildVisual(Presenter, _windowVisual.Child);
                        return;
                    }
                }
                else if (item is DisplayCaptureSessionItem display)
                {
                    _windowVisual = null;
                    _displayVisual = GraphicsCaptureItemVisual.Create(display);

                    if (_displayVisual != null)
                    {
                        ElementCompositionPreview.SetElementChildVisual(Presenter, _displayVisual.Child);
                        return;
                    }
                }
            }

            _windowVisual = null;
            _displayVisual = null;
            ElementCompositionPreview.SetElementChildVisual(Presenter, BootStrapper.Current.Compositor.CreateSpriteVisual());
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_windowVisual != null)
            {
                _windowVisual.Size = e.NewSize.ToVector2();
            }
            else if (_displayVisual != null)
            {
                _displayVisual.Size = e.NewSize.ToVector2();
            }
        }
    }

    public partial class GraphicsCaptureItemVisual
    {
        public static GraphicsCaptureItemVisual Create(GraphicsCaptureItem item)
        {
            return new GraphicsCaptureItemVisual(item, ElementComposition.GetSharedDevice());
        }

        public static GraphicsCaptureItemVisual Create(WindowId displayId)
        {
            var item = GraphicsCaptureItem.TryCreateFromWindowId(displayId);
            if (item == null)
            {
                return null;
            }

            return new GraphicsCaptureItemVisual(item, ElementComposition.GetSharedDevice());
        }

        private GraphicsCaptureItemVisual(GraphicsCaptureItem item, CanvasDevice device)
        {
            _item = item;
            _device = device;
            _closeLock = new object();

            var compositor = BootStrapper.Current.Compositor;
            var compositionGraphicsDevice = CanvasComposition.CreateCompositionGraphicsDevice(compositor, _device);

            _surface = compositionGraphicsDevice.CreateDrawingSurface(new Size(0, 0), DirectXPixelFormat.B8G8R8A8UIntNormalized, DirectXAlphaMode.Premultiplied);
            _brush = compositor.CreateSurfaceBrush(_surface);
            _brush.HorizontalAlignmentRatio = 0.5f;
            _brush.VerticalAlignmentRatio = 0.5f;
            _brush.Stretch = CompositionStretch.Uniform;

            _visual = compositor.CreateSpriteVisual();
            _visual.Brush = _brush;
            //_visual.Size = new Vector2(500, 500);

            _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(_device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, item.Size);
            _session = _framePool.CreateCaptureSession(item);

            _framePool.FrameArrived += OnFrameArrived;
            _session.StartCapture();
        }

        public Visual Child => _visual;

        public Vector2 Size
        {
            get => _visual.Size;
            set => _visual.Size = value;
        }

        public void Dispose()
        {
            lock (_closeLock)
            {
                _session?.Dispose();
                _framePool?.Dispose();
                _surface?.Dispose();
                _brush?.Dispose();

                _surface = null;
                _brush = null;
                _framePool = null;
                _session = null;
                _item = null;
            }
        }

        private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            lock (_closeLock)
            {
                using (var frame = sender.TryGetNextFrame())
                {
                    _surface.Resize(frame.ContentSize);

                    using (var bitmap = CanvasBitmap.CreateFromDirect3D11Surface(_device, frame.Surface))
                    using (var drawingSession = CanvasComposition.CreateDrawingSession(_surface))
                    {
                        drawingSession.DrawImage(bitmap);
                    }
                }

                _session?.Dispose();
                _framePool?.Dispose();
            }
        }

        private GraphicsCaptureItem _item;
        private Direct3D11CaptureFramePool _framePool;
        private GraphicsCaptureSession _session;

        private CanvasDevice _device;
        private CompositionSurfaceBrush _brush;
        private CompositionDrawingSurface _surface;

        private SpriteVisual _visual;

        private object _closeLock;
    }
}
