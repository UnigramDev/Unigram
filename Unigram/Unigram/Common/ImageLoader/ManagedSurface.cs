using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Composition;

namespace Unigram.Common
{
    public class ManagedSurface
    {
        private CompositionDrawingSurface   _surface;
        private ContentDrawer               _drawer;
        private CompositionSurfaceBrush     _brush;
        
        public CompositionDrawingSurface Surface
        {
            get { return _surface; }
        }

        public CompositionSurfaceBrush Brush
        {
            get
            {
                if (_brush == null)
                {
                    _brush = _surface.Compositor.CreateSurfaceBrush(_surface);
                }

                return _brush;
            }
        }

        public Size Size
        {
            get
            {
                return (_surface != null) ? _surface.Size : Size.Empty;
            }
        }

        public ManagedSurface(CompositionDrawingSurface surface)
        {
            Debug.Assert(surface != null);
            _surface = surface;

            ImageLoader.Instance.RegisterSurface(this);
        }

        public async Task Draw(CompositionGraphicsDevice device, Object drawingLock, ContentDrawer drawer)
        {
            Debug.Assert(_surface != null);

            _drawer = drawer;
            await _drawer.Draw(device, drawingLock, _surface, _surface.Size);
        }

        private async Task ReloadContent(CompositionGraphicsDevice device, Object drawingLock)
        {
            await _drawer.Draw(device, drawingLock, _surface, _surface.Size);
        }

        public async void OnDeviceReplaced(object sender, object e)
        {
            DeviceReplacedEventArgs args = (DeviceReplacedEventArgs)e;
            await ReloadContent(args.GraphicsDevce, args.DrawingLock);
        }
                
        public void Dispose()
        {
            if (_surface != null)
            {
                _surface.Dispose();
                _surface = null;
            }

            if (_brush != null)
            {
                _brush.Dispose();
                _brush = null;
            }

            _drawer = null;

            ImageLoader.Instance.UnregisterSurface(this);
        }
    }
}
