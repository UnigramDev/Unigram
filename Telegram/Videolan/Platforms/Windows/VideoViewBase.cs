using System;
using System.Runtime.InteropServices;
using LibVLCSharp.Shared;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using Telegram.Controls;

#if WINUI
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
#else
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.ApplicationModel;
#endif

namespace LibVLCSharp.Platforms.Windows
{
    /// <summary>
    /// VideoView base class for the UWP platform
    /// </summary>
    [TemplatePart(Name = PartSwapChainPanelName, Type = typeof(SwapChainPanel))]
    public abstract class VideoViewBase : ControlEx, IVideoView
    {
        private const string PartSwapChainPanelName = "SwapChainPanel";

        SwapChainPanel _panel;
        SharpDX.Direct3D11.Device _d3D11Device;
        SharpDX.DXGI.Device3 _device3;
        SwapChain2 _swapChain2;
        SwapChain1 _swapChain;
        DeviceContext _deviceContext;
        bool _loaded;

        /// <summary>
        /// The constructor
        /// </summary>
        public VideoViewBase()
        {
            DefaultStyleKey = typeof(VideoViewBase);

            Disconnected += (s, e) => DestroySwapChain();
#if !WINUI
            if (!DesignMode.DesignModeEnabled)
            {
                Application.Current.Suspending += (s, e) => { Trim(); };
            }
#endif
        }

        /// <summary>
        /// Invoked whenever application code or internal processes (such as a rebuilding layout pass) call ApplyTemplate. 
        /// In simplest terms, this means the method is called just before a UI element displays in your app.
        /// Override this method to influence the default post-template logic of a class.
        /// </summary>
        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _panel = (SwapChainPanel)GetTemplateChild(PartSwapChainPanelName);

#if !WINUI
            if (DesignMode.DesignModeEnabled)
                return;
#endif
            DestroySwapChain();

            _panel.SizeChanged += (s, eventArgs) =>
            {
                if (_loaded)
                {
                    UpdateSize();
                }
                else
                {
                    CreateSwapChain();
                }
            };

            _panel.CompositionScaleChanged += (s, eventArgs) =>
            {
                if (_loaded)
                {
                    UpdateScale();
                }
            };

        }

        public void Clear()
        {
            if (_swapChain != null)
            {
                try
                {
                    using var backBuffer = _swapChain.GetBackBuffer<Texture2D>(0);
                    using var target = new RenderTargetView(_d3D11Device, backBuffer);

                    _deviceContext.ClearRenderTargetView(target, new RawColor4(0, 0, 0, 0));
                    _swapChain.Present(0, PresentFlags.None);
                }
                catch
                {
                    // All the remote procedure calls must be wrapped in a try-catch block
                }
            }
        }

        /// <summary>
        /// Gets the swapchain parameters to pass to the <see cref="LibVLC"/> constructor.
        /// If you don't pass them to the <see cref="LibVLC"/> constructor, the video won't
        /// be displayed in your application.
        /// Calling this property will throw an <see cref="InvalidOperationException"/> if the VideoView is not yet full Loaded.
        /// </summary>
        /// <returns>The list of arguments to be given to the <see cref="LibVLC"/> constructor.</returns>
        public string[] SwapChainOptions
        {
            get
            {
                if (!_loaded)
                {
                    throw new InvalidOperationException("You must wait for the VideoView to be loaded before calling GetSwapChainOptions()");
                }

                _deviceContext = _d3D11Device!.ImmediateContext;

                return new string[]
                {
                    $"--winrt-d3dcontext=0x{_deviceContext.NativePointer.ToString("x")}",
                    $"--winrt-swapchain=0x{_swapChain!.NativePointer.ToString("x")}"
                };
            }
        }

        /// <summary>
        /// Called when the video view is fully loaded
        /// </summary>
        protected abstract void OnInitialized();

        /// <summary>
        /// Initializes the SwapChain for use with LibVLC
        /// </summary>
        void CreateSwapChain()
        {
            // Do not create the swapchain when the VideoView is collapsed.
            if (_panel == null || _panel.ActualHeight == 0)
                return;

            SharpDX.DXGI.Factory2 dxgiFactory = null;
            try
            {
                var creationFlags =
                    DeviceCreationFlags.BgraSupport | DeviceCreationFlags.VideoSupport;

#if DEBUG
                creationFlags |= DeviceCreationFlags.Debug;

                try
                {
                    dxgiFactory = new SharpDX.DXGI.Factory2(true);
                }
                catch (SharpDXException)
                {
                    dxgiFactory = new SharpDX.DXGI.Factory2(false);
                }
#else
                dxgiFactory = new SharpDX.DXGI.Factory2(false);
#endif
                _d3D11Device = null;
                int i_adapter = 0;
                int adapterCount = dxgiFactory.GetAdapterCount();

                while (_d3D11Device == null)
                {
                    if (i_adapter == adapterCount)
                    {
                        if (creationFlags.HasFlag(DeviceCreationFlags.VideoSupport))
                        {
                            i_adapter = 0;
                            creationFlags &= ~DeviceCreationFlags.VideoSupport;
                            continue;
                        }
                        else
                        {
                            break;
                        }
                    }

                    try
                    {
                        var adapter = dxgiFactory.GetAdapter(i_adapter++);
                        _d3D11Device = new SharpDX.Direct3D11.Device(adapter, creationFlags);
                        adapter.Dispose();
                        adapter = null;
                        break; 
                    }
                    catch (SharpDXException)
                    {
                    }
                }

                if (_d3D11Device is null)
                {
                    throw new VLCException("Could not create Direct3D11 device : No compatible adapter found.");
                }

                var device = _d3D11Device.QueryInterface<SharpDX.DXGI.Device1>();

                //Create the swapchain
                var swapChainDescription = new SharpDX.DXGI.SwapChainDescription1
                {
                    Width = (int)(_panel.ActualWidth * _panel.CompositionScaleX),
                    Height = (int)(_panel.ActualHeight * _panel.CompositionScaleY),
                    Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                    Stereo = false,
                    SampleDescription =
                    {
                        Count = 1,
                        Quality = 0
                    },
                    Usage = Usage.RenderTargetOutput,
                    BufferCount = 2,
                    SwapEffect = SwapEffect.FlipSequential,
                    Flags = SwapChainFlags.None,
                    AlphaMode = AlphaMode.Premultiplied
                };

                _swapChain = new SharpDX.DXGI.SwapChain1(dxgiFactory, _d3D11Device, ref swapChainDescription);
                dxgiFactory.Dispose();
                dxgiFactory = null;

                device.MaximumFrameLatency = 1;

                using (var panelNative = ComObject.As<ISwapChainPanelNative>(_panel))
                {
                    panelNative.SwapChain = _swapChain;
                }

                // This is necessary so we can call Trim() on suspend
                _device3 = device.QueryInterface<SharpDX.DXGI.Device3>();
                if (_device3 == null)
                {
                    throw new VLCException("Failed to query interface \"Device3\"");
                }

                device.Dispose();
                device = null;

                _swapChain2 = _swapChain.QueryInterface<SharpDX.DXGI.SwapChain2>();
                if (_swapChain2 == null)
                {
                    throw new VLCException("Failed to query interface \"SwapChain2\"");
                }

                UpdateScale();
                UpdateSize();
                _loaded = true;
                OnInitialized();
            }
            catch (Exception ex)
            {
                DestroySwapChain();
                if (ex is SharpDXException)
                {
                    throw new VLCException("SharpDX operation failed, see InnerException for details", ex);
                }

                throw;
            }
        }

        /// <summary>
        /// Destroys the SwapChain and all related instances.
        /// </summary>
        void DestroySwapChain()
        {
            _swapChain2?.Dispose();
            _swapChain2 = null;

            _device3?.Dispose();
            _device3 = null;

            if (_panel != null)
            {
                using (var panelNative = ComObject.As<ISwapChainPanelNative>(_panel))
                {
                    panelNative.SwapChain = null;
                }
            }

            _swapChain?.Dispose();
            _swapChain = null;

            _deviceContext?.Dispose();
            _deviceContext = null;

            _d3D11Device?.Dispose();
            _d3D11Device = null;

            _loaded = false;
        }

        readonly Guid SWAPCHAIN_WIDTH = new Guid(0xf1b59347, 0x1643, 0x411a, 0xad, 0x6b, 0xc7, 0x80, 0x17, 0x7a, 0x6, 0xb6);
        readonly Guid SWAPCHAIN_HEIGHT = new Guid(0x6ea976a0, 0x9d60, 0x4bb7, 0xa5, 0xa9, 0x7d, 0xd1, 0x18, 0x7f, 0xc9, 0xbd);

        /// <summary>
        /// Associates width/height private data into the SwapChain, so that VLC knows at which size to render its video.
        /// </summary>
        void UpdateSize()
        {
            if (_panel is null || _swapChain is null || _swapChain.IsDisposed)
                return;

            var width = IntPtr.Zero;
            var height = IntPtr.Zero;

            try
            {
                width = Marshal.AllocHGlobal(sizeof(int));
                height = Marshal.AllocHGlobal(sizeof(int));

                var w = (int)(_panel.ActualWidth * _panel.CompositionScaleX);
                var h = (int)(_panel.ActualHeight * _panel.CompositionScaleY);

                Marshal.WriteInt32(width, w);
                Marshal.WriteInt32(height, h);

                _swapChain.SetPrivateData(SWAPCHAIN_WIDTH, sizeof(int), width);
                _swapChain.SetPrivateData(SWAPCHAIN_HEIGHT, sizeof(int), height);
            }
            finally
            {
                Marshal.FreeHGlobal(width);
                Marshal.FreeHGlobal(height);
            }
        }

        /// <summary>
        /// Updates the MatrixTransform of the SwapChain.
        /// </summary>
        void UpdateScale()
        {
            if (_panel is null) return;

            // TODO: experiment
            // CompositionScale changes when che SwapChainPanel is inside a ScrollViewer and ZoomLevel changes.
            // We don't want this to happen, so let's try to use XamlRoot.RasterizationScale instead.

            _swapChain2!.MatrixTransform = new RawMatrix3x2
            {
                M11 = 1.0f / (float)XamlRoot.RasterizationScale, //_panel.CompositionScaleX,
                M22 = 1.0f / (float)XamlRoot.RasterizationScale //_panel.CompositionScaleY
            };
        }

        /// <summary>
        /// When the app is suspended, UWP apps should call Trim so that the DirectX data is cleaned.
        /// </summary>
        void Trim()
        {
            _device3?.Trim();
        }

        /// <summary>
        /// When the media player is attached to the view.
        /// </summary>
        void Attach()
        {
        }

        /// <summary>
        /// When the media player is detached from the view.
        /// </summary>
        void Detach()
        {
        }


        /// <summary>
        /// Identifies the <see cref="MediaPlayer"/> dependency property.
        /// </summary>
        public static DependencyProperty MediaPlayerProperty { get; } = DependencyProperty.Register(nameof(MediaPlayer), typeof(MediaPlayer),
            typeof(VideoViewBase), new PropertyMetadata(null, OnMediaPlayerChanged));
        /// <summary>
        /// MediaPlayer object connected to the view
        /// </summary>
        public MediaPlayer MediaPlayer
        {
            get => (MediaPlayer?)GetValue(MediaPlayerProperty);
            set => SetValue(MediaPlayerProperty, value);
        }

        private static void OnMediaPlayerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var videoView = (VideoViewBase)d;
            videoView.Detach();
            if (e.NewValue != null)
            {
                videoView.Attach();
            }
        }
    }

#if WINUI
    [Guid("63aad0b8-7c24-40ff-85a8-640d944cc325")]
    internal class ISwapChainPanelNative : SharpDX.DXGI.ISwapChainPanelNative
    {
        public ISwapChainPanelNative(IntPtr nativePtr) : base(nativePtr) { }
    }
#endif
}
