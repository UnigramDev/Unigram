//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using LinqToVisualTree;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using Telegram.Charts;
using Telegram.Common;
using Telegram.Controls.Wallet;
using Telegram.Converters;
using Telegram.Native.Graphics;
using Telegram.Services;
using Telegram.Services.Wallet;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
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
    /// rather than the screen: the tilt, the material that is lit by it, and the skeleton that
    /// stands in before the numbers are known.
    /// </remarks>
    public sealed partial class WalletCard : UserControl, AnimatedImageLoader.IFrameTarget
    {
        private bool _skeleton;

        public WalletCard()
        {
            InitializeComponent();

            // The proportions of a payment card, which is what the design is drawn against.
            LayoutRoot.Constraint = new Size(360, 220);
            LayoutRoot.SizeChanged += OnSizeChanged;

            CardBalanceGram.Foreground = new SolidColorBrush(WalletCardMaterials.Blue.Accent);
            CardBalanceUsd.Foreground = new SolidColorBrush(WalletCardMaterials.Blue.Accent);

            Stage.PointerMoved += Stage_PointerMoved;
            Stage.PointerExited += Stage_PointerExited;
            Stage.PointerCanceled += Stage_PointerExited;
            Stage.PointerCaptureLost += Stage_PointerExited;

            CardAddress.SizeChanged += OnAddressSizeChanged;

            StickerLayer.Frame = StickerFrame;
            StickerLayer.IsClipped = true;
            StickerLayer.Arranged += OnStickersArranged;
            StickerLayer.HeldChanged += OnStickersHeldChanged;

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
            CreateSurface();

            // box-shadow: 0px 1px 0px 0px rgba(255, 255, 255, 0.06)
            //
            // No blur and a single pixel down: this is the highlight that makes the address read
            // as engraved into the card rather than printed on it.
            VisualUtilities.DropShadow(CardAddress, radius: 0, opacity: 1.0f,
                target: CardAddressShadow, color: Colors.White, offset: new Vector3(1, 0, 0));

            CreatePrintShadows();

            // An eject asked for before the card was in a window has been waiting for its clock.
            _flusher = AnimatedImageLoader.GetFlusher(XamlRoot);
            UpdateRendering();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            StopRendering();
            _flusher = null;

            FinishReveal();
            FinishEject();
            RestoreSlotText();

            // Put back in its slot, it was left hidden.
            _ejectSlot = null;
            LayoutRoot.Opacity = 1;

            // Shown again, it starts at rest rather than wherever the pointer left it.
            _hovering = false;
            _pitch = _yaw = _targetPitch = _targetYaw = 0;
            _lift = 1;

            DestroySurface();
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
            Tilt.CenterX = e.NewSize.Width / 2;
            Tilt.CenterY = e.NewSize.Height / 2;

            UpdateSurfaceLayout();
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
        private Ellipse _revealRim;
        private Vector2 _revealOrigin;
        private float _revealElapsed = -1;
        private bool _revealFade;

        // Where the new colours are, in face units: a circle that radius round the origin, or
        // everywhere by the mix while it is negative.
        private float _revealRadius = -1;
        private float _revealMix = 1;

        // The size the symbol is decoded and rasterized at, in face units: larger than any slot, so
        // every sprite scales it down.
        private const float PatternSourceSize = 48;

        private readonly List<PatternSymbol> _symbols = new();
        private UpgradedGift _patternGift;

        private AnimatedImage _patternSource;
        private CompositionVisualSurface _patternSurface;
        private CompositionSurfaceBrush _patternBrush;
        private ContainerVisual _patternContainer;

        private sealed class PatternSymbol
        {
            public SpriteVisual Sprite;
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

            _surface?.SetPalettes(_revealFrom.Palette, _revealTo.Palette);

            UpdatePattern(clientService, gift);
            StepReveal(0);
            UpdateRendering();
            Draw();
        }

        private Vector2 ToBox(Vector2 units)
        {
            var size = LayoutRoot.ActualSize;
            return new Vector2(units.X * size.X / FaceWidth, units.Y * size.Y / FaceHeight);
        }

        private void ApplyColors(WalletCardStyle style)
        {
            _revealRadius = -1;
            _revealMix = 1;
            _surface?.SetPalettes(style.Palette, style.Palette);

            SetAccent(style.Accent);
            Draw();
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

            if (_revealRim != null)
            {
                _revealRim.Opacity = 0;
            }

            StepSymbols(-1, Vector2.Zero, 0);
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
                _revealRadius = -1;
                _revealMix = p;
            }
            else
            {
                // Grows from the origin until it covers the whole card.
                var far = MathF.Max(MathF.Max(origin.Length(), new Vector2(size.X - origin.X, origin.Y).Length()),
                    MathF.Max(new Vector2(origin.X, size.Y - origin.Y).Length(), new Vector2(size.X - origin.X, size.Y - origin.Y).Length()));
                var radius = far * 1.05f * p;

                _revealRadius = size.X > 0 ? radius * FaceWidth / size.X : 0;
                _revealMix = 1;

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

            ClearPattern();

            if (gift?.Symbol?.Sticker == null || clientService == null)
            {
                return;
            }

            // One hidden image, rasterized once and shown by a sprite per slot, all sharing its brush.
            _patternSource = new AnimatedImage
            {
                Source = DelayedFileSource.FromSticker(clientService, gift.Symbol.Sticker),
                ReplacementColor = new SolidColorBrush(_style.Pattern),
                Width = PatternSourceSize,
                Height = PatternSourceSize,
                FrameSize = new Size(PatternSourceSize, PatternSourceSize),
                DecodeFrameType = DecodePixelType.Logical,
                AutoPlay = false,
                IsViewportAware = false,
                IsHitTestVisible = false,
                Opacity = 0
            };

            PatternLayer.Children.Add(_patternSource);

            var compositor = ElementComposition.GetElementVisual(PatternLayer).Compositor;

            _patternSurface = compositor.CreateVisualSurface();
            _patternSurface.SourceVisual = ElementComposition.GetElementVisual(_patternSource);
            _patternSurface.SourceSize = new Vector2(PatternSourceSize);

            _patternBrush = compositor.CreateSurfaceBrush(_patternSurface);
            _patternBrush.Stretch = CompositionStretch.Fill;

            _patternContainer = compositor.CreateContainerVisual();

            foreach (var (x, y, size, alpha) in PatternSlots(FaceWidth, FaceHeight))
            {
                var sprite = compositor.CreateSpriteVisual();
                sprite.Brush = _patternBrush;
                sprite.Size = new Vector2(size);
                sprite.CenterPoint = new Vector3(size / 2, size / 2, 0);

                _patternContainer.Children.InsertAtTop(sprite);
                _symbols.Add(new PatternSymbol { Sprite = sprite, X = x, Y = y, Size = size, Alpha = alpha });
            }

            ElementCompositionPreview.SetElementChildVisual(PatternLayer, _patternContainer);

            StepSymbols(-1, Vector2.Zero, 0);
        }

        private void ClearPattern()
        {
            if (_patternSource == null)
            {
                return;
            }

            ElementCompositionPreview.SetElementChildVisual(PatternLayer, null);

            foreach (var symbol in _symbols)
            {
                symbol.Sprite.Dispose();
            }

            _symbols.Clear();

            _patternContainer.Dispose();
            _patternBrush.Dispose();
            _patternSurface.Dispose();

            PatternLayer.Children.Remove(_patternSource);

            _patternSource = null;
            _patternSurface = null;
            _patternBrush = null;
            _patternContainer = null;
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

                symbol.Sprite.Scale = new Vector3(sizeK, sizeK, 1);
                symbol.Sprite.Offset = new Vector3(symbol.X - symbol.Size / 2 + dx, symbol.Y - symbol.Size / 2 + dy, 0);
                symbol.Sprite.Opacity = symbol.Alpha * opacity * pop;
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

        #region Material and tilt

        // The tilt the pointer reaches at the card's edge, in radians, and how much of it.
        private const double MaxPitch = 0.20;
        private const double MaxYaw = 0.30;
        private const double TiltStrength = 0.7;

        // Lifted while the pointer is over it, and shadowed by as much.
        private const double LiftScale = 1.01;
        private const float MaxShadowOpacity = 0.16f;

        // The thickness in face units, as plates from the back one forwards.
        private const double Thickness = 9;
        private static readonly double[] PlateDepths = { 1, 2 / 3.0, 1 / 3.0 };

        // At rest the stars would cost a draw every frame for as long as the card is on screen, so
        // they twinkle only while the card is moving.
        private static readonly bool TwinkleWhileIdle = false;

        private WalletCardSurface _surface;
        private CompositionGraphicsDevice _device;
        private CompositionSurfaceBrush _faceBrush;
        private CompositionSurfaceBrush _edgeBrush;
        private SpriteVisual _face;
        private SpriteVisual _shadow;
        private ContainerVisual _plateContainer;
        private SpriteVisual[] _plates;
        private DropShadow _dropShadow;
        private XamlRoot _xamlRoot;

        // DIPs and pixels per face unit, as the surface was last laid out.
        private float _units;
        private float _scale;

        private AnimatedImageLoader.FrameFlusher _flusher;
        private bool _rendering;
        private long _lastFrame;

        // Where the pointer asks the card to be, and where it is on its way there.
        private bool _hovering;
        private double _targetPitch;
        private double _targetYaw;
        private double _pitch;
        private double _yaw;
        private double _lift = 1;
        private double _time;

        private void CreateSurface()
        {
            if (_surface != null)
            {
                return;
            }

            var compositor = ElementComposition.GetElementVisual(this).Compositor;

            _device = Direct2D.Current.Device;
            _surface = new WalletCardSurface(_device);

            if (_revealElapsed >= 0)
            {
                _surface.SetPalettes(_revealFrom.Palette, _revealTo.Palette);
            }
            else
            {
                _surface.SetPalettes(_style.Palette, _style.Palette);
            }

            _faceBrush = CreateSurfaceBrush(compositor);
            _edgeBrush = CreateSurfaceBrush(compositor);

            _dropShadow = compositor.CreateDropShadow();
            _dropShadow.Mask = _faceBrush;
            _dropShadow.Color = Colors.Black;
            _dropShadow.Opacity = 0;

            _shadow = compositor.CreateSpriteVisual();
            _shadow.Shadow = _dropShadow;
            ElementCompositionPreview.SetElementChildVisual(ShadowHost, _shadow);

            _plateContainer = compositor.CreateContainerVisual();
            _plates = new SpriteVisual[PlateDepths.Length];

            for (int i = 0; i < _plates.Length; i++)
            {
                _plates[i] = compositor.CreateSpriteVisual();
                _plates[i].Brush = _edgeBrush;
                _plateContainer.Children.InsertAtTop(_plates[i]);
            }

            ElementCompositionPreview.SetElementChildVisual(PlateHost, _plateContainer);

            _face = compositor.CreateSpriteVisual();
            _face.Brush = _faceBrush;
            ElementCompositionPreview.SetElementChildVisual(FaceHost, _face);

            _xamlRoot = XamlRoot;
            _xamlRoot.Changed += OnXamlRootChanged;
            _device.RenderingDeviceReplaced += OnRenderingDeviceReplaced;

            UpdateSurfaceLayout();
        }

        private CompositionSurfaceBrush CreateSurfaceBrush(Compositor compositor)
        {
            var brush = compositor.CreateSurfaceBrush(_surface.Surface);
            brush.Stretch = CompositionStretch.None;
            brush.HorizontalAlignmentRatio = 0;
            brush.VerticalAlignmentRatio = 0;
            return brush;
        }

        private void DestroySurface()
        {
            if (_surface == null)
            {
                return;
            }

            _xamlRoot.Changed -= OnXamlRootChanged;
            _xamlRoot = null;

            _device.RenderingDeviceReplaced -= OnRenderingDeviceReplaced;
            _device = null;

            ElementCompositionPreview.SetElementChildVisual(ShadowHost, null);
            ElementCompositionPreview.SetElementChildVisual(PlateHost, null);
            ElementCompositionPreview.SetElementChildVisual(FaceHost, null);

            _shadow.Dispose();
            _face.Dispose();

            foreach (var plate in _plates)
            {
                plate.Dispose();
            }

            _plateContainer.Dispose();
            _dropShadow.Dispose();
            _faceBrush.Dispose();
            _edgeBrush.Dispose();
            _surface.Dispose();

            _surface = null;
            _shadow = null;
            _face = null;
            _plates = null;
            _plateContainer = null;
            _dropShadow = null;
            _faceBrush = null;
            _edgeBrush = null;
        }

        private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args)
        {
            if ((float)(sender.RasterizationScale * _units) != _scale)
            {
                UpdateSurfaceLayout();
            }
        }

        // Raised by the compositor, which has thrown away whatever the surface held.
        private void OnRenderingDeviceReplaced(CompositionGraphicsDevice sender, RenderingDeviceReplacedEventArgs args)
        {
            this.BeginOnUIThread(Redraw);
        }

        private void Redraw()
        {
            Draw();
        }

        // The surface is drawn at the size the card is shown, not at the face's design size, so the
        // Viewbox never stretches it.
        private void UpdateSurfaceLayout()
        {
            var width = LayoutRoot.ActualSize.X;
            if (_surface == null || width <= 0)
            {
                return;
            }

            var rasterization = _xamlRoot.RasterizationScale;
            var margin = WalletCardSurface.Margin;

            _units = width / FaceWidth;
            _scale = (float)(rasterization * _units);
            _surface.SetLayout(new Size(FaceWidth, FaceHeight), WalletStickerLayer.CornerRadius, WalletStickerLayer.ChipRect, _scale);

            var size = new Vector2(FaceWidth + margin * 2, FaceHeight + margin * 2) * _units;
            var offset = new Vector3(-margin * _units, -margin * _units, 0);

            _face.Size = size;
            _face.Offset = offset;
            _shadow.Size = size;
            _shadow.Offset = offset;

            foreach (var plate in _plates)
            {
                plate.Size = size;
            }

            _dropShadow.BlurRadius = 24 * _units;
            _dropShadow.Offset = new Vector3(0, 7 * _units, 0);

            // The surface is in pixels and the sprites in DIPs; the plate is the surface's bottom half.
            var toDips = Matrix3x2.CreateScale((float)(1 / rasterization));
            _faceBrush.TransformMatrix = toDips;
            _edgeBrush.TransformMatrix = toDips * Matrix3x2.CreateTranslation(0, -_surface.HalfHeight * _units);

            Apply();
            Draw();
        }

        private void Stage_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            // A finger only ever presses, and a drag is not a tilt. Nor is a card still coming out.
            if (e.Pointer.PointerDeviceType == PointerDeviceType.Touch || _ejectElapsed >= 0)
            {
                return;
            }

            var size = LayoutRoot.ActualSize;
            if (size.X <= 0 || size.Y <= 0)
            {
                return;
            }

            var point = e.GetCurrentPoint(Stage).Position;
            var offset = LayoutRoot.ActualOffset;

            var x = Math.Clamp((point.X - offset.X - size.X / 2) / (size.X / 2), -1, 1);
            var y = Math.Clamp((point.Y - offset.Y - size.Y / 2) / (size.Y / 2), -1, 1);

            // The side under the pointer goes down: positive yaw takes the right edge away,
            // negative pitch the bottom one.
            _targetYaw = x * MaxYaw * TiltStrength;
            _targetPitch = -y * MaxPitch * TiltStrength;
            _hovering = true;

            UpdateRendering();
        }

        private void Stage_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            _hovering = false;
            UpdateRendering();
        }

        // A sticker in hand settles the card flat under it. The layer takes every move until it is
        // let go, so the tilt comes back with the first move after that.
        private void OnStickersHeldChanged(object sender, EventArgs e)
        {
            if (StickerLayer.IsHeld)
            {
                _hovering = false;
                UpdateRendering();
            }
        }

        private bool IsIdle()
        {
            return !_hovering
                && _revealElapsed < 0
                && _ejectElapsed < 0
                && !(TwinkleWhileIdle && PowerSavingPolicy.AreSmoothTransitionsEnabled)
                && Math.Abs(_pitch) < 0.0005
                && Math.Abs(_yaw) < 0.0005
                && Math.Abs(_targetPitch) < 0.0005
                && Math.Abs(_targetYaw) < 0.0005
                && Math.Abs(_lift - 1) < 0.0001;
        }

        private void UpdateRendering()
        {
            if (IsIdle())
            {
                StopRendering();
            }
            else if (!_rendering && _flusher != null)
            {
                _rendering = true;
                _lastFrame = 0;
                _flusher.Subscribe(this);
            }
        }

        private void StopRendering()
        {
            if (_rendering)
            {
                _rendering = false;
                _flusher.Unsubscribe(this);
            }
        }

        bool AnimatedImageLoader.IFrameTarget.OnFrame(long time)
        {
            var dt = _lastFrame == 0 ? 0 : Math.Min((double)(time - _lastFrame) / Stopwatch.Frequency, 1.0 / 20);
            _lastFrame = time;

            if (PowerSavingPolicy.AreSmoothTransitionsEnabled)
            {
                _time += dt;
            }

            if (_ejectElapsed >= 0)
            {
                StepEject((float)dt);
            }

            // A released card lets go of its tilt at 4 per second, and everything chases its
            // target at 8, the lift at 12. During an eject too, for a card put back while hovered.
            if (!_hovering)
            {
                var decay = Math.Exp(-dt * 4);
                _targetPitch *= decay;
                _targetYaw *= decay;
            }

            var smoothing = 1 - Math.Exp(-dt * 8);
            _pitch += (_targetPitch - _pitch) * smoothing;
            _yaw += (_targetYaw - _yaw) * smoothing;
            _lift += ((_hovering ? LiftScale : 1) - _lift) * (1 - Math.Exp(-dt * 12));

            if (_revealElapsed >= 0)
            {
                StepReveal((float)Math.Min(dt, 0.05));
            }

            if (IsIdle())
            {
                _pitch = _yaw = _targetPitch = _targetYaw = 0;
                _lift = 1;
                StopRendering();
            }

            Apply();

            // Nothing a flight moves is on the face, which was drawn before it started: the tilt
            // that turns it is XAML's, and the stars can miss a twinkle.
            if (_ejectElapsed >= 0 && _revealElapsed < 0)
            {
                return false;
            }

            return Draw();
        }

        private void Apply()
        {
            // Positive pitch tips the top edge away and positive yaw the right one, and
            // CompositeTransform3D turns the other way about both axes.
            Tilt.RotationX = -_pitch * 180 / Math.PI;
            Tilt.RotationY = -_yaw * 180 / Math.PI;
            Tilt.RotationZ = _ejectRotation;
            Tilt.TranslateX = -_yaw * 3 * _units + _ejectTranslation.X;
            Tilt.TranslateY = _pitch * 2.5 * _units + _ejectTranslation.Y;
            Tilt.ScaleX = _lift * _ejectScale;
            Tilt.ScaleY = _lift * _ejectScale;

            if (_plates == null)
            {
                return;
            }

            // The back face is the front one moved by the thickness along the tilted z axis, with no
            // perspective of its own. A real depth would show nothing at these angles: from straight
            // in front, the plate shrinks behind the face faster than the tilt moves it out.
            var shiftX = -Thickness * Math.Sin(_yaw) * _units;
            var shiftY = Thickness * Math.Cos(_yaw) * Math.Sin(_pitch) * _units;
            var margin = WalletCardSurface.Margin * _units;

            for (int i = 0; i < _plates.Length; i++)
            {
                _plates[i].Offset = new Vector3((float)(shiftX * PlateDepths[i]) - margin, (float)(shiftY * PlateDepths[i]) - margin, 0);
            }

            var progress = Math.Clamp((_lift - 1) / (LiftScale - 1), 0, 1);
            _dropShadow.Opacity = MaxShadowOpacity * (float)(progress * progress * (3 - 2 * progress));
        }

        private bool Draw()
        {
            return _surface != null && _surface.Draw(new WalletCardFrame
            {
                Time = (float)_time,
                Pitch = (float)_pitch,
                Yaw = (float)_yaw,
                RevealCenter = _revealOrigin,
                RevealRadius = _revealRadius,
                RevealMix = _revealMix
            });
        }

        #endregion

        #region Eject

        // The whole motion, one progress driving three curves: the slide out of the slot over its
        // first 40%, and from 30% a spring for the turn and the flight, and an ease for the slot's
        // edge rising out of the way.
        private const float EjectSeconds = 0.65f;

        // Putting it back runs the same curves backwards, faster.
        private const float InsertSeconds = 0.38f;

        // The tilt about the slot line while the card comes out, in degrees: what makes it a
        // trapezoid, the part already out nearer the eye.
        private const double EjectTilt = 35;

        // How much wider than the slot the far end of the card is once it is all out. The eye is
        // put where that holds, so the trapezoid has the same shape however wide the slot is.
        private const double EjectSpread = 1.6;
        private const double RestDepth = 650;

        // The narrowest the card starts, as a share of its full size, however small the slot.
        private const float EjectMinimumScale = 0.2f;

        // Further than the card ever reaches, for a clip that only has a top edge.
        private const float SlotReach = 10000;

        // How far the slot's own text rises out of the way as the card comes out of it.
        private const float SlotTextRise = 20;

        private FrameworkElement _ejectSlot;
        private TextBlock _slotText;
        private float _ejectElapsed = -1;
        private bool _ejectStarted;
        private bool _inserting;

        // In Stage's coordinates: the slot's middle, the window's top edge, which the slot's edge
        // rises to, and where the card's centre rests.
        private Vector2 _slotCenter;
        private float _windowTop;
        private Vector2 _restCenter;

        // The card's scale on its side, which makes it exactly as wide as the slot.
        private float _slotScale;
        private CompositionRectangleGeometry _slotGeometry;

        // Where the flight has the card, on top of its tilt: moved, turned in degrees, scaled.
        private Vector2 _ejectTranslation;
        private double _ejectRotation;
        private double _ejectScale = 1;

        /// <summary>
        /// How far the card has flown from its slot towards rest, 0 to 1 on the curve the slot's
        /// edge rises on: every frame it moves, either way, and once more when it stops. What it
        /// was handed over for comes and goes with it.
        /// </summary>
        public event EventHandler<float> EjectFlightChanged;

        /// <summary>
        /// The card is back in the slot after <see cref="Insert"/>, or was never out of it.
        /// </summary>
        public event EventHandler Inserted;

        /// <summary>
        /// Plays the card coming out of <paramref name="slot"/> as a cash machine hands one back:
        /// on its side and chip end up, slid down out of it tilted towards the eye, then turned and
        /// sprung to where it rests. Can be called before the card is shown; it starts once the
        /// card has a size, and is skipped if the slot is nowhere to be seen.
        /// </summary>
        public void Eject(FrameworkElement slot)
        {
            _ejectSlot = slot;
            _ejectElapsed = 0;
            _ejectStarted = false;

            _hovering = false;
            _pitch = _yaw = _targetPitch = _targetYaw = 0;
            _lift = 1;

            // Until it has a size there is nothing to put in the slot.
            LayoutRoot.Opacity = 0;

            UpdateRendering();
        }

        /// <summary>
        /// Puts the card back into the slot <see cref="Eject"/> took it out of: the same motion
        /// played backwards, from wherever it has got to. Once in, it stays hidden.
        /// </summary>
        /// <returns>How long it will take, zero when there is nothing to put back.</returns>
        public TimeSpan Insert()
        {
            // Nothing seen to put back.
            if (_ejectSlot == null || (_ejectElapsed >= 0 && !_ejectStarted))
            {
                FinishEject();
                Inserted?.Invoke(this, EventArgs.Empty);
                return TimeSpan.Zero;
            }

            // Measured again: the window can have changed since the card came out.
            if (_ejectElapsed < 0)
            {
                _ejectElapsed = EjectSeconds;
                _ejectStarted = false;
            }

            _inserting = true;
            _hovering = false;

            UpdateRendering();

            return TimeSpan.FromSeconds(_ejectElapsed / EjectSeconds * InsertSeconds);
        }

        private bool StartEject()
        {
            // The slot is the text the button shows, not the whole button, which may be much wider.
            var text = _ejectSlot?.Descendants<TextBlock>().FirstOrDefault();
            var slot = text ?? _ejectSlot;
            if (slot == null || slot.XamlRoot != XamlRoot || slot.ActualWidth <= 0)
            {
                return false;
            }

            RestoreSlotText();
            _slotText = text;

            // Through the window: the slot is under the popup the card is in, in another tree.
            var bounds = slot.TransformToVisual(null).TransformBounds(new Rect(0, 0, slot.ActualWidth, slot.ActualHeight));
            var origin = Stage.TransformToVisual(null).TransformPoint(new Point(0, 0));

            var size = LayoutRoot.ActualSize;
            var offset = LayoutRoot.ActualOffset;

            _restCenter = new Vector2(offset.X + size.X / 2, offset.Y + size.Y / 2);
            _slotCenter = new Vector2((float)(bounds.X + bounds.Width / 2 - origin.X), (float)(bounds.Y + bounds.Height / 2 - origin.Y));
            _windowTop = (float)-origin.Y;

            // On its side the card is as wide as it is tall.
            _slotScale = Math.Clamp((float)bounds.Width / size.Y, EjectMinimumScale, 1);

            // Everything above the slot's middle is inside the machine.
            var visual = ElementComposition.GetElementVisual(Stage);
            _slotGeometry = visual.Compositor.CreateRectangleGeometry();
            _slotGeometry.Offset = new Vector2(-SlotReach, _slotCenter.Y);
            _slotGeometry.Size = new Vector2(SlotReach * 2, SlotReach);
            visual.Clip = visual.Compositor.CreateGeometricClip(_slotGeometry);

            // Out, the card on its side reaches its scaled length below the slot and leans that far
            // times sin(tilt) towards the eye; the far end then projects EjectSpread times wider.
            var lean = _slotScale * size.X * Math.Sin(EjectTilt * Math.PI / 180);
            Perspective.Depth = EjectSpread * lean / (EjectSpread - 1);

            _ejectStarted = true;
            return true;
        }

        private void StepEject(float dt)
        {
            // Not before it can be seen, so none of it plays to an empty window.
            if (_surface == null || LayoutRoot.ActualHeight <= 0)
            {
                return;
            }

            if (!_ejectStarted)
            {
                if (!StartEject())
                {
                    FinishEject();
                    return;
                }

                // The first frame it is seen is its first, however long the ones that made the
                // popup took: the clock starts on the next frame.
                dt = 0;
                _lastFrame = 0;
            }

            LayoutRoot.Opacity = 1;

            // Clamped going back: below zero is the sentinel for not ejecting at all.
            _ejectElapsed = _inserting ? Math.Max(_ejectElapsed - dt * (EjectSeconds / InsertSeconds), 0) : _ejectElapsed + dt;

            var progress = Math.Min(_ejectElapsed / EjectSeconds, 1);
            var slide = CubicBezierInterpolator.EASE_OUT.getInterpolation(Math.Clamp(progress / 0.4f, 0, 1));
            var flight = Math.Clamp((progress - 0.3f) / 0.7f, 0, 1);
            var edge = CubicBezierInterpolator.EASE_OUT_QUINT.getInterpolation(flight);

            // The slot's text makes way for the card, rising and fading as the card comes out where
            // it was.
            if (_slotText != null)
            {
                _slotText.Translation = new Vector3(0, -SlotTextRise * slide, 0);
                _slotText.Opacity = 1 - slide;
            }
            var spring = (float)Spring(flight);

            var size = LayoutRoot.ActualSize;
            var middle = size / 2;

            // On its side, the card's left edge leads it down out of the slot, and its middle is
            // where the slide has got it to; the spring takes it from there to its rest.
            var slid = new Vector2(_slotCenter.X, _slotCenter.Y + _slotScale * size.X * (slide - 0.5f));
            var center = Vector2.Lerp(slid, _restCenter, spring);
            var rotation = -90 * (1 - spring);
            var scale = _slotScale + (1 - _slotScale) * spring;

            _ejectTranslation = center - _restCenter;
            _ejectRotation = rotation;
            _ejectScale = scale;

            // The hinge is the point of the card on the slot line while it slides, and moves to its
            // middle as it flies. That is where the eye is, too.
            var hinge = Vector2.Lerp(new Vector2(size.X * slide, middle.Y), middle, edge);
            var angle = rotation * Math.PI / 180;
            var arm = (hinge - middle) * scale;
            var pivot = center + new Vector2(
                (float)(arm.X * Math.Cos(angle) - arm.Y * Math.Sin(angle)),
                (float)(arm.X * Math.Sin(angle) + arm.Y * Math.Cos(angle)));

            // Negative, because CompositeTransform3D turns the other way: the part below the slot,
            // the part out, comes towards the eye.
            Hinge.CenterX = pivot.X;
            Hinge.CenterY = pivot.Y;
            Hinge.RotationX = -EjectTilt * (1 - spring);

            Perspective.OffsetX = pivot.X - Stage.ActualWidth / 2;
            Perspective.OffsetY = pivot.Y - Stage.ActualHeight / 2;

            // The slot's edge rises to the top of the window as the card flies.
            _slotGeometry.Offset = new Vector2(-SlotReach, _slotCenter.Y + (_windowTop - _slotCenter.Y) * edge);

            EjectFlightChanged?.Invoke(this, edge);

            if (_inserting ? _ejectElapsed <= 0 : progress >= 1)
            {
                FinishEject();
            }
        }

        // The card is out for as long as it is shown, and its slot stays empty until it goes.
        private void RestoreSlotText()
        {
            if (_slotText != null)
            {
                _slotText.Translation = Vector3.Zero;
                _slotText.Opacity = 1;
                _slotText = null;
            }
        }

        // A damped spring, damping ratio 0.75 and natural frequency 10 per unit of the flight:
        // overshoots by under 3% and is all but still when the flight ends.
        private static double Spring(double t)
        {
            const double Damped = 6.6143782776614763; // 10 * sqrt(1 - 0.75 * 0.75)
            return 1 - Math.Exp(-7.5 * t) * (Math.Cos(Damped * t) + 7.5 / Damped * Math.Sin(Damped * t));
        }

        private void FinishEject()
        {
            if (_ejectElapsed < 0)
            {
                return;
            }

            var inserted = _inserting;

            _ejectElapsed = -1;
            _inserting = false;
            _slotGeometry = null;
            _ejectTranslation = Vector2.Zero;
            _ejectRotation = 0;
            _ejectScale = 1;

            Hinge.RotationX = 0;
            Perspective.Depth = RestDepth;
            Perspective.OffsetX = 0;
            Perspective.OffsetY = 0;

            ElementComposition.GetElementVisual(Stage).Clip = null;

            // Back in, it is behind the slot's edge, which goes with the clip.
            LayoutRoot.Opacity = inserted && _ejectStarted ? 0 : 1;

            // Where it stopped, which a skipped eject never stepped to.
            EjectFlightChanged?.Invoke(this, inserted ? 0 : 1);

            if (inserted)
            {
                RestoreSlotText();
                Inserted?.Invoke(this, EventArgs.Empty);
            }
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
