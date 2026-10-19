//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Telegram.Common;
using Telegram.Controls.Wallet;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Services.Wallet;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls
{
    /// <summary>
    /// The wallet, as a card: the balance on its face, the address engraved down its side.
    /// </summary>
    /// <remarks>
    /// Its own control because it is shown in two places that share nothing else - the wallet
    /// window, and the popup a dApp's connection request opens, where it is what tells the user
    /// which wallet they are about to hand over.
    ///
    /// Everything it draws comes from <see cref="SetState"/>. It owns the parts that are the card
    /// rather than the screen: the tilt, the gradient that repaints with it, and the skeleton that
    /// stands in before the numbers are known.
    /// </remarks>
    public sealed partial class WalletCard : UserControl
    {
        private readonly WalletCardSheen _sheen = new();

        private bool _skeleton;

        public WalletCard()
        {
            InitializeComponent();

            // The proportions of a payment card, which is what the design is drawn against.
            LayoutRoot.Constraint = new Size(360, 220);
            LayoutRoot.SizeChanged += OnSizeChanged;

            CardBalanceGram.Foreground = new SolidColorBrush(_sheen.Accent);
            CardBalanceUsd.Foreground = new SolidColorBrush(_sheen.Accent);

            CardAddress.SizeChanged += OnAddressSizeChanged;

            StickerLayer.Frame = StickerFrame;
            StickerLayer.IsClipped = true;
            StickerLayer.Arranged += OnStickersArranged;

            CardBalanceText.SizeChanged += OnPrintSizeChanged;
            CardBalanceUsd.SizeChanged += OnPrintSizeChanged;
            CardName.SizeChanged += OnPrintSizeChanged;

            // A press on a sticker is taken by the layer, so one that gets here is beside them.
            LayoutRoot.PointerPressed += LayoutRoot_PointerPressed;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            VisualUtilities.AttachTilt(LayoutRoot, Sheen, 10, 20, 0, OnTilt);

            // box-shadow: 0px 1px 0px 0px rgba(255, 255, 255, 0.06)
            //
            // No blur and a single pixel down: this is the highlight that makes the address read
            // as engraved into the card rather than printed on it.
            VisualUtilities.DropShadow(CardAddress, radius: 0, opacity: 1.0f,
                target: CardAddressShadow, color: Colors.White, offset: new Vector3(1, 0, 0));

            CreatePrintShadows();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            StopRendering();
            FinishReveal();

            VisualUtilities.DetachTilt(LayoutRoot);

            // Detaching stops the per-frame callback wherever it happens to be, and the card can be
            // shown again with its surface intact, so the bands are reset explicitly.
            OnTilt(Vector2.Zero, 0);
        }

        /// <summary>
        /// Draws the wallet this card stands for.
        /// </summary>
        /// <remarks>
        /// Everything at once rather than a property each: the balance and what it converts to are
        /// one answer, and showing half of it is the thing this exists to avoid.
        /// </remarks>
        public void SetState(IClientService clientService, WalletState state)
        {
            UpdateAddress(clientService, state.Address);
            UpdateBalance(clientService, state);
        }

        /// <summary>
        /// The same card, showing one transfer instead of the balance: what leaves, and the
        /// address it leaves for.
        /// </summary>
        /// <remarks>
        /// The card is the wallet, so a request to spend from it belongs on the card rather than
        /// beside it - and the address engraved is the *recipient*, which is the one thing the
        /// user has to recognise before agreeing.
        ///
        /// No skeleton and no owner name here. Both belong to the card standing for the wallet at
        /// rest; this one stands for a single act.
        /// </remarks>
        public void SetTransfer(IClientService clientService, WalletState state, string recipient, BigInteger nanograms)
        {
            UpdateAddress(clientService, recipient);

            CardName.Visibility = Visibility.Collapsed;
            CardBalanceSkeleton.Visibility = Visibility.Collapsed;
            HideSkeleton();

            CardBalanceIcon.Visibility = Visibility.Visible;
            CardBalanceText.Visibility = Visibility.Visible;

            var amount = Formatter.GramExact(nanograms);

            // Signed, because the card is the user's own wallet and this is money leaving it. The
            // minus is part of the number rather than a decoration on it.
            CardBalance.Text = "−" + amount.Integer;
            CardBalanceFraction.Text = amount.Fraction;

            if (WalletHelper.TryToCurrency(clientService, state, nanograms, out var converted))
            {
                CardBalanceUsd.Text = Formatter.FormatAmountExact(converted, WalletHelper.CurrencyDecimals, state?.Currency ?? "USD");
                CardBalanceUsd.Visibility = Visibility.Visible;
            }
            else
            {
                // No rate, so no second line rather than a number in a currency it was never
                // converted into.
                CardBalanceUsd.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateAddress(IClientService clientService, string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                CardAddress.Text = string.Empty;
            }
            else
            {
                var builder = new StringBuilder();

                for (int i = 0; i < address.Length; i += 4)
                {
                    if (i > 0)
                    {
                        builder.Append(i == 24 ? "\n" : " ");
                    }

                    builder.Append(address.Substring(i, 4).ToUpperInvariant());
                }

                CardAddress.Text = builder.ToString();
            }

            if (clientService.TryGetUser(clientService.Options.MyId, out Td.Api.User user))
            {
                CardName.Text = user.FullName().ToUpperInvariant();
            }

            // Shown again for the caller that hid it, so one card can be reused for both.
            CardName.Visibility = Visibility.Visible;
        }

        private void UpdateBalance(IClientService clientService, WalletState state)
        {
            // Half an answer is not shown: grams with no rate to convert them at would be a number
            // beside a currency it has not been converted into.
            var known = state.IsSynchronized && state.CurrencyRate > 0;

            CardBalanceIcon.Visibility = known ? Visibility.Visible : Visibility.Collapsed;
            CardBalanceText.Visibility = known ? Visibility.Visible : Visibility.Collapsed;
            CardBalanceUsd.Visibility = known ? Visibility.Visible : Visibility.Collapsed;
            CardBalanceSkeleton.Visibility = known ? Visibility.Collapsed : Visibility.Visible;

            if (!known)
            {
                ShowSkeleton();
                return;
            }

            HideSkeleton();

            var amount = Formatter.GramSummary(state.BalanceNanograms);
            CardBalance.Text = amount.Integer;
            CardBalanceFraction.Text = amount.Fraction;

            CardBalanceUsd.Text = WalletHelper.TryToCurrency(clientService, state, state.BalanceNanograms, out var converted)
                ? Formatter.FormatAmountExact(converted, WalletHelper.CurrencyDecimals, state.Currency ?? "USD")
                : string.Empty;
        }

        /// <summary>
        /// The two bars the balance and its converted line will fill, shimmering.
        /// </summary>
        /// <remarks>
        /// Once: every call builds a visual and hands it to the element, so calling it on each
        /// update would stack them. The card is drawn at a fixed design size inside a Viewbox, so
        /// these numbers are that design's, not the screen's.
        ///
        /// White rather than the theme's hover colour, which the skeleton would otherwise take: the
        /// card is the same blue in either theme, and a light theme's hover is dark.
        /// </remarks>
        private void ShowSkeleton()
        {
            if (_skeleton)
            {
                return;
            }

            _skeleton = true;

            VisualUtilities.SetSkeleton(CardBalanceSkeleton, new Vector2(180, 62),
                Color.FromArgb(0x3A, 0xFF, 0xFF, 0xFF),
                CanvasGeometry.CreateRoundedRectangle(null, 0, 0, 170, 32, 8, 8),
                CanvasGeometry.CreateRoundedRectangle(null, 0, 40, 120, 22, 8, 8));
        }

        // Collapsing the bars does not stop their sweep.
        private void HideSkeleton()
        {
            if (_skeleton)
            {
                _skeleton = false;
                VisualUtilities.HideSkeleton(CardBalanceSkeleton);
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            SheenRoot.CornerRadius = new CornerRadius(e.NewSize.Width * (3.18 / 85.60));
            RevealRoot.CornerRadius = SheenRoot.CornerRadius;

            if (_revealSheen != null && _revealSheen.Update(e.NewSize))
            {
                RevealSheen.Fill = new ImageBrush { ImageSource = _revealSheen.Source };
            }

            // A new surface only when the size actually moved, and the card settles at one size
            // and is measured again at that size on every reflow.
            if (_sheen.Update(e.NewSize))
            {
                if (Sheen.Fill is ImageBrush brush)
                {
                    brush.ImageSource = _sheen.Source;
                }
                else
                {
                    Sheen.Fill = new ImageBrush
                    {
                        ImageSource = _sheen.Source
                    };
                }
            }
        }

        /// <summary>
        /// Repaints the card's gradient for one frame of the tilt. What changes is the shape of
        /// the ramp, which is why it is a redraw rather than something the compositor can do.
        /// </summary>
        private void OnTilt(Vector2 pointer, float amount)
        {
            _sheen.Render(pointer, amount);
            _revealSheen?.Render(pointer, amount);
        }

        private void OnAddressSizeChanged(object sender, SizeChangedEventArgs e)
        {
            CardAddressShadow.RenderTransform = new CompositeTransform
            {
                Rotation = 90,
                TranslateX = -(e.NewSize.Height - e.NewSize.Width) / 2
            };
            CardAddress.RenderTransform = new CompositeTransform
            {
                Rotation = 90,
                TranslateX = -(e.NewSize.Height - e.NewSize.Width) / 2
            };
        }

        #region Gift style

        // The face's own design size, which the stickers and the pattern are laid out in: it
        // sits in the Viewbox.
        private const float FaceWidth = WalletStickerLayer.FaceWidth;
        private const float FaceHeight = WalletStickerLayer.FaceHeight;

        // The height the reveal's and the pattern's distances were tuned against.
        private const float TuningHeight = 205;

        // How long a gift's colours take to spread, and the shorter crossfade that takes them off.
        private const float RevealSeconds = 1.05f;
        private const float FadeSeconds = 0.45f;

        private WalletCardStyle _style = WalletCardMaterials.Blue;
        private WalletCardStyle _revealFrom;
        private WalletCardStyle _revealTo;
        private WalletCardSheen _revealSheen;
        private CompositionEllipseGeometry _revealGeometry;
        private Ellipse _revealRim;
        private Vector2 _revealOrigin;
        private float _revealElapsed = -1;
        private bool _revealFade;
        private TimeSpan _revealLast;
        private bool _rendering;

        private readonly List<PatternSymbol> _symbols = new();
        private UpgradedGift _patternGift;

        private sealed class PatternSymbol
        {
            public AnimatedImage Image;
            public CompositeTransform Transform;
            public float X;
            public float Y;
            public float Size;
            public float Alpha;
        }

        public WalletStickerLayer Stickers => StickerLayer;

        private void LayoutRoot_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            StickerLayer.Deselect();
        }

        /// <summary>
        /// Someone else's card as their profile shows it: the balance it reports, the owner's name,
        /// the address. The colours and the stickers come separately, as for any card.
        /// </summary>
        public void SetCard(IClientService clientService, TonWalletCard card, string owner)
        {
            UpdateAddress(clientService, card.Address);
            CardName.Text = owner?.ToUpperInvariant() ?? string.Empty;
            CardName.Visibility = Visibility.Visible;

            CardBalanceIcon.Visibility = Visibility.Visible;
            CardBalanceText.Visibility = Visibility.Visible;
            CardBalanceSkeleton.Visibility = Visibility.Collapsed;
            HideSkeleton();

            var amount = Formatter.GramSummary(card.GramAmount);
            CardBalance.Text = amount.Integer;
            CardBalanceFraction.Text = amount.Fraction;

            // In the viewer's own currency: the card says how much, not what it is worth to whom.
            var wallet = clientService.Session.Resolve<IWalletService>().State;
            if (WalletHelper.TryToCurrency(clientService, wallet, card.GramAmount, out var converted))
            {
                CardBalanceUsd.Text = Formatter.FormatAmountExact(converted, WalletHelper.CurrencyDecimals, wallet?.Currency ?? "USD");
                CardBalanceUsd.Visibility = Visibility.Visible;
            }
            else
            {
                CardBalanceUsd.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// The card's colours: a gift's, or the blue card's for null. Animated, they spread from
        /// <paramref name="origin"/>, in face units, and a gift's symbols open up as
        /// the wave passes them; taking a gift's colours off is a crossfade.
        /// </summary>
        public void SetStyle(IClientService clientService, UpgradedGift gift, Vector2 origin, bool animate)
        {
            var colors = gift?.Backdrop?.Colors;
            var style = colors != null ? WalletCardMaterials.ForBackdrop(colors.CenterColor, colors.EdgeColor) : WalletCardMaterials.Blue;

            if (!animate || LayoutRoot.ActualWidth <= 0)
            {
                FinishReveal();
                _style = style;
                ApplyColors(style);
                UpdatePattern(clientService, gift);
                return;
            }

            // A wave already running hands over the colours it was heading for.
            FinishReveal();

            _revealFrom = _style;
            _revealTo = style;
            _style = style;
            _revealFade = gift == null;
            _revealOrigin = origin;
            _revealElapsed = 0;

            _revealSheen ??= new WalletCardSheen();
            _revealSheen.SetColors(style.Background, style.Sheen, style.Accent);
            _revealSheen.Update(new Size(LayoutRoot.ActualSize.X, LayoutRoot.ActualSize.Y));
            RevealSheen.Fill = new ImageBrush { ImageSource = _revealSheen.Source };
            RevealRoot.CornerRadius = SheenRoot.CornerRadius;
            RevealRoot.Opacity = _revealFade ? 0 : 1;
            RevealRoot.Visibility = Visibility.Visible;

            var visual = ElementComposition.GetElementVisual(RevealRoot);
            if (_revealFade)
            {
                visual.Clip = null;
            }
            else
            {
                _revealGeometry ??= visual.Compositor.CreateEllipseGeometry();
                _revealGeometry.Center = ToBox(origin);
                _revealGeometry.Radius = Vector2.Zero;
                visual.Clip = visual.Compositor.CreateGeometricClip(_revealGeometry);
            }

            UpdatePattern(clientService, gift);
            StartRendering();
            StepReveal(0);
        }

        private Vector2 ToBox(Vector2 units)
        {
            var size = LayoutRoot.ActualSize;
            return new Vector2(units.X * size.X / FaceWidth, units.Y * size.Y / FaceHeight);
        }

        private void ApplyColors(WalletCardStyle style)
        {
            _sheen.SetColors(style.Background, style.Sheen, style.Accent);
            SetAccent(style.Accent);
        }

        private void SetAccent(Color accent)
        {
            if (CardBalanceGram.Foreground is SolidColorBrush gram)
            {
                gram.Color = accent;
            }

            if (CardBalanceUsd.Foreground is SolidColorBrush usd)
            {
                usd.Color = accent;
            }
        }

        private void FinishReveal()
        {
            if (_revealElapsed < 0)
            {
                return;
            }

            _revealElapsed = -1;
            ApplyColors(_style);

            RevealRoot.Visibility = Visibility.Collapsed;
            ElementComposition.GetElementVisual(RevealRoot).Clip = null;

            if (_revealRim != null)
            {
                _revealRim.Opacity = 0;
            }

            StepSymbols(-1, Vector2.Zero, 0);
        }

        private void StartRendering()
        {
            if (!_rendering)
            {
                _rendering = true;
                _revealLast = TimeSpan.Zero;
                CompositionTarget.Rendering += OnRendering;
            }
        }

        private void StopRendering()
        {
            if (_rendering)
            {
                _rendering = false;
                CompositionTarget.Rendering -= OnRendering;
            }
        }

        private void OnRendering(object sender, object e)
        {
            var time = e is RenderingEventArgs args ? args.RenderingTime : TimeSpan.Zero;
            var dt = _revealLast == TimeSpan.Zero ? 0 : (float)(time - _revealLast).TotalSeconds;
            _revealLast = time;

            if (_revealElapsed < 0)
            {
                StopRendering();
                return;
            }

            StepReveal(Math.Min(dt, 0.05f));
        }

        /// <summary>
        /// One frame of the wave: a quick start with the landing ring, then a long glide to the
        /// edges, ease-out quart. Taking colours off is a calm smoothstep crossfade.
        /// </summary>
        private void StepReveal(float dt)
        {
            _revealElapsed += dt;

            var duration = _revealFade ? FadeSeconds : RevealSeconds;
            var t = Math.Clamp(_revealElapsed / duration, 0, 1);
            var p = _revealFade ? t * t * (3 - 2 * t) : 1 - MathF.Pow(1 - t, 4);

            var size = LayoutRoot.ActualSize;
            var origin = ToBox(_revealOrigin);

            if (_revealFade)
            {
                RevealRoot.Opacity = p;
            }
            else
            {
                // Grows from the origin until it covers the whole card.
                var far = MathF.Max(MathF.Max(origin.Length(), new Vector2(size.X - origin.X, origin.Y).Length()),
                    MathF.Max(new Vector2(origin.X, size.Y - origin.Y).Length(), new Vector2(size.X - origin.X, size.Y - origin.Y).Length()));
                var radius = far * 1.05f * p;

                if (_revealGeometry != null)
                {
                    _revealGeometry.Center = origin;
                    _revealGeometry.Radius = new Vector2(radius, radius);
                }

                UpdateRim(origin, radius, p);
            }

            // The lettering crossfades while the wave sweeps over it, rather than in one frame.
            var k = Math.Clamp((p - 0.25f) / 0.45f, 0, 1);
            k = k * k * (3 - 2 * k);
            SetAccent(ColorsHelper.AlphaBlend(_revealFrom.Accent, _revealTo.Accent, k));

            StepSymbols(_revealFade ? -1 : p, _revealOrigin, _revealFade ? 0 : t);

            if (t >= 1)
            {
                FinishReveal();
            }
        }

        // A bright, thin edge of light right on the front, fading as the wave settles.
        private void UpdateRim(Vector2 origin, float radius, float p)
        {
            if (_revealRim == null)
            {
                _revealRim = new Ellipse { Stroke = new SolidColorBrush(Colors.White), IsHitTestVisible = false };
                RevealRimLayer.Children.Add(_revealRim);
            }

            var width = 9 * LayoutRoot.ActualSize.Y / TuningHeight;
            var fade = (1 - p) * (1 - p);
            var r = Math.Max(0, radius - width / 2);

            _revealRim.Width = _revealRim.Height = r * 2 + width;
            _revealRim.StrokeThickness = width;
            _revealRim.Opacity = radius > 1 ? 0.32f * fade * 0.6f : 0;
            Canvas.SetLeft(_revealRim, origin.X - r - width / 2);
            Canvas.SetTop(_revealRim, origin.Y - r - width / 2);
        }

        /// <summary>
        /// The gift's symbols across the card, under the print: patternSlots, a jittered hex
        /// lattice that keeps clear of the edges.
        /// </summary>
        private void UpdatePattern(IClientService clientService, UpgradedGift gift)
        {
            if (gift == _patternGift && gift != null)
            {
                return;
            }

            _patternGift = gift;

            foreach (var symbol in _symbols)
            {
                PatternLayer.Children.Remove(symbol.Image);
            }

            _symbols.Clear();

            if (gift?.Symbol?.Sticker == null || clientService == null)
            {
                return;
            }

            var source = DelayedFileSource.FromSticker(clientService, gift.Symbol.Sticker);
            var color = _style.Pattern;

            foreach (var (x, y, size, alpha) in PatternSlots(FaceWidth, FaceHeight))
            {
                var transform = new CompositeTransform { CenterX = size / 2, CenterY = size / 2 };
                var image = new AnimatedImage
                {
                    Source = source,
                    Width = size,
                    Height = size,
                    FrameSize = new Size(48, 48),
                    DecodeFrameType = Windows.UI.Xaml.Media.Imaging.DecodePixelType.Logical,
                    AutoPlay = false,
                    IsHitTestVisible = false,
                    RenderTransform = transform
                };

                image.SetEffect(AnimatedImageEffect.Silhouette, color, 0);
                Canvas.SetLeft(image, x - size / 2);
                Canvas.SetTop(image, y - size / 2);
                PatternLayer.Children.Add(image);

                _symbols.Add(new PatternSymbol { Image = image, Transform = transform, X = x, Y = y, Size = size, Alpha = alpha });
            }

            StepSymbols(-1, Vector2.Zero, 0);
        }

        /// <summary>
        /// Each symbol opens up as the wave front passes it: it grows in with a soft overshoot and
        /// drifts a little outward into its place. A negative progress shows them at rest.
        /// </summary>
        private void StepSymbols(float p, Vector2 origin, float t)
        {
            var opacity = _style.PatternOpacity;
            var k = FaceHeight / TuningHeight;
            var o = origin;

            var far = MathF.Max(MathF.Max(o.Length(), new Vector2(FaceWidth - o.X, o.Y).Length()),
                MathF.Max(new Vector2(o.X, FaceHeight - o.Y).Length(), new Vector2(FaceWidth - o.X, FaceHeight - o.Y).Length()));
            var waveR = far * 1.05f * p;

            foreach (var symbol in _symbols)
            {
                var sizeK = 1f;
                var pop = 1f;
                var dx = 0f;
                var dy = 0f;

                if (p >= 0)
                {
                    var distance = new Vector2(symbol.X - o.X, symbol.Y - o.Y).Length();
                    var lt = Math.Clamp((waveR + p * 120 * k - distance) / (120 * k), 0, 1);
                    var e = 1 - (1 - lt) * (1 - lt) * (1 - lt);
                    var back = e + 0.28f * MathF.Sin(MathF.PI * e);

                    sizeK = 0.2f + 0.8f * back;
                    pop = lt <= 0 ? 0 : MathF.Min(e * 1.6f, 1);

                    if (distance > 0.5f)
                    {
                        dx = -(symbol.X - o.X) / distance * 24 * k * (1 - e);
                        dy = -(symbol.Y - o.Y) / distance * 24 * k * (1 - e);
                    }
                }

                symbol.Transform.ScaleX = symbol.Transform.ScaleY = sizeK;
                symbol.Transform.TranslateX = dx;
                symbol.Transform.TranslateY = dy;
                symbol.Image.Opacity = symbol.Alpha * opacity * pop;
            }
        }

        /// <summary>
        /// patternSlots: rows 50 units apart on a hex lattice, jittered by a hash so the layout
        /// is the same every time, relaxed apart, gaps filled, and shrunk and faded towards the
        /// edges.
        /// </summary>
        private static List<(float X, float Y, float Size, float Alpha)> PatternSlots(float w, float h)
        {
            var k = h / TuningHeight;
            var row = 50 * k;
            var col = row * 2 / MathF.Sqrt(3);
            var baseSize = 22 * k;
            ReadOnlySpan<float> sizes = stackalloc float[] { 1.12f, 0.95f, 0.78f };

            static float Rnd(int seed, int salt)
            {
                unchecked
                {
                    var v = (seed ^ (salt * 83492791)) * 1103515245 + 12345;
                    return (((uint)v >> 8) & 0xFFFF) / 65535f * 2 - 1;
                }
            }

            var xs = new List<float>();
            var ys = new List<float>();
            var ss = new List<float>();
            var alphas = new List<float>();

            for (int r = -3; r < 3; r++)
            {
                var offset = (r & 1) != 0 ? col / 2 : 0;
                var n = (int)(w / col) + 2;

                for (int c = -n; c <= n; c++)
                {
                    var seed = unchecked(((r + 7) * 73856093) ^ ((c + 31) * 19349663));
                    var x = w / 2 + c * col + offset + 0.27f * col * Rnd(seed, 1);
                    var y = h / 2 + (r + 0.5f) * row + 0.27f * row * Rnd(seed, 2);

                    if (x < -col || x > w + col || y < -row || y > h + row)
                    {
                        continue;
                    }

                    var pick = ((2 * r + c + (Rnd(seed, 3) > 0.55f ? 1 : 0)) % 3 + 3) % 3;
                    xs.Add(x);
                    ys.Add(y);
                    ss.Add(sizes[pick] * baseSize * (1 + 0.05f * MathF.Sin(8.2f * x / w + 3.1f * y / h)));
                    alphas.Add(0.41f + 0.03f * Rnd(seed, 4));
                }
            }

            for (int pass = 0; pass < 8; pass++)
            {
                for (int i = 0; i < xs.Count; i++)
                {
                    for (int j = i + 1; j < xs.Count; j++)
                    {
                        var dx = xs[j] - xs[i];
                        var dy = ys[j] - ys[i];
                        var d = MathF.Sqrt(dx * dx + dy * dy);
                        var minimum = 0.62f * row + 0.3f * (ss[i] + ss[j]);

                        if (d > 0.01f && d < minimum)
                        {
                            var push = (minimum - d) * 0.25f / d;
                            xs[i] -= dx * push;
                            ys[i] -= dy * push;
                            xs[j] += dx * push;
                            ys[j] += dy * push;
                        }
                    }
                }
            }

            for (var y = row / 4; y < h; y += row / 4)
            {
                for (var x = col / 4; x < w; x += col / 4)
                {
                    var nearest = float.MaxValue;
                    for (int i = 0; i < xs.Count; i++)
                    {
                        nearest = MathF.Min(nearest, MathF.Sqrt((xs[i] - x) * (xs[i] - x) + (ys[i] - y) * (ys[i] - y)));
                    }

                    if (nearest > 0.8f * row)
                    {
                        xs.Add(x);
                        ys.Add(y);
                        ss.Add(baseSize * 0.78f);
                        alphas.Add(0.41f);
                    }
                }
            }

            static float Smooth(float e0, float e1, float x)
            {
                var t = Math.Clamp((x - e0) / (e1 - e0), 0, 1);
                return t * t * (3 - 2 * t);
            }

            var slots = new List<(float, float, float, float)>(xs.Count);
            for (int i = 0; i < xs.Count; i++)
            {
                var x = xs[i];
                var y = ys[i];
                var e = MathF.Min(MathF.Min(x, w - x), MathF.Min(y, h - y));
                var shrink = 0.72f + 0.28f * Smooth(9 * k, 40 * k, e);
                var size = ss[i] * shrink;

                if (e < size / 2 + 4 * k)
                {
                    continue;
                }

                slots.Add((x, y, size, alphas[i] * Smooth(0, 0.2f * w, MathF.Min(x, w - x)) * Smooth(0, 0.2f * h, MathF.Min(y, h - y))));
            }

            return slots;
        }

        #endregion

        #region Print over stickers

        // The white print is lost on a light sticker, so over one it casts a shadow.
        private SpriteVisual _balanceShadow;
        private SpriteVisual _usdShadow;
        private SpriteVisual _nameShadow;

        private void CreatePrintShadows()
        {
            _balanceShadow = CreatePrintShadow(CardBalanceText, CardBalanceTextShadow);
            _usdShadow = CreatePrintShadow(CardBalanceUsd, CardBalanceUsdShadow);
            _nameShadow = CreatePrintShadow(CardName, CardNameShadow);

            UpdatePrint(false);
        }

        private static SpriteVisual CreatePrintShadow(TextBlock text, Border target)
        {
            var visual = VisualUtilities.DropShadow(text, radius: 6, opacity: 0.6f, target: target, offset: new Vector3(0, 1, 0));
            visual.Opacity = 0;
            return visual;
        }

        private void OnStickersArranged(object sender, EventArgs e)
        {
            UpdatePrint(true);
        }

        private void OnPrintSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdatePrint(true);
        }

        private void UpdatePrint(bool animated)
        {
            if (!IsLoaded)
            {
                return;
            }

            UpdatePrintShadow(_balanceShadow, CardBalanceText, animated);
            UpdatePrintShadow(_usdShadow, CardBalanceUsd, animated);
            UpdatePrintShadow(_nameShadow, CardName, animated);

            // A sticker under the button or the diamond is taken by the press rather than them.
            QrButton.IsHitTestVisible = !StickerLayer.IsUnder(BoundsOnFace(QrButton));
            Diamond.IsHitTestVisible = !StickerLayer.IsUnder(BoundsOnFace(Diamond));
        }

        private Rect BoundsOnFace(FrameworkElement element)
        {
            return element.TransformToVisual(StickerLayer).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        }

        private void UpdatePrintShadow(SpriteVisual shadow, TextBlock text, bool animated)
        {
            if (shadow == null)
            {
                return;
            }

            var opacity = StickerLayer.IsUnder(BoundsOnFace(text)) ? 1f : 0f;

            if (animated)
            {
                // From wherever it is, so an arrangement in the middle of a fade turns it round.
                var animation = shadow.Compositor.CreateScalarKeyFrameAnimation();
                animation.InsertKeyFrame(1, opacity);
                animation.Duration = TimeSpan.FromMilliseconds(200);

                shadow.StartAnimation("Opacity", animation);
            }
            else
            {
                shadow.StopAnimation("Opacity");
                shadow.Opacity = opacity;
            }
        }

        #endregion
    }
}
