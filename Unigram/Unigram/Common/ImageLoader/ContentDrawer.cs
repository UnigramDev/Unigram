using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Composition;
using System;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Composition;

namespace Unigram.Common
{
    public abstract class ContentDrawer
    {
        public abstract Task Draw(CompositionGraphicsDevice device, Object drawingLock, CompositionDrawingSurface surface, Size size);
    }

    public delegate void LoadTimeEffectHandler(CompositionDrawingSurface surface, CanvasBitmap bitmap, CompositionGraphicsDevice device);

    public class BitmapDrawer : ContentDrawer
    {
        Uri _uri;
        LoadTimeEffectHandler _handler;

        public BitmapDrawer(Uri uri, LoadTimeEffectHandler handler)
        {
            _uri = uri;
            _handler = handler;
        }

        public Uri Uri
        {
            get { return _uri; }
        }

        public override async Task Draw(CompositionGraphicsDevice device, Object drawingLock, CompositionDrawingSurface surface, Size size)
        {
            var canvasDevice = CanvasComposition.GetCanvasDevice(device);
            using (var canvasBitmap = await CanvasBitmap.LoadAsync(canvasDevice, _uri))
            {
                var bitmapSize = canvasBitmap.Size;

                //
                // Because the drawing is done asynchronously and multiple threads could
                // be trying to get access to the device/surface at the same time, we need
                // to do any device/surface work under a lock.
                //
                lock (drawingLock)
                {
                    Size surfaceSize = size;
                    if (surface.Size != size || surface.Size == new Size(0, 0))
                    {
                        // Resize the surface to the size of the image
                        CanvasComposition.Resize(surface, bitmapSize);
                        surfaceSize = bitmapSize;
                    }

                    // Allow the app to process the bitmap if requested
                    if (_handler != null)
                    {
                        _handler(surface, canvasBitmap, device);
                    }
                    else
                    {
                        // Draw the image to the surface
                        using (var session = CanvasComposition.CreateDrawingSession(surface))
                        {
                            session.Clear(Windows.UI.Color.FromArgb(0, 0, 0, 0));
                            session.DrawImage(canvasBitmap, new Rect(0, 0, surfaceSize.Width, surfaceSize.Height), new Rect(0, 0, bitmapSize.Width, bitmapSize.Height));
                        }
                    }
                }
            }
        }
    }

    public class StreamDrawer : ContentDrawer
    {
        IRandomAccessStream _uri;
        LoadTimeEffectHandler _handler;

        public StreamDrawer(IRandomAccessStream uri, LoadTimeEffectHandler handler)
        {
            _uri = uri;
            _handler = handler;
        }

        public IRandomAccessStream Uri
        {
            get { return _uri; }
        }

        public override async Task Draw(CompositionGraphicsDevice device, Object drawingLock, CompositionDrawingSurface surface, Size size)
        {
            var canvasDevice = CanvasComposition.GetCanvasDevice(device);
            using (var canvasBitmap = await CanvasBitmap.LoadAsync(canvasDevice, _uri))
            {
                var bitmapSize = canvasBitmap.Size;

                //
                // Because the drawing is done asynchronously and multiple threads could
                // be trying to get access to the device/surface at the same time, we need
                // to do any device/surface work under a lock.
                //
                lock (drawingLock)
                {
                    Size surfaceSize = size;
                    if (surface.Size != size || surface.Size == new Size(0, 0))
                    {
                        // Resize the surface to the size of the image
                        CanvasComposition.Resize(surface, bitmapSize);
                        surfaceSize = bitmapSize;
                    }

                    // Allow the app to process the bitmap if requested
                    if (_handler != null)
                    {
                        _handler(surface, canvasBitmap, device);
                    }
                    else
                    {
                        // Draw the image to the surface
                        using (var session = CanvasComposition.CreateDrawingSession(surface))
                        {
                            session.Clear(Windows.UI.Color.FromArgb(0, 0, 0, 0));
                            session.DrawImage(canvasBitmap, new Rect(0, 0, surfaceSize.Width, surfaceSize.Height), new Rect(0, 0, bitmapSize.Width, bitmapSize.Height));
                        }
                    }
                }
            }
        }
    }

    internal class CircleDrawer : ContentDrawer
    {
        private float _radius;
        private Color _color;

        public CircleDrawer(float radius, Color color)
        {
            _radius = radius;
            _color = color;
        }

        public float Radius
        {
            get { return _radius; }
        }

        public Color Color
        {
            get { return _color; }
        }

#pragma warning disable 1998
        public override async Task Draw(CompositionGraphicsDevice device, Object drawingLock, CompositionDrawingSurface surface, Size size)
        {
            using (var ds = CanvasComposition.CreateDrawingSession(surface))
            {
                ds.Clear(Colors.Transparent);
                ds.FillCircle(new Vector2(_radius, _radius), _radius, _color);
            }
        }
    }

    internal class TextDrawer : ContentDrawer
    {
        private string _text;
        private CanvasTextFormat _textFormat;
        private Color _textColor;
        private Color _backgroundColor;

        public TextDrawer(string text, CanvasTextFormat textFormat, Color textColor, Color bgColor)
        {
            _text = text;
            _textFormat = textFormat;
            _textColor = textColor;
            _backgroundColor = bgColor;
        }

        public override async Task Draw(CompositionGraphicsDevice device, Object drawingLock, CompositionDrawingSurface surface, Size size)
        {
            using (var ds = CanvasComposition.CreateDrawingSession(surface))
            {
                ds.Clear(_backgroundColor);
                ds.DrawText(_text, new Rect(0, 0, surface.Size.Width, surface.Size.Height), _textColor, _textFormat);
            }
        }
    }
}
