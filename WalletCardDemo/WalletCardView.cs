using System;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace WalletCardDemo
{
    /// <summary>
    /// Win2D-powered wallet card with pointer parallax and a gradient/star
    /// shader that reacts to the card's orientation. Port of
    /// WalletCardSceneView.swift.
    ///
    /// The shape of the original survives intact - the same tilt limits, the
    /// same critically-damped follow, the same idle float, the same
    /// gradient-spin accumulator. Three things had to be rebuilt:
    ///
    ///  * SceneKit has no counterpart here. The card is a flat image put
    ///    through Transform3DEffect (Direct2D's 4x4 transform), and its
    ///    thickness is one extra pass: the same silhouette drawn one card-depth
    ///    behind, so the tilt reveals a sliver of edge exactly where the
    ///    extrusion would have been.
    ///  * CMMotionManager is gone by request - desktops have no gyro. The
    ///    pointer drives the same gyroPitch/gyroRoll inputs the device attitude
    ///    used to, so everything downstream is unchanged.
    ///  * SCNSceneRendererDelegate becomes CanvasAnimatedControl.Update, which
    ///    is the same fixed-step callback with the same dt contract.
    /// </summary>
    public sealed class WalletCardView : UserControl
    {
        // Tilt limits (radians). The card never spins - it only leans.
        private const float MaxPitch = 0.14f;   // around X (up/down)
        private const float MaxYaw = 0.21f;     // around Y (left/right)

        // Permanent gradient spin (turns/sec); card angular velocity boosts it.
        private const float GradientBaseSpeed = 0.02f;

        // 1 unit = 100 design points in the SceneKit scene, so the camera at
        // z = 7.4 is 740 design points away. Reused verbatim as the perspective
        // depth: it is the one number that sets how strong the foreshortening
        // reads, and matching it keeps the two builds comparable.
        private const float CameraDistance = 740f;

        // SCNShape extrusionDepth 0.06 -> 6 design points.
        private const float CardDepth = 6f;

        private readonly CanvasAnimatedControl _canvas;
        private readonly WalletCardModel _model;

        private CardTextures _textures;
        private PixelShaderEffect _shader;
        private Transform3DEffect _faceTransform;
        private Transform3DEffect _slabTransform;
        private CanvasRenderTarget _slab;

        // Targets written by the pointer (UI thread), read by the render loop.
        // Plain floats, as in the original: 32-bit aligned reads cannot tear,
        // and a frame that reads a half-updated pair is indistinguishable from
        // one that sampled a moment earlier.
        private float _pointerPitch, _pointerRoll;
        private bool _isPointerOver;
        private bool _isPressed;

        // Smoothed state owned by the render loop. Starts tilted for a soft
        // "settle in" entrance, exactly like the iOS build.
        private float _currentX = 0.10f, _currentY = -0.45f;
        private float _previousX = 0.10f, _previousY = -0.45f;
        private float _gradientPhase;
        private float _gradientSpeed = GradientBaseSpeed;
        private float _scale = 1f;
        private double _time;

        public WalletCardView()
            : this(WalletCardModel.Demo)
        {
        }

        public WalletCardView(WalletCardModel model)
        {
            _model = model;

            _canvas = new CanvasAnimatedControl
            {
                ClearColor = Colors.Transparent,
                // A null Background is not hit-testable, and then no pointer
                // event ever reaches the handlers below.
                Background = new SolidColorBrush(Colors.Transparent),
                // 60Hz fixed step, matching preferredFramesPerSecond.
                TargetElapsedTime = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60),
                IsFixedTimeStep = true
            };

            _canvas.CreateResources += OnCreateResources;
            _canvas.Update += OnUpdate;
            _canvas.Draw += OnDraw;

            Content = _canvas;

            PointerMoved += OnPointerMoved;
            PointerExited += OnPointerExited;
            PointerPressed += OnPointerPressed;
            PointerReleased += OnPointerReleased;
            PointerCaptureLost += OnPointerCaptureLost;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _canvas.Paused = false;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // A continuously-rendering control that outlives its page is a GPU
            // job nobody is watching; RemoveFromVisualTree also tears down the
            // game loop thread. One-way - this control does not come back.
            _canvas.Paused = true;

            _canvas.CreateResources -= OnCreateResources;
            _canvas.Update -= OnUpdate;
            _canvas.Draw -= OnDraw;

            PointerMoved -= OnPointerMoved;
            PointerExited -= OnPointerExited;
            PointerPressed -= OnPointerPressed;
            PointerReleased -= OnPointerReleased;
            PointerCaptureLost -= OnPointerCaptureLost;

            Loaded -= OnLoaded;
            Unloaded -= OnUnloaded;

            _canvas.RemoveFromVisualTree();

            // Effects and render targets hold GPU memory that the finalizer
            // would only give back at the GC's convenience.
            _faceTransform?.Dispose();
            _faceTransform = null;
            _slabTransform?.Dispose();
            _slabTransform = null;
            _shader?.Dispose();
            _shader = null;
            _slab?.Dispose();
            _slab = null;
            _textures?.Dispose();
            _textures = null;
        }

        // MARK: - Resources

        private void OnCreateResources(ICanvasAnimatedControl sender, CanvasCreateResourcesEventArgs args)
        {
            args.TrackAsyncAction(CreateResourcesAsync(sender).AsAsyncAction());
        }

        private async Task CreateResourcesAsync(ICanvasAnimatedControl sender)
        {
            try
            {
                await CreateResourcesCoreAsync(sender);
            }
            catch (Exception ex)
            {
                // CreateResources runs on the game-loop thread, where a throw
                // does not reach Application.UnhandledException with a usable
                // stack. Catch it where the context still exists.
                Log(ex.ToString());
                throw;
            }
        }

        private static void Log(string text)
        {
            try
            {
                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, "resources.txt"), text);
            }
            catch
            {
            }
        }

        private async Task CreateResourcesCoreAsync(ICanvasAnimatedControl sender)
        {
            _textures?.Dispose();
            _textures = await CardTextures.CreateAsync(sender, _model, sender.Dpi);

            _shader?.Dispose();
            _shader = await CreateShaderAsync(sender);

            if (_shader != null)
            {
                _shader.Source1 = _textures.Overlay;
                _shader.Source2 = _textures.Stars;

                // Both inputs are complex only so the shader can read
                // D2DGetInputCoordinate - the UV that the iOS geometry modifier
                // computed. OneToOne says every output pixel reads the input
                // pixel underneath it, which is what makes that UV meaningful.
                _shader.Source1Mapping = SamplerCoordinateMapping.OneToOne;
                _shader.Source2Mapping = SamplerCoordinateMapping.OneToOne;

                _shader.Properties["cardSize"] = new Vector2(CardDesign.Width, CardDesign.Height);
                _shader.Properties["cornerRadius"] = CardDesign.CornerRadius;

                _faceTransform?.Dispose();
                _faceTransform = new Transform3DEffect
                {
                    Source = _shader,
                    // D2D's 3D transform takes only the modes its own sampler
                    // knows; HighQualityCubic is not one of them. Anisotropic is
                    // the right pick anyway - the card is foreshortened, so the
                    // sample footprint is not square.
                    InterpolationMode = CanvasImageInterpolation.Anisotropic
                };
            }

            _slab?.Dispose();
            _slab = CreateSlab(sender);

            _slabTransform?.Dispose();
            _slabTransform = new Transform3DEffect
            {
                Source = _slab,
                InterpolationMode = CanvasImageInterpolation.Anisotropic
            };
        }

        private async Task<PixelShaderEffect> CreateShaderAsync(ICanvasAnimatedControl sender)
        {
            var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Shaders/CardFront.bin"));
            var buffer = await FileIO.ReadBufferAsync(file);

            var bytes = new byte[buffer.Length];
            DataReader.FromBuffer(buffer).ReadBytes(bytes);

            var effect = new PixelShaderEffect(bytes);

            // IsSupported is an instance method - it answers for this shader on
            // this device, not for the feature in general. Feature level 10 is
            // the floor for D2D custom effects; below it the card still draws,
            // it just loses the gradient, which is a branch worth having.
            if (!effect.IsSupported(sender.Device))
            {
                effect.Dispose();
                return null;
            }

            return effect;
        }

        /// <summary>
        /// The card's extrusion, drawn one card-depth behind the front face so
        /// the tilt uncovers it along two edges. A vertical ramp stands in for
        /// SCNShape's separate side and chamfer materials: the lit chamfer sits
        /// at the top, the darker extrusion side below.
        /// </summary>
        private static CanvasRenderTarget CreateSlab(ICanvasAnimatedControl sender)
        {
            var slab = new CanvasRenderTarget(sender, CardDesign.Width, CardDesign.Height, sender.Dpi);

            using (var ds = slab.CreateDrawingSession())
            {
                ds.Clear(Colors.Transparent);

                using var brush = new CanvasLinearGradientBrush(ds, new[]
                {
                    // bevel: UIColor(0.81, 0.89, 0.98) / side: UIColor(0.34, 0.63, 0.88)
                    new CanvasGradientStop { Position = 0f, Color = Color.FromArgb(255, 207, 227, 250) },
                    new CanvasGradientStop { Position = 0.45f, Color = Color.FromArgb(255, 87, 161, 224) },
                    new CanvasGradientStop { Position = 1f, Color = Color.FromArgb(255, 48, 96, 145) },
                }, CanvasEdgeBehavior.Clamp, CanvasAlphaMode.Premultiplied)
                {
                    StartPoint = new Vector2(0, 0),
                    EndPoint = new Vector2(0, CardDesign.Height)
                };

                ds.FillRoundedRectangle(new Rect(0, 0, CardDesign.Width, CardDesign.Height),
                    CardDesign.CornerRadius, CardDesign.CornerRadius, brush);
            }

            return slab;
        }

        // MARK: - Pointer
        //
        // The gyro's job, done by the pointer. Hover rather than press, unlike
        // Unigram's VisualUtilities.AttachTilt: this is a demo whose whole point
        // is the effect, and there is no ScrollViewer here whose pan gesture a
        // captured press would eat. Wire it to press when it moves into the app.

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(this).Position;
            var size = RenderSize;

            if (size.Width <= 0 || size.Height <= 0)
            {
                return;
            }

            // -1..1 from the centre, then straight onto the same limits the
            // device attitude fed. Pointer towards an edge pushes that edge away,
            // which is the direction a real card would go under a finger.
            var x = (float)(point.X / size.Width * 2 - 1);
            var y = (float)(point.Y / size.Height * 2 - 1);

            _pointerRoll = Clamp(x * MaxYaw, MaxYaw);
            _pointerPitch = Clamp(y * MaxPitch, MaxPitch);
            _isPointerOver = true;
        }

        private void OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            _isPointerOver = false;
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _isPressed = true;
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _isPressed = false;
        }

        private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            // Matters more than PointerReleased: a press that turns into a pan
            // never sends Released at all.
            _isPressed = false;
        }

        // MARK: - Render loop

        private void OnUpdate(ICanvasAnimatedControl sender, CanvasAnimatedUpdateEventArgs args)
        {
            var dt = (float)Math.Min(Math.Max(args.Timing.ElapsedTime.TotalSeconds, 0), 1.0 / 30.0);
            if (dt <= 0)
            {
                return;
            }

            _time += dt;

            // The pointer decays away when it leaves, the way the pan did.
            if (!_isPointerOver)
            {
                var decay = MathF.Exp(-dt * 4);
                _pointerPitch *= decay;
                _pointerRoll *= decay;
            }

            // Gentle idle float so the card is alive even without input.
            var idleX = 0.013f * MathF.Sin((float)_time * 0.50f);
            var idleY = 0.022f * MathF.Sin((float)_time * 0.37f + 1.6f);

            var targetX = Clamp(_pointerPitch + idleX, MaxPitch + 0.03f);
            var targetY = Clamp(_pointerRoll + idleY, MaxYaw + 0.03f);

            var k = 1 - MathF.Exp(-dt * 8);
            _currentX += (targetX - _currentX) * k;
            _currentY += (targetY - _currentY) * k;

            // popScale(1.02) as a follow rather than an SCNTransaction: the same
            // 0.3s ease-out shape, and it cannot fight the tilt for the transform.
            var targetScale = _isPressed ? 1.02f : 1f;
            _scale += (targetScale - _scale) * (1 - MathF.Exp(-dt * 10));

            // Permanent gradient spin; the card's angular velocity smoothly
            // accelerates it. The star mask rides the same phase.
            var angularVelocity = (MathF.Abs(_currentX - _previousX) + MathF.Abs(_currentY - _previousY)) / dt;
            _previousX = _currentX;
            _previousY = _currentY;

            var targetSpeed = GradientBaseSpeed + MathF.Min(angularVelocity * 0.5f, 0.30f);
            _gradientSpeed += (targetSpeed - _gradientSpeed) * (1 - MathF.Exp(-dt * 3));
            _gradientPhase = (_gradientPhase + dt * _gradientSpeed) % 1f;
        }

        private void OnDraw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
        {
            var ds = args.DrawingSession;
            var center = new Vector2((float)sender.Size.Width / 2, (float)sender.Size.Height / 2);

            DrawBackdrop(ds, sender, center);

            // Lean, then the small counter-slide the original applies so the card
            // drifts opposite the tilt instead of pivoting on the spot.
            var lean = Matrix4x4.CreateFromYawPitchRoll(_currentY, _currentX, _currentY * 0.05f);
            var slide = Matrix4x4.CreateTranslation(-_currentY * 10f, _currentX * 8f, 0);
            var projection = CreatePerspective(CameraDistance, center);

            var model = Matrix4x4.CreateScale(_scale)
                * Matrix4x4.CreateTranslation(-CardDesign.Width / 2, -CardDesign.Height / 2, 0);

            if (_slabTransform != null)
            {
                // One card-depth behind, in the card's own frame, so the lean
                // carries it in the right direction.
                _slabTransform.TransformMatrix = model
                    * Matrix4x4.CreateTranslation(0, 0, -CardDepth) * lean * slide * projection;
                ds.DrawImage(_slabTransform);
            }

            if (_faceTransform == null)
            {
                DrawFallback(ds, center);
                return;
            }

            // Boxed through IDictionary<string, object>, so this is four small
            // allocations a frame - the only ones on this path. Worth replacing
            // with a native D2D effect if this ever moves into Unigram proper.
            _shader.Properties["gradientPhase"] = _gradientPhase;
            _shader.Properties["time"] = (float)_time;
            _shader.Properties["lightDir"] = new Vector2(_currentY / MaxYaw, _currentX / MaxPitch);
            _shader.Properties["gloss"] = 1f;
            _shader.Properties["aaScale"] = sender.Dpi / 96f;

            _faceTransform.TransformMatrix = model * lean * slide * projection;
            ds.DrawImage(_faceTransform);
        }

        /// <summary>
        /// ContentView.swift's ZStack backdrop. Drawn here rather than in XAML
        /// because plain UWP has no radial gradient brush - the one in
        /// Microsoft.UI.Xaml.Media ships with WinUI 2, which this does not pull in.
        /// </summary>
        private static void DrawBackdrop(CanvasDrawingSession ds, ICanvasAnimatedControl sender, Vector2 center)
        {
            ds.Clear(Color.FromArgb(255, 9, 10, 14));

            using var glow = new CanvasRadialGradientBrush(sender, new[]
            {
                // Color(red: 0, green: 0.28, blue: 0.62).opacity(0.30), held flat
                // to startRadius 20 and faded out by endRadius 340.
                new CanvasGradientStop { Position = 0f, Color = Color.FromArgb(0x4D, 0, 71, 158) },
                new CanvasGradientStop { Position = 20f / 340f, Color = Color.FromArgb(0x4D, 0, 71, 158) },
                new CanvasGradientStop { Position = 1f, Color = Color.FromArgb(0, 0, 71, 158) },
            }, CanvasEdgeBehavior.Clamp, CanvasAlphaMode.Premultiplied)
            {
                Center = center,
                RadiusX = 340,
                RadiusY = 340
            };

            ds.FillRectangle(0, 0, (float)sender.Size.Width, (float)sender.Size.Height, glow);
        }

        /// <summary>
        /// No custom shader available: the content still needs to sit on
        /// something, so it gets the gradient's mid colour and nothing moving.
        /// </summary>
        private void DrawFallback(CanvasDrawingSession ds, Vector2 center)
        {
            if (_textures == null)
            {
                return;
            }

            var origin = center - new Vector2(CardDesign.Width / 2, CardDesign.Height / 2);
            ds.FillRoundedRectangle(new Rect(origin.X, origin.Y, CardDesign.Width, CardDesign.Height),
                CardDesign.CornerRadius, CardDesign.CornerRadius, Color.FromArgb(255, 0x0B, 0x8A, 0xFC));
            ds.DrawImage(_textures.Overlay, origin);
        }

        /// <summary>
        /// Perspective about <paramref name="center"/>, folded into one matrix.
        ///
        /// A plain 1/depth term in M34 divides everything by w, including any
        /// recentring translation that follows it - so the centre has to be
        /// pre-multiplied by w instead: x' = (x + cx(1 - z/d)) / (1 - z/d),
        /// which lands at cx + x/(1 - z/d). Hence the M31/M32 pair alongside
        /// M41/M42.
        /// </summary>
        private static Matrix4x4 CreatePerspective(float depth, Vector2 center)
        {
            var m = Matrix4x4.Identity;
            m.M34 = -1f / depth;
            m.M31 = -center.X / depth;
            m.M32 = -center.Y / depth;
            m.M41 = center.X;
            m.M42 = center.Y;
            return m;
        }

        private static float Clamp(float value, float limit)
        {
            return MathF.Max(-limit, MathF.Min(limit, value));
        }
    }
}
