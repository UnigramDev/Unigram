//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Composition;
using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Native.Composition;
using Telegram.Navigation;
using Telegram.Services;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
#if WINUI3
using CompositionAlphaMode = Microsoft.Graphics.DirectX.DirectXAlphaMode;
using CompositionPixelFormat = Microsoft.Graphics.DirectX.DirectXPixelFormat;
#else
using CompositionAlphaMode = Windows.Graphics.DirectX.DirectXAlphaMode;
using CompositionPixelFormat = Windows.Graphics.DirectX.DirectXPixelFormat;
#endif
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

#if !WINUI3
        private WindowVisual _windowVisual;
#endif
        private ThumbnailVisual _thumbnail;
        private bool _thumbnailRequested;

        /// <summary>
        /// The queue is what WinUI takes window stills through, and is not used on UWP, where DWM
        /// keeps a live thumbnail of the window instead.
        /// </summary>
        public void UpdateCell(CaptureSessionItem item, ThumbnailQueue queue)
        {
            ReleaseThumbnail();

            if (item != null)
            {
                DisplayName.Text = item.DisplayName;

#if WINUI3
                if (item is WindowCaptureSessionItem window)
                {
                    _thumbnail = new WindowSnapshotVisual(window.WindowId, queue);
                }
#else
                if (item is WindowCaptureSessionItem window)
                {
                    var destination = new WindowId { Value = (ulong)WindowContext.GetWindowHandle(XamlRoot).ToInt64() };

                    _windowVisual = WindowVisual.Create(window.WindowId, destination, BootStrapper.Current.Compositor, (float)XamlRoot.RasterizationScale);

                    if (_windowVisual != null)
                    {
                        ElementCompositionPreview.SetElementChildVisual(Presenter, _windowVisual.Child);
                        return;
                    }
                }
#endif
                else if (item is DisplayCaptureSessionItem display)
                {
                    _thumbnail = new GraphicsCaptureItemVisual(display.Item);
                }

                if (_thumbnail != null)
                {
                    ElementCompositionPreview.SetElementChildVisual(Presenter, _thumbnail.Child);
                    RequestThumbnail();
                    return;
                }
            }

            ElementCompositionPreview.SetElementChildVisual(Presenter, BootStrapper.Current.Compositor.CreateSpriteVisual());
        }

        /// <summary>
        /// What a window or a display shows changes while the user is away arranging it.
        /// </summary>
        public void RefreshThumbnail()
        {
            _thumbnailRequested = false;
            RequestThumbnail();
        }

        // The still is taken at the size it is shown at, so not before layout has given it one.
        private void RequestThumbnail()
        {
            if (_thumbnail == null || _thumbnailRequested || XamlRoot is not XamlRoot xamlRoot)
            {
                return;
            }

            var size = Presenter.ActualSize;
            if (size.X <= 0 || size.Y <= 0)
            {
                return;
            }

            _thumbnailRequested = true;
            _thumbnail.Capture(size * (float)xamlRoot.RasterizationScale);
        }

        private void ReleaseThumbnail()
        {
            _thumbnail?.Dispose();
            _thumbnail = null;
            _thumbnailRequested = false;
#if !WINUI3
            _windowVisual = null;
#endif
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
#if !WINUI3
            if (_windowVisual != null)
            {
                // DWM rasterizes the thumbnail in physical pixels, so the visual needs the scale
                // handed to it. An element that has left the tree has no XamlRoot to ask, and
                // nothing to show it in either.
                if (XamlRoot is XamlRoot xamlRoot)
                {
                    _windowVisual.Size = new Vector3(e.NewSize.ToVector2(), (float)xamlRoot.RasterizationScale);
                }

                return;
            }
#endif
            if (_thumbnail != null)
            {
                _thumbnail.Size = e.NewSize.ToVector2();
                RequestThumbnail();
            }
        }
    }

    /// <summary>
    /// Takes stills one at a time, off the UI thread. A window that stops answering while it is
    /// drawn holds up only the stills queued behind it, and a full-size copy of one window at a
    /// time is the most that is ever held.
    /// </summary>
    public sealed class ThumbnailQueue
    {
        private Task _tail = Task.CompletedTask;

        // From the UI thread only, which is what keeps the chain unsynchronized.
        public void Enqueue(Action action)
        {
            _tail = _tail.ContinueWith(_ => action(), CancellationToken.None, TaskContinuationOptions.DenyChildAttach, TaskScheduler.Default);
        }
    }

    /// <summary>
    /// A still of a capture source, kept at the size it is shown at rather than the source's own:
    /// at full resolution a 4K display alone is over 30 MB.
    /// </summary>
    public abstract partial class ThumbnailVisual : IDisposable
    {
        protected readonly object _lock = new();
        protected readonly CanvasDevice _device;
        protected bool _disposed;

        private readonly CompositionGraphicsDevice _graphicsDevice;
        private readonly CompositionDrawingSurface _surface;
        private readonly CompositionSurfaceBrush _brush;
        private readonly SpriteVisual _visual;

        protected ThumbnailVisual()
        {
            _device = ElementComposition.GetSharedDevice();

            var compositor = BootStrapper.Current.Compositor;

            _graphicsDevice = CanvasComposition.CreateCompositionGraphicsDevice(compositor, _device);
            _surface = _graphicsDevice.CreateDrawingSurface(new Size(0, 0), CompositionPixelFormat.B8G8R8A8UIntNormalized, CompositionAlphaMode.Premultiplied);

            _brush = compositor.CreateSurfaceBrush(_surface);
            _brush.HorizontalAlignmentRatio = 0.5f;
            _brush.VerticalAlignmentRatio = 0.5f;
            _brush.Stretch = CompositionStretch.Uniform;

            _visual = compositor.CreateSpriteVisual();
            _visual.Brush = _brush;
        }

        public Visual Child => _visual;

        public Vector2 Size
        {
            get => _visual.Size;
            set => _visual.Size = value;
        }

        /// <summary>
        /// Takes a still to fit in size, in physical pixels. Asking again while one is being taken
        /// does nothing.
        /// </summary>
        public abstract void Capture(Vector2 size);

        protected static SizeInt32 Fit(SizeInt32 source, Vector2 size)
        {
            var scale = MathF.Min(1, MathF.Min(size.X / source.Width, size.Y / source.Height));

            return new SizeInt32
            {
                Width = Math.Max(1, (int)MathF.Round(source.Width * scale)),
                Height = Math.Max(1, (int)MathF.Round(source.Height * scale))
            };
        }

        // From any thread.
        protected void Draw(CanvasBitmap bitmap, Rect source, SizeInt32 target)
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                _surface.Resize(target);

                using (var session = CanvasComposition.CreateDrawingSession(_surface))
                {
                    session.Clear(default(Color));
                    session.DrawImage(bitmap, new Rect(0, 0, target.Width, target.Height), source, 1, CanvasImageInterpolation.HighQualityCubic);
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;

                _brush.Dispose();
                _surface.Dispose();
                _graphicsDevice.Dispose();
            }

            OnDisposed();
        }

        // Outside the lock: a capture callback may be waiting on it.
        protected virtual void OnDisposed()
        {
        }
    }

    /// <summary>
    /// A display, from the first frame of a capture session that is closed as soon as it arrives.
    /// </summary>
    public sealed partial class GraphicsCaptureItemVisual : ThumbnailVisual
    {
        private readonly GraphicsCaptureItem _item;

        private Direct3D11CaptureFramePool _framePool;
        private GraphicsCaptureSession _session;
        private Vector2 _size;

        public GraphicsCaptureItemVisual(GraphicsCaptureItem item)
        {
            _item = item;
        }

        public override void Capture(Vector2 size)
        {
            lock (_lock)
            {
                if (_disposed || _framePool != null)
                {
                    return;
                }

                _size = size;

                try
                {
                    _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(_device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, _item.Size);
                    _framePool.FrameArrived += OnFrameArrived;

                    _session = _framePool.CreateCaptureSession(_item);
                    _session.StartCapture();
                }
                catch (Exception ex)
                {
                    Logger.Error("Capturing a display failed", ex);
                    Close(_framePool, _session);

                    _framePool = null;
                    _session = null;
                }
            }
        }

        private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            Direct3D11CaptureFramePool framePool;
            GraphicsCaptureSession session;

            lock (_lock)
            {
                try
                {
                    using var frame = sender.TryGetNextFrame();

                    if (frame != null && !_disposed)
                    {
                        var content = frame.ContentSize;

                        using var bitmap = CanvasBitmap.CreateFromDirect3D11Surface(_device, frame.Surface);
                        Draw(bitmap, new Rect(0, 0, content.Width, content.Height), Fit(content, _size));
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Drawing a display frame failed", ex);
                }

                framePool = _framePool;
                session = _session;

                _framePool = null;
                _session = null;
            }

            Close(framePool, session);
        }

        protected override void OnDisposed()
        {
            Direct3D11CaptureFramePool framePool;
            GraphicsCaptureSession session;

            lock (_lock)
            {
                framePool = _framePool;
                session = _session;

                _framePool = null;
                _session = null;
            }

            Close(framePool, session);
        }

        private void Close(Direct3D11CaptureFramePool framePool, GraphicsCaptureSession session)
        {
            session?.Dispose();

            if (framePool != null)
            {
                framePool.FrameArrived -= OnFrameArrived;
                framePool.Dispose();
            }
        }
    }

#if WINUI3
    /// <summary>
    /// A window, drawn by the window itself into a bitmap through PrintWindow.
    /// </summary>
    public sealed partial class WindowSnapshotVisual : ThumbnailVisual
    {
        private readonly WindowId _windowId;
        private readonly ThumbnailQueue _queue;

        private bool _pending;

        public WindowSnapshotVisual(WindowId windowId, ThumbnailQueue queue)
        {
            _windowId = windowId;
            _queue = queue;
        }

        public override void Capture(Vector2 size)
        {
            lock (_lock)
            {
                if (_disposed || _pending)
                {
                    return;
                }

                _pending = true;
            }

            _queue.Enqueue(() => Snapshot(size));
        }

        private void Snapshot(Vector2 size)
        {
            try
            {
                lock (_lock)
                {
                    // Gone by the time its turn came: the cell was recycled or the popup closed.
                    if (_disposed)
                    {
                        return;
                    }
                }

                using var bitmap = WindowVisual.CaptureSnapshot(_windowId, (int)size.X, (int)size.Y);

                if (bitmap != null)
                {
                    using var canvas = CanvasBitmap.CreateFromSoftwareBitmap(_device, bitmap);
                    Draw(canvas, new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight), new SizeInt32 { Width = bitmap.PixelWidth, Height = bitmap.PixelHeight });
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Taking a window still failed", ex);
            }
            finally
            {
                lock (_lock)
                {
                    _pending = false;
                }
            }
        }
    }
#endif
}
