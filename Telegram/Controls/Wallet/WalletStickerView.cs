//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Numerics;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Native;
using Telegram.Native.Graphics;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls.Wallet
{
    /// <summary>
    /// One gift on the wallet card: its art with a white die-cut edge, the shadow it casts while it
    /// is off the card, and the curl it rolls on and peels off with.
    /// </summary>
    /// <remarks>
    /// Laid out at <see cref="BaseSize"/> in the card's face units and moved, turned and sized by
    /// its render transform alone, so a sticker in motion never measures anything. The values are
    /// springs stepped by <see cref="WalletStickerLayer"/>'s frame loop, which only runs while
    /// something is moving: gestures retarget them every frame, and composition cannot be read
    /// back.
    ///
    /// The sticker is drawn by <see cref="StickerCurlSurface"/>, flat or curled, from its first
    /// frame - or from each frame in turn while it plays. Composition cannot cut the edge well: an
    /// effect brush is clipped to the sticker's own square, which the art fills, so the edge has no
    /// room. The surface draws it at full resolution with room round it, and the flat sticker is
    /// the same drawing as the curled one. A plain AnimatedImage stands in where the surface cannot
    /// be drawn.
    /// </remarks>
    public sealed partial class WalletStickerView : UserControl
    {
        public const float BaseSize = WalletStickerLayer.FaceWidth * 0.231f;

        // The share of the square the round of a gift takes.
        public const float HitShare = 0.86f;

        // How long a sticker takes to fly back into its cell.
        private const float FlightSeconds = 0.44f;

        // The die-cut edge's blur, against the side of the frame it is cut from.
        private const float DieCutShare = 0.0144f;

        // Deep blue reads as shade on every lacquer the card can wear.
        private static readonly Color ShadowColor = Color.FromArgb(0xFF, 0x0B, 0x2E, 0x5C);

        private readonly DelayedFileSource _source;
        private readonly Grid _root;
        private readonly CompositeTransform _transform = new();

        private StickerCurlSurface _surface;
        private CompositionSurfaceBrush _brush;
        private DropShadow _shadow;
        private SpriteVisual _visual;
        private CompositionGeometricClip _clip;
        private bool _clipped;
        private AnimatedImage _fallback;
        private bool _failed;
        private bool _released;
        private bool _selected;
        private bool _drawn;

        // What the surface last drew, to draw again only when it changes.
        private float _drawnCurl = -1;
        private bool _dirty;

        private IBuffer _still;
        private int _stillWidth;
        private int _stillHeight;
        private int _side;
        private bool _stillLoading;
        private bool _peelPending;

        private LottieAnimation _animation;
        private IBuffer _frame;
        private float _playTime;
        private int _playFrame;
        private bool _playing;
        private bool _playLoading;

        public readonly StickerSpring X = new(0, 900, 0.86f);
        public readonly StickerSpring Y = new(0, 900, 0.86f);

        // The size the user gave it, against BaseSize.
        public readonly StickerSpring Scale = new(1, 700, 0.9f);

        // Degrees, clockwise.
        public readonly StickerSpring Rotation = new(0, 700, 0.9f);

        // 0 flat on the card, 1 held up off it.
        public readonly StickerSpring Lift = new(0, 380, 0.82f);

        // 0 flat, 1 all of it lifted but the leading edge.
        public readonly StickerSpring Curl = new(0, 70, 0.92f);

        // 0 gone, 1 shown.
        public readonly StickerSpring Presence = new(1, 380, 0.9f);

        // The way the sticker moves while it curls, on the card: its leading edge is the one that
        // touches first, and the opposite one lifts first.
        public Vector2 CurlDirection = new(0, 1);
        public float CurlBend = 1;

        private bool _flying;
        private float _flightTime;
        private Vector2 _flightFrom;
        private Vector2 _flightTo;
        private float _flightScaleFrom;
        private float _flightScaleTo;
        private float _flightRotationFrom;
        private float _flightRotationTo;
        private float _flightCurlFrom;

        public WalletStickerView(IClientService clientService, TonNft collectible)
        {
            Collectible = collectible;

            Width = BaseSize;
            Height = BaseSize;
            IsTabStop = true;
            UseSystemFocusVisuals = true;
            RenderTransform = _transform;

            // Not System: a touch on a sticker moves the sticker, the window does not pan under it.
            ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.TranslateY | ManipulationModes.Rotate | ManipulationModes.Scale;

            _transform.CenterX = BaseSize / 2;
            _transform.CenterY = BaseSize / 2;

            // What takes the pointer: about the shape of a gift, rather than the square round it, so
            // a sticker does not take a press meant for one under its corner.
            var hitArea = new Ellipse
            {
                Width = BaseSize * HitShare,
                Height = BaseSize * HitShare,
                Fill = new SolidColorBrush(Colors.Transparent)
            };

            _root = new Grid();
            _root.Children.Add(hitArea);

            Content = _root;

            AutomationProperties.SetName(this, collectible.Gift.ToName());

            // Asks for the file, and says when it is there; at once when it already is.
            _source = DelayedFileSource.FromSticker(clientService, collectible.Gift.Model.Sticker);
            _source.Downloaded += OnDownloaded;
        }

        private void OnDownloaded(object sender, EventArgs e)
        {
            // Raised from the file update, off the UI thread, unless the file was already there.
            this.BeginOnUIThread(PrepareStill);
        }

        public TonNft Collectible { get; }

        public string Address => Collectible.Address;

        public Vector2 Center => new(X.Value, Y.Value);

        public float Size => BaseSize * Scale.Value;

        public bool IsFlying => _flying;

        /// <summary>
        /// Leaving the card, and removed from it once it has stopped moving.
        /// </summary>
        public bool IsLeaving { get; set; }

        /// <summary>
        /// Carried over the card from the list, and not dropped on it yet.
        /// </summary>
        public bool IsPreview { get; set; }

        /// <summary>
        /// Cut by the card's edge once it is applied to the card. Not on a flight back to its cell,
        /// nor while <see cref="IsArriving"/> or <see cref="IsSelected"/>.
        /// </summary>
        public bool IsClipped { get; set; }

        /// <summary>
        /// Being worked on: whole, so the part over the edge can still be seen and taken hold of.
        /// </summary>
        public bool IsSelected
        {
            get => _selected;
            set
            {
                if (_selected != value)
                {
                    _selected = value;

                    if (_surface != null)
                    {
                        UpdateClip();
                    }
                }
            }
        }

        /// <summary>
        /// Tossed in from its cell, until it comes to rest.
        /// </summary>
        public bool IsArriving { get; set; }

        public void Place(Vector2 center, float scale, float rotation)
        {
            X.Set(center.X);
            Y.Set(center.Y);
            Scale.Set(scale);
            Rotation.Set(rotation);
            Lift.Set(0);
            Curl.Set(0);
            Presence.Set(1);
        }

        /// <summary>
        /// Plays the gift's animation once, drawn frame by frame as the still is. Animated stickers
        /// only: a gift whose art is a video or an image stays still.
        /// </summary>
        public void Play()
        {
            if (_fallback != null)
            {
                _fallback.LoopCount = 1;
                _fallback.Play();
                return;
            }

            var file = Collectible.Gift.Model.Sticker.StickerValue;
            if (_playing || _playLoading || _surface == null || !file.Local.IsDownloadingCompleted || !file.Local.Path.HasExtension(".tgs", ".json"))
            {
                return;
            }

            _playLoading = true;
            _ = PlayAsync(file.Local.Path, _side);
        }

        private async Task PlayAsync(string path, int side)
        {
            var animation = await Task.Run(() => LoadAnimation(path, side));

            _playLoading = false;

            if (animation == null || _surface == null)
            {
                animation?.Dispose();
                return;
            }

            _animation = animation;
            _frame ??= BufferSurface.Create((uint)(side * side * 4));
            _playTime = 0;
            _playFrame = -1;
            _playing = true;
        }

        private static LottieAnimation LoadAnimation(string path, int side)
        {
            try
            {
                return LottieAnimation.LoadFromFile(path, side, side, false, null);
            }
            catch
            {
                return null;
            }
        }

        private void StopPlaying()
        {
            _playing = false;

            _animation?.Dispose();
            _animation = null;

            // Back to the still the card shows at rest.
            if (_surface != null && _still != null)
            {
                _surface.SetImage(_still, _stillWidth, _stillHeight, _side * DieCutShare);
                _dirty = true;
            }
        }

        #region Motion

        /// <summary>
        /// Rolls onto the card from where it is, moving in <paramref name="direction"/>: the edge
        /// that leads touches first and the rest follows.
        /// </summary>
        public void RollOn(Vector2 direction)
        {
            CurlDirection = Normalize(direction);
            CurlBend = 1;

            // From as high as a peel takes it: the prototype's 0.9 lifts nearly the whole sheet,
            // far higher than the sticker was held.
            Curl.With(30, 1).Set(PeelPeak).To(0);
            Lift.With(150, 0.9f).To(0);
        }

        /// <summary>
        /// Comes up off the card from the edge nearest <paramref name="grab"/>, an offset from the
        /// centre, and straightens again in the air.
        /// </summary>
        public void PickUp(Vector2 grab)
        {
            // The grabbed edge has to trail, as the curl lifts the trailing edge first.
            CurlDirection = grab.LengthSquared() > 1 ? Normalize(-grab) : new Vector2(0, -1);
            CurlBend = 1;

            Lift.With(320, 0.75f).To(1);

            // A peel with nothing to draw it with would be over before the still is decoded, so
            // it waits for it: a few frames at most.
            PrepareStill();

            if (_stillLoading)
            {
                _peelPending = true;
            }
            else
            {
                Peel();
            }
        }

        // About half the sheet comes up, at an eighth of a second, and it is straight again by about
        // two thirds of one: critically damped, from a kick of v0 = peak * sqrt(k) * e.
        private const float PeelPeak = 0.5f;

        private void Peel()
        {
            Curl.With(60, 1).Set(0);
            Curl.Velocity = PeelPeak * MathF.Sqrt(60) * MathF.E;
        }

        /// <summary>
        /// Peels off the card and flies back into its cell, at <paramref name="home"/> on the card
        /// and <paramref name="homeScale"/> of its own size; with nowhere to go, it shrinks away
        /// where it is.
        /// </summary>
        public void Leave(Vector2? home, float homeScale)
        {
            IsLeaving = true;

            if (home is not Vector2 to)
            {
                Curl.With(90, 0.9f).To(0.6f);
                Lift.With(200, 0.9f).To(0.6f);
                Presence.With(260, 0.9f).To(0);
                return;
            }

            _flying = true;
            _flightTime = 0;
            _flightFrom = Center;
            _flightTo = to;
            _flightScaleFrom = Scale.Value;
            _flightScaleTo = homeScale;
            _flightRotationFrom = Rotation.Value;

            // The short way back to upright.
            var turn = -_flightRotationFrom % 360;
            turn = turn > 180 ? turn - 360 : turn < -180 ? turn + 360 : turn;
            _flightRotationTo = _flightRotationFrom + turn;

            // Away from where it is going: that edge comes up first.
            CurlDirection = Normalize(to - _flightFrom);
            _flightCurlFrom = Math.Max(Curl.Value, Lift.Value > 0.5f ? 0 : 0.9f);
        }

        /// <summary>
        /// One frame. False once nothing is moving.
        /// </summary>
        public bool Step(float dt)
        {
            if (_flying)
            {
                StepFlight(dt);
            }
            else
            {
                X.Step(dt);
                Y.Step(dt);
                Scale.Step(dt);
                Rotation.Step(dt);
                Lift.Step(dt);
                Curl.Step(dt);
            }

            Presence.Step(dt);

            if (_playing)
            {
                _playTime += dt;
            }

            var moving = !X.Resting(0.01f) || !Y.Resting(0.01f) || !Scale.Resting(0.0005f) || !Rotation.Resting(0.01f)
                || !Lift.Resting(0.001f) || !Curl.Resting(0.001f) || !Presence.Resting(0.001f);

            IsArriving &= moving;

            return _flying || _peelPending || _playing || _playLoading || moving;
        }

        /// <summary>
        /// The way back to the cell: a light toss along an arc, swelling a little mid-air with the
        /// shadow rising and falling. It straightens as it leaves, and rolls into the cell on the
        /// last stretch, about half as hard as onto the card.
        /// </summary>
        private void StepFlight(float dt)
        {
            _flightTime = Math.Min(1, _flightTime + dt / FlightSeconds);

            var t = _flightTime;
            var e = 1 - (1 - t) * (1 - t);

            var distance = Vector2.Distance(_flightFrom, _flightTo);
            var control = (_flightFrom + _flightTo) / 2 - new Vector2(0, Math.Min(90, distance * 0.28f));

            var u = 1 - e;
            var position = u * u * _flightFrom + 2 * u * e * control + e * e * _flightTo;
            var arc = MathF.Sin(MathF.PI * e);

            X.Set(position.X);
            Y.Set(position.Y);
            Scale.Set((_flightScaleFrom + (_flightScaleTo - _flightScaleFrom) * e) * (1 + 0.05f * arc));
            Rotation.Set(_flightRotationFrom + (_flightRotationTo - _flightRotationFrom) * e);
            Lift.Set(0.8f * arc);

            if (t < 0.3f)
            {
                CurlBend = 1;
                Curl.Set(_flightCurlFrom * (1 - Smooth(t / 0.3f)));
            }
            else if (t > 0.6f)
            {
                // Up into a cell the roll reads far stronger than down onto one, so it bends less
                // the more upward the flight is.
                CurlBend = 0.55f * (1 - 0.6f * Math.Clamp(-CurlDirection.Y, 0, 1));
                Curl.Set(0.45f * (1 - Smooth((t - 0.6f) / 0.4f)));
            }
            else
            {
                Curl.Set(0);
            }

            if (t >= 1)
            {
                _flying = false;
                Presence.Set(0);
            }
        }

        private static float Smooth(float t)
        {
            t = Math.Clamp(t, 0, 1);
            return t * t * (3 - 2 * t);
        }

        private static Vector2 Normalize(Vector2 direction)
        {
            return direction.LengthSquared() > 0.0001f ? Vector2.Normalize(direction) : new Vector2(0, 1);
        }

        /// <summary>
        /// Writes this frame's values to the transform, the shadow and the surface.
        /// </summary>
        public void Apply()
        {
            var lift = Math.Clamp(Lift.Value, 0, 1);
            var curl = Math.Clamp(Curl.Value, 0, 1);
            var presence = Math.Clamp(Presence.Value, 0, 1);

            _transform.TranslateX = X.Value - BaseSize / 2;
            _transform.TranslateY = Y.Value - BaseSize / 2 - lift * 4;
            _transform.ScaleX = _transform.ScaleY = Scale.Value * (1 + lift * 0.08f) * presence;
            _transform.Rotation = Rotation.Value;

            Opacity = presence;

            if (_surface == null)
            {
                return;
            }

            UpdateClip();

            // Once stuck flat the shadow has no job: it shows only while the sticker is off the
            // card, or landing.
            _shadow.Opacity = 0.35f * Math.Clamp(lift * 3 + curl, 0, 1);
            _shadow.BlurRadius = 3 + lift * 8;
            _shadow.Offset = new Vector3(0, 1.5f + lift * 5, 0);

            if (_playing)
            {
                StepAnimation();
            }

            var curled = curl > 0.004f;
            if (!_dirty && !curled && _drawnCurl == 0)
            {
                return;
            }

            // The direction on the card, into the sticker's own unturned space, which it is drawn
            // in before the transform turns it.
            var radians = -Rotation.Value * MathF.PI / 180;
            var direction = Vector2.Transform(CurlDirection, Matrix3x2.CreateRotation(radians));

            if (_surface.Draw(curled ? curl : 0, direction, CurlBend))
            {
                _drawn = true;
                _dirty = false;
                _drawnCurl = curled ? curl : 0;

                if (!curled && !_playing)
                {
                    _surface.Trim();
                }
            }
            else if (!_drawn)
            {
                // Never drawn at all: most often a GPU without the shader model the curl needs.
                Fail();
            }
            else
            {
                // A lost device; drawn again from scratch on the next frame.
                _dirty = true;
            }
        }

        private void UpdateClip()
        {
            var clipped = IsClipped && !_flying && !IsArriving && !_selected;
            if (clipped)
            {
                // Where the sprite is on the face, as the transform puts it there, inverted to
                // bring the face into the sprite.
                var margin = BaseSize * StickerCurlSurface.Margin;
                var center = new Vector2(BaseSize / 2);

                var toFace = Matrix3x2.CreateTranslation(-margin, -margin)
                    * Matrix3x2.CreateScale((float)_transform.ScaleX, center)
                    * Matrix3x2.CreateRotation((float)_transform.Rotation * MathF.PI / 180, center)
                    * Matrix3x2.CreateTranslation((float)_transform.TranslateX, (float)_transform.TranslateY);

                // Not invertible only at no size, when nothing shows anyway.
                if (Matrix3x2.Invert(toFace, out Matrix3x2 fromFace))
                {
                    _clip.TransformMatrix = fromFace;
                }
            }

            if (_clipped != clipped)
            {
                _clipped = clipped;
                _visual.Clip = clipped ? _clip : null;
            }
        }

        private void StepAnimation()
        {
            var frame = (int)(_playTime * _animation.FrameRate);
            if (frame >= _animation.TotalFrame)
            {
                StopPlaying();
                return;
            }

            if (frame != _playFrame)
            {
                _playFrame = frame;

                _animation.RenderSync(_frame, frame);
                _surface.SetImage(_frame, _side, _side, _side * DieCutShare);
                _dirty = true;
            }
        }

        #endregion

        #region Drawing

        /// <summary>
        /// Decodes the still the sticker is drawn from, once its file is there.
        /// </summary>
        public void PrepareStill()
        {
            if (_still != null || _stillLoading || _failed || _released)
            {
                return;
            }

            var file = Collectible.Gift.Model.Sticker.StickerValue;
            if (!file.Local.IsDownloadingCompleted)
            {
                return;
            }

            // Room for the largest the user can make it.
            var scale = XamlRoot?.RasterizationScale ?? 1;
            var side = (int)Math.Clamp(Math.Ceiling(BaseSize * 1.6 * scale), 64, 512);

            _stillLoading = true;
            _ = LoadStillAsync(file.Local.Path, file.Size > 0 ? file.Size : file.Local.DownloadedSize, side);
        }

        private async Task LoadStillAsync(string path, long size, int side)
        {
            var still = await Task.Run(() => DecodeStill(path, size, side));

            _stillLoading = false;

            if (_released)
            {
                return;
            }

            if (still.Buffer == null)
            {
                _peelPending = false;
                Fail();
                return;
            }

            _still = still.Buffer;
            _stillWidth = still.Width;
            _stillHeight = still.Height;
            _side = side;

            CreateSurface();
            Apply();

            // Only while it is still held up: put down meanwhile, there is nothing to peel.
            if (_peelPending)
            {
                _peelPending = false;

                if (Lift.Target > 0.5f)
                {
                    Peel();
                }
            }
        }

        private void CreateSurface()
        {
            if (_surface != null || _failed)
            {
                return;
            }

            try
            {
                var compositor = ElementComposition.GetElementVisual(this).Compositor;

                _surface = new StickerCurlSurface(Direct2D.Current.Device, _side);
                _surface.SetImage(_still, _stillWidth, _stillHeight, _side * DieCutShare);

                _brush = compositor.CreateSurfaceBrush(_surface.Surface);
                _brush.Stretch = CompositionStretch.Fill;

                // The shape of what is drawn, curl and edge included.
                _shadow = compositor.CreateDropShadow();
                _shadow.Color = ShadowColor;
                _shadow.Mask = _brush;
                _shadow.Opacity = 0;

                var margin = BaseSize * StickerCurlSurface.Margin;

                _visual = compositor.CreateSpriteVisual();
                _visual.Brush = _brush;
                _visual.Shadow = _shadow;
                _visual.Size = new Vector2(BaseSize + margin * 2);
                _visual.Offset = new Vector3(-margin, -margin, 0);

                // The card's face less its chip; Apply moves it into the sprite's space as the
                // sticker moves. Rooted with using: Win2D's UWP projection has no GC.KeepAlive.
                var chip = WalletStickerLayer.ChipRect;
                var chipRadius = WalletStickerLayer.ChipCornerRadius;

                using var face = CanvasGeometry.CreateRoundedRectangle(null, 0, 0, WalletStickerLayer.FaceWidth, WalletStickerLayer.FaceHeight, WalletStickerLayer.CornerRadius, WalletStickerLayer.CornerRadius);
                using var socket = CanvasGeometry.CreateRoundedRectangle(null, (float)chip.X, (float)chip.Y, (float)chip.Width, (float)chip.Height, chipRadius.X, chipRadius.Y);
                using var cut = face.CombineWith(socket, Matrix3x2.Identity, CanvasGeometryCombine.Exclude);

                _clip = compositor.CreateGeometricClip(compositor.CreatePathGeometry(new CompositionPath(cut)));

                ElementCompositionPreview.SetElementChildVisual(this, _visual);

                _drawnCurl = -1;
                _dirty = true;
            }
            catch
            {
                // Throws while the rendering device is lost.
                ReleaseSurface();
            }
        }

        // The first frame, which is what the card shows while nothing plays.
        private static (IBuffer Buffer, int Width, int Height) DecodeStill(string path, long size, int side)
        {
            try
            {
                if (path.HasExtension(".tgs", ".json"))
                {
                    using var animation = LottieAnimation.LoadFromFile(path, side, side, false, null);
                    if (animation == null)
                    {
                        return default;
                    }

                    var buffer = BufferSurface.Create((uint)(side * side * 4));
                    animation.RenderSync(buffer, 0);

                    return (buffer, side, side);
                }
                else if (path.HasExtension(".webp"))
                {
                    var buffer = Direct2DDevice.DrawWebP(path, side, out int width, out int height);
                    return buffer != null && width > 0 && height > 0 ? (buffer, width, height) : default;
                }
                else
                {
                    using var animation = VideoAnimation.LoadFromFile(new LocalFileSource(path, size), false, false, false);
                    if (animation == null || animation.PixelWidth <= 0 || animation.PixelHeight <= 0)
                    {
                        return default;
                    }

                    var fit = Math.Min(1, (double)side / Math.Max(animation.PixelWidth, animation.PixelHeight));
                    var width = Math.Max(1, (int)(animation.PixelWidth * fit));
                    var height = Math.Max(1, (int)(animation.PixelHeight * fit));

                    var buffer = BufferSurface.Create((uint)(width * height * 4));
                    return animation.RenderSync(buffer, width, height, true, out _) ? (buffer, width, height) : default;
                }
            }
            catch
            {
                return default;
            }
        }

        // The surface cannot be drawn here: the plain sticker instead, without an edge or a curl.
        private void Fail()
        {
            if (_failed)
            {
                return;
            }

            _failed = true;
            _playing = false;
            _peelPending = false;

            ReleaseSurface();

            _fallback = new AnimatedImage
            {
                Source = _source,
                Width = BaseSize,
                Height = BaseSize,
                FrameSize = new Windows.Foundation.Size(128, 128),
                DecodeFrameType = DecodePixelType.Logical,
                AutoPlay = false,
                IsHitTestVisible = false
            };

            _root.Children.Add(_fallback);
        }

        private void ReleaseSurface()
        {
            if (_visual != null)
            {
                ElementCompositionPreview.SetElementChildVisual(this, null);
                _visual.Dispose();
                _visual = null;
            }

            _clip?.Dispose();
            _clip = null;
            _clipped = false;

            _shadow?.Dispose();
            _shadow = null;

            _brush?.Dispose();
            _brush = null;

            _surface?.Dispose();
            _surface = null;

            _animation?.Dispose();
            _animation = null;
            _playing = false;
        }

        /// <summary>
        /// Lets go of everything the sticker draws with. The sticker leaving the tree.
        /// </summary>
        public void Release()
        {
            _released = true;

            _source.Downloaded -= OnDownloaded;
            _source.Complete();

            ReleaseSurface();

            _still = null;
            _frame = null;
        }

        #endregion
    }
}
