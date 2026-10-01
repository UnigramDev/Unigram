//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Telegram.Services;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    /// <summary>
    /// An amount being typed, drawn rather than edited: the whole part at the control's own size
    /// and the fraction smaller beside it.
    /// </summary>
    /// <remarks>
    /// Not a TextBox, and not a RichEditBox either. Two font sizes in one editable field needs
    /// RichEdit, which costs the input filter - <c>BeforeTextChanging</c> is a TextBox event, and
    /// rejecting bad input after the fact means putting the caret back where it was on every
    /// keystroke, paste and IME composition.
    ///
    /// What makes the trade worth it is that an amount is not really text: it is appended to and
    /// backspaced, never edited in the middle and never partially selected. Windows Calculator
    /// takes the same view - its display is a TextBlock and its digits arrive through
    /// CharacterReceived - and so do the payment apps this was modelled on, which all drive a
    /// rendered display from a keypad rather than a text field.
    ///
    /// Because it is not a text field, it has to say so itself: see
    /// <see cref="AmountTextBlockAutomationPeer"/>, which is what a screen reader reads.
    /// </remarks>
    public sealed partial class AmountTextBlock : Control
    {
        private ContentPresenter PrefixPresenter;
        private ContentPresenter SuffixPresenter;
        private Grid AmountHost;

        /// <summary>
        /// What is being typed. Never empty: a field nothing has been entered into holds
        /// <see cref="Zero"/>, which is also what it draws.
        /// </summary>
        /// <remarks>
        /// There is no placeholder, because there is nothing for one to say that this doesn't. It
        /// being real content is what lets the first digit replace it the way any other digit
        /// replaces another - a pop out and a pop in, in the slot both of them share.
        /// </remarks>
        private readonly StringBuilder _typed = new(Zero);

        private const string Zero = "0";

        // What the field held before each of the last few changes. Capped because this is an
        // amount rather than a document - a handful of keystrokes and a paste is the whole of it.
        private readonly List<string> _history = new();

        private const int HistoryDepth = 64;

        public AmountTextBlock()
        {
            DefaultStyleKey = typeof(AmountTextBlock);

            IsTabStop = true;
            UseSystemFocusVisuals = true;

            // The drawn number is not a TextBlock, so nothing inherits these on its behalf.
            RegisterPropertyChangedCallback(FontFamilyProperty, OnFontChanged);
            RegisterPropertyChangedCallback(FontSizeProperty, OnFontChanged);
            RegisterPropertyChangedCallback(FontWeightProperty, OnFontChanged);
            RegisterPropertyChangedCallback(ForegroundProperty, OnForegroundChanged);
        }

        protected override void OnApplyTemplate()
        {
            PrefixPresenter = GetTemplateChild(nameof(PrefixPresenter)) as ContentPresenter;
            SuffixPresenter = GetTemplateChild(nameof(SuffixPresenter)) as ContentPresenter;
            AmountHost = GetTemplateChild(nameof(AmountHost)) as Grid;

            // The row re-arranges on every keystroke and is animated back into place from there,
            // so translation has to be on before the first one. On the control itself as well as
            // its parts: growing wider moves it within whatever centres it.
            ElementCompositionPreview.SetIsTranslationEnabled(this, true);

            // Paired rather than just added, so that re-templating can't subscribe twice.
            SizeChanged -= OnSizeChanged;
            SizeChanged += OnSizeChanged;

            if (PrefixPresenter != null)
            {
                PrefixPresenter.SizeChanged -= OnPrefixSizeChanged;
                PrefixPresenter.SizeChanged += OnPrefixSizeChanged;
            }

            if (AmountHost != null)
            {
                ElementCompositionPreview.SetIsTranslationEnabled(AmountHost, true);

                AmountHost.SizeChanged -= OnAmountSizeChanged;
                AmountHost.SizeChanged += OnAmountSizeChanged;
            }

            if (SuffixPresenter != null)
            {
                ElementCompositionPreview.SetIsTranslationEnabled(SuffixPresenter, true);
            }

            base.OnApplyTemplate();

            UpdateText();
        }

        #region Properties

        /// <summary>
        /// How big the fraction is drawn, the whole part taking <see cref="Control.FontSize"/>.
        /// </summary>
        public double FractionFontSize
        {
            get => (double)GetValue(FractionFontSizeProperty);
            set => SetValue(FractionFontSizeProperty, value);
        }

        public static readonly DependencyProperty FractionFontSizeProperty =
            DependencyProperty.Register(nameof(FractionFontSize), typeof(double), typeof(AmountTextBlock), new PropertyMetadata(16d, OnFontPropertyChanged));

        private static void OnFontPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AmountTextBlock)d).InvalidateFormat();
        }

        /// <summary>
        /// What is drawn before the number, a currency symbol typically.
        /// </summary>
        /// <remarks>
        /// Part of the control rather than of the screen around it, so that it moves with the
        /// number: the amount is arranged at its final width the instant a digit is typed, and
        /// what keeps that from being a jump is the whole row gliding to meet it. A prefix sitting
        /// outside would snap while the digits were still sliding.
        ///
        /// Content rather than a string, because the symbol is rarely in the same face as the
        /// number - and what font a currency mark wants is the screen's business, not the field's.
        /// </remarks>
        public object Prefix
        {
            get => GetValue(PrefixProperty);
            set => SetValue(PrefixProperty, value);
        }

        public static readonly DependencyProperty PrefixProperty =
            DependencyProperty.Register(nameof(Prefix), typeof(object), typeof(AmountTextBlock), new PropertyMetadata(null));

        /// <summary>
        /// What is drawn after the number, a currency name typically. See <see cref="Prefix"/>.
        /// </summary>
        public object Suffix
        {
            get => GetValue(SuffixProperty);
            set => SetValue(SuffixProperty, value);
        }

        public static readonly DependencyProperty SuffixProperty =
            DependencyProperty.Register(nameof(Suffix), typeof(object), typeof(AmountTextBlock), new PropertyMetadata(null));

        #endregion

        #region Text

        /// <summary>
        /// What has been typed, exactly as typed - including a trailing separator, and the zeros
        /// after it that have not been followed by a digit yet.
        /// </summary>
        /// <remarks>
        /// Kept verbatim rather than round-tripped through a number, because a number cannot hold
        /// the half-written states: "1." and "1.0" both parse as one and neither may be rewritten
        /// while the next digit is still coming.
        ///
        /// Reads as <see cref="Zero"/> rather than as nothing when nothing has been entered, which
        /// parses to the same amount as nothing did and is what the field was showing anyway.
        /// </remarks>
        public string Text
        {
            get => _typed.ToString();
            set
            {
                _typed.Clear();
                _typed.Append(string.IsNullOrEmpty(value) ? Zero : value);

                // Written by the screen rather than by the user - a swap, or an amount arriving
                // from a link. Undoing across one of those would restore a number in the other
                // unit, so this is a new starting point rather than a step.
                _history.Clear();

                UpdateText();
            }
        }

        /// <summary>
        /// Raised for anything that changed <see cref="Text"/>, typing included.
        /// </summary>
        public event EventHandler TextChanged;

        /// <summary>
        /// Answers whether the amount that would result from a keystroke is one this field accepts.
        /// Nothing is appended when it says no, so what is on screen is always sendable.
        /// </summary>
        public Func<string, bool> Validate { get; set; }

        private void UpdateText()
        {
            if (AmountHost == null)
            {
                return;
            }

            UpdateGlyphs(_typed.ToString());

            // What a screen reader reads, since there is no text field here for it to read by
            // itself. Raised as a property change so it is announced as it is typed.
            var peer = FrameworkElementAutomationPeer.FromElement(this) as AmountTextBlockAutomationPeer;
            peer?.RaiseValueChanged();
        }

        /// <summary>
        /// The decimal separator, which is the app's rather than the region's: the amount is
        /// written and read back in the language the rest of the number is in.
        /// </summary>
        private static char Separator => LocaleService.Current.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];

        #endregion

        #region Rendering

        /// <summary>
        /// Where a glyph belongs in the amount, which is what gives it an identity from one change
        /// to the next.
        /// </summary>
        /// <remarks>
        /// A digit is only ever appended to or removed from the end of its part, so its index from
        /// the start survives an edit. A group separator is re-positioned by the grouping as the
        /// number grows, so it is identified by its ordinal from the right instead - which is what
        /// makes it slide from one gap to the next rather than pop out and back in.
        /// </remarks>
        private enum GlyphPart
        {
            Whole,
            Group,
            Separator,
            Fraction
        }

        private readonly record struct GlyphKey(GlyphPart Part, int Index);

        private readonly record struct OutlineKey(int GlyphIndex, float FontSize);

        private sealed class GlyphShape
        {
            public CompositionSpriteShape Shape;
            public CompositionColorBrush Brush;
            public char Character;
            public float X;
        }

        private sealed class GlyphOutline
        {
            public CompositionPathGeometry Geometry;
            public Vector2 Center;
        }

        private readonly struct PlacedGlyph
        {
            public PlacedGlyph(float x, GlyphOutline outline)
            {
                X = x;
                Outline = outline;
            }

            public readonly float X;
            public readonly GlyphOutline Outline;
        }

        private static readonly Comparison<PlacedGlyph> _byOrigin = (x, y) => x.X.CompareTo(y.X);

        private readonly Dictionary<GlyphKey, GlyphShape> _shapes = new();

        // A character's outline doesn't depend on where it sits, so it is built once per size and
        // shared by every shape that draws it.
        private readonly Dictionary<OutlineKey, GlyphOutline> _outlines = new();

        private readonly HashSet<GlyphKey> _alive = new();

        // Where the group separators fall, filled right to left. Twenty is past anything a long
        // can hold, let alone anything this field accepts.
        private readonly int[] _breaks = new int[20];

        private static readonly TimeSpan _duration = Constants.FastAnimation; // TimeSpan.FromSeconds(0.25);

        private ShapeVisual _visual;
        private CompositionEasingFunction _easing;
        private CanvasTextFormat _format;

        private Color _color = Colors.Transparent;

        private bool _ready;

        private float _available;
        private float _scaled = 1;

        private void UpdateGlyphs(string typed)
        {
            var compositor = ElementComposition.GetElementVisual(this).Compositor;

            if (_visual == null)
            {
                _visual = compositor.CreateShapeVisual();

                // A ShapeVisual clips to its Size, and the glyphs are placed from its left edge, so
                // this only has to be larger than any amount that can be typed.
                _visual.Size = new Vector2(4096, 4096);

                ElementComposition.SetElementChildVisual(AmountHost, _visual);
            }

            var index = typed.IndexOf(Separator);
            var whole = Group(index < 0 ? typed : typed.Substring(0, index));
            var fraction = index < 0 ? string.Empty : typed.Substring(index);
            var text = whole + fraction;

            var device = ElementComposition.GetSharedDevice();

            using var layout = new CanvasTextLayout(device, text, GetFormat(), float.PositiveInfinity, 0);

            // The separator belongs to the fraction, so that it shrinks along with the digits it
            // introduces rather than sitting at full size between them. One layout rather than two,
            // because only one layout can put two sizes on the same baseline.
            if (fraction.Length > 0)
            {
                layout.SetFontSize(whole.Length, fraction.Length, (float)FractionFontSize);
            }

            // Tabular figures put every digit on the same advance, so replacing one doesn't nudge
            // the rest of the amount sideways.
            using var typography = new CanvasTypography();
            typography.AddFeature(CanvasTypographyFeatureName.TabularFigures, 1);

            layout.SetTypography(0, text.Length, typography);

            var renderer = new GlyphOutlineRenderer(device, compositor, _outlines);
            layout.DrawToTextRenderer(renderer, Vector2.Zero);

            // Set before anything is animated, so the row around the number is arranged where it
            // belongs on this very keystroke and only the pixels are left to catch up.
            AmountHost.Height = layout.LayoutBounds.Height;
            AmountHost.Width = layout.LayoutBounds.Width;

            var keys = GetKeys(text, whole.Length);
            var color = Foreground is SolidColorBrush solid ? solid.Color : Colors.Black;
            var animate = _ready;

            _ready = true;

            if (color != _color)
            {
                _color = color;

                foreach (var pair in _shapes)
                {
                    // Stopped first: a glyph still fading in would otherwise be animated back to
                    // the colour it was told to fade from.
                    pair.Value.Brush.StopAnimation("Color");
                    pair.Value.Brush.Color = color;
                }
            }

            var placed = renderer.Placed;

            // Runs arrive one per format change - the fraction is a second one - and in whatever
            // order the layout produced them, so they are put back into reading order here. An
            // amount is digits and separators in a single face: one glyph per character, left to
            // right, which is what lets a glyph be matched to a character by position at all.
            placed.Sort(_byOrigin);

            if (placed.Count != text.Length)
            {
                // Shaping disagreed, which nothing this field accepts should be able to cause.
                // A glyph no longer stands for a character, so there is no identity to animate
                // across: draw the amount again from nothing rather than guess at a pairing.
                Redraw(compositor, placed, color);
                return;
            }

            List<GlyphShape> dying = null;

            _alive.Clear();

            for (int i = 0; i < placed.Count; i++)
            {
                var character = text[i];
                var key = keys[i];

                _alive.Add(key);

                if (_shapes.TryGetValue(key, out var existing))
                {
                    if (existing.Character == character)
                    {
                        if (existing.X != placed[i].X)
                        {
                            existing.X = placed[i].X;
                            SetOffset(existing.Shape, placed[i].X, animate);
                        }

                        continue;
                    }

                    // The slot survives but holds a different character - what replacing the whole
                    // amount does - so the old one pops out where it stands while the one that
                    // takes its place pops in over it.
                    (dying ??= new List<GlyphShape>()).Add(existing);
                }

                var brush = compositor.CreateColorBrush(color);
                var shape = CreateShape(compositor, placed[i], brush);

                _shapes[key] = new GlyphShape
                {
                    Shape = shape,
                    Brush = brush,
                    Character = character,
                    X = placed[i].X
                };

                if (animate)
                {
                    AnimateIn(shape, brush, color);
                }
            }

            // Collected first: the dictionary can't be written while it is being enumerated. A
            // replaced character is never in here, because its key was taken over above.
            List<GlyphKey> dead = null;

            foreach (var pair in _shapes)
            {
                if (_alive.Contains(pair.Key) is false)
                {
                    (dead ??= new List<GlyphKey>()).Add(pair.Key);
                }
            }

            if (dead != null)
            {
                foreach (var key in dead)
                {
                    (dying ??= new List<GlyphShape>()).Add(_shapes[key]);
                    _shapes.Remove(key);
                }
            }

            if (dying != null)
            {
                var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                batch.Completed += (s, args) =>
                {
                    foreach (var glyph in dying)
                    {
                        _visual.Shapes.Remove(glyph.Shape);
                    }
                };

                foreach (var glyph in dying)
                {
                    AnimateOut(glyph.Shape, glyph.Brush, color);
                }

                batch.End();
            }
        }

        private CompositionSpriteShape CreateShape(Compositor compositor, PlacedGlyph placed, CompositionColorBrush brush)
        {
            var shape = compositor.CreateSpriteShape(placed.Outline.Geometry);
            shape.FillBrush = brush;
            shape.Offset = new Vector2(placed.X, 0);
            shape.CenterPoint = placed.Outline.Center;

            _visual.Shapes.Add(shape);
            return shape;
        }

        /// <summary>
        /// Draws the amount from nothing, for when a glyph can no longer be matched to a character.
        /// </summary>
        /// <remarks>
        /// Nothing animates and nothing is keyed: one brush for the lot, and the next change starts
        /// over as if the field had just been shown.
        /// </remarks>
        private void Redraw(Compositor compositor, List<PlacedGlyph> placed, Color color)
        {
            _shapes.Clear();
            _visual.Shapes.Clear();
            _ready = false;

            var brush = compositor.CreateColorBrush(color);

            foreach (var glyph in placed)
            {
                CreateShape(compositor, glyph, brush);
            }
        }

        /// <summary>
        /// The identity of each character of the drawn amount. See <see cref="GlyphPart"/>.
        /// </summary>
        private static GlyphKey[] GetKeys(string text, int wholeLength)
        {
            var keys = new GlyphKey[text.Length];
            var digits = 0;
            var groups = 0;

            for (int i = 0; i < wholeLength; i++)
            {
                if (char.IsDigit(text[i]))
                {
                    keys[i] = new GlyphKey(GlyphPart.Whole, digits++);
                }
            }

            // Counted per character rather than per separator, so that a group separator more than
            // one character long doesn't hand both of its characters the same key.
            for (int i = wholeLength - 1; i >= 0; i--)
            {
                if (char.IsDigit(text[i]) is false)
                {
                    keys[i] = new GlyphKey(GlyphPart.Group, groups++);
                }
            }

            for (int i = wholeLength; i < text.Length; i++)
            {
                keys[i] = i == wholeLength
                    ? new GlyphKey(GlyphPart.Separator, 0)
                    : new GlyphKey(GlyphPart.Fraction, i - wholeLength - 1);
            }

            return keys;
        }

        /// <summary>
        /// Puts the group separators into the whole part.
        /// </summary>
        /// <remarks>
        /// Drawn but never stored: <see cref="Text"/> stays what was typed, which is what the rest
        /// of the screen reads back. Done over the string rather than through a number, because
        /// there is no numeric type an arbitrary run of typed digits is guaranteed to fit in.
        /// </remarks>
        private string Group(string digits)
        {
            var format = LocaleService.Current.CurrentCulture.NumberFormat;
            var sizes = format.NumberGroupSizes;
            var separator = format.NumberGroupSeparator;

            if (sizes.Length == 0 || separator.Length == 0 || digits.Length < 2)
            {
                return digits;
            }

            // Group lengths are read from the right with the last one repeating, and a zero ends
            // the grouping - the shape NumberFormatInfo uses for scripts that don't group evenly.
            var count = 0;
            var index = digits.Length;

            for (int i = 0; count < _breaks.Length; i++)
            {
                var size = sizes[Math.Min(i, sizes.Length - 1)];

                if (size <= 0 || index - size <= 0)
                {
                    break;
                }

                index -= size;
                _breaks[count++] = index;
            }

            if (count == 0)
            {
                return digits;
            }

            var builder = new StringBuilder(digits.Length + (count * separator.Length));
            var previous = 0;

            for (int i = count - 1; i >= 0; i--)
            {
                builder.Append(digits, previous, _breaks[i] - previous);
                builder.Append(separator);

                previous = _breaks[i];
            }

            builder.Append(digits, previous, digits.Length - previous);
            return builder.ToString();
        }

        private CanvasTextFormat GetFormat()
        {
            return _format ??= new CanvasTextFormat
            {
                FontFamily = FontFamily?.Source ?? string.Empty,
                FontSize = (float)FontSize,
                FontWeight = FontWeight,
                FontStyle = FontStyle,
                FontStretch = FontStretch,
                WordWrapping = CanvasWordWrapping.NoWrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
                VerticalAlignment = CanvasVerticalAlignment.Top
            };
        }

        private void OnFontChanged(DependencyObject sender, DependencyProperty dp)
        {
            InvalidateFormat();
        }

        private void InvalidateFormat()
        {
            _format?.Dispose();
            _format = null;

            // An outline belongs to one face at one size, so nothing cached survives this. The
            // shapes go with it rather than animating: a glyph can't glide from one face to
            // another, and pretending it can is worse than drawing the amount again.
            _outlines.Clear();
            _shapes.Clear();
            _visual?.Shapes.Clear();

            _ready = false;

            UpdateText();
        }

        private void OnForegroundChanged(DependencyObject sender, DependencyProperty dp)
        {
            // Picked up by the next update rather than applied here, because that is where the
            // running animations are known about.
            UpdateText();
        }

        /// <summary>
        /// Moves the control within whatever holds it, which only its own width can do.
        /// </summary>
        /// <remarks>
        /// Half, because the control is centred - the style says so, and the arithmetic here is
        /// the reason it has to keep saying so. Worked out rather than read back off the visual:
        /// XAML writes an aligned element's offset after ArrangeOverride has returned, so there is
        /// no point in the pass where the control can ask where it ended up and be told the truth.
        /// </remarks>
        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.PreviousSize.Width > 0)
            {
                var moved = (float)(e.NewSize.Width - e.PreviousSize.Width);

                TranslateBy(this, -moved / 2);
            }

            ScaleTo(e.NewSize, e.PreviousSize.Width > 0);
        }

        /// <summary>
        /// Measures the row against infinity, so that it keeps asking for its full width even when
        /// that is more than there is room for.
        /// </summary>
        /// <remarks>
        /// A constrained measure would squeeze the row into what was on offer and re-lay-out the
        /// number inside it, which is not what too little room should mean here: the row keeps its
        /// size and is drawn smaller instead. See <see cref="ScaleTo"/>.
        ///
        /// How much room there was is taken down on the way past, this being the only place it can
        /// be had - nothing else the control is told carries it. What arrives here has already had
        /// the margin taken off it, and the style sets a large negative one to keep the layout clip
        /// away, so it has to be added back to get at the width the parent really had.
        /// </remarks>
        protected override Size MeasureOverride(Size availableSize)
        {
            var margin = Margin;
            _available = (float)(availableSize.Width + margin.Left + margin.Right);

            return base.MeasureOverride(new Size(double.PositiveInfinity, availableSize.Height));
        }

        private void OnPrefixSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Whatever the prefix gains pushes the number and the unit along by that much.
            if (e.PreviousSize.Width > 0)
            {
                var moved = (float)(e.NewSize.Width - e.PreviousSize.Width);

                TranslateBy(AmountHost, moved);
                TranslateBy(SuffixPresenter, moved);
            }
        }

        /// <summary>
        /// Moves the unit along by whatever the number just gained.
        /// </summary>
        /// <remarks>
        /// Taken from the size change rather than from where the parts ended up, because that is
        /// the whole of it: the number is the only thing between the two, so what it gains is
        /// exactly what the unit is pushed by. Nothing to read back, and no layout phase to be
        /// right about.
        ///
        /// The unit's own width doesn't come into it. Changing the currency makes the control
        /// wider or narrower without moving the left edge the unit is drawn from, so that lands on
        /// <see cref="OnSizeChanged"/> alone.
        /// </remarks>
        private void OnAmountSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.PreviousSize.Width > 0)
            {
                TranslateBy(SuffixPresenter, (float)(e.NewSize.Width - e.PreviousSize.Width));
            }
        }

        /// <summary>
        /// Puts a part back by how far layout has just moved it, and glides it to nothing.
        /// </summary>
        private void TranslateBy(UIElement element, float moved)
        {
            if (element == null || MathF.Abs(moved) < 0.5f)
            {
                return;
            }

            var visual = ElementComposition.GetElementVisual(element);
            var animation = visual.Compositor.CreateScalarKeyFrameAnimation();

            // Added to StartingValue rather than replacing it: a part still catching up from the
            // last keystroke has to carry that remainder into this one.
            animation.SetScalarParameter("Moved", -moved);
            animation.InsertExpressionKeyFrame(0, "this.StartingValue + Moved");
            animation.InsertKeyFrame(1, 0, GetEasing(visual.Compositor));
            animation.Duration = _duration;

            visual.StartAnimation("Translation.X", animation);
        }

        /// <summary>
        /// Draws the row at whatever fraction of itself there is room for, and glides it there.
        /// </summary>
        /// <remarks>
        /// Only the drawing shrinks. The control keeps the size it asked for, so the number is not
        /// re-laid-out into a smaller box and the glyphs keep the places they were animated to -
        /// which is the whole point of shrinking it this way rather than measuring it smaller.
        ///
        /// About the middle, which is what CenterPoint is set for: the box stays centred in its
        /// parent whatever it is drawn at, so scaling about its centre is what keeps the number
        /// centred as it shrinks.
        /// </remarks>
        private void ScaleTo(Size size, bool animate)
        {
            var scale = _available > 0 && size.Width > _available
                ? _available / (float)size.Width
                : 1;

            var visual = ElementComposition.GetElementVisual(this);

            // Set whether or not the scale itself changed: it is where the box's middle is now.
            visual.CenterPoint = new Vector3((float)size.Width / 2, (float)size.Height / 2, 0);

            if (scale == _scaled)
            {
                return;
            }

            _scaled = scale;

            if (animate is false)
            {
                visual.Scale = new Vector3(scale, scale, 1);
                return;
            }

            var animation = visual.Compositor.CreateVector3KeyFrameAnimation();
            animation.InsertExpressionKeyFrame(0, "this.StartingValue");
            animation.InsertKeyFrame(1, new Vector3(scale, scale, 1), GetEasing(visual.Compositor));
            animation.Duration = _duration;

            visual.StartAnimation("Scale", animation);
        }

        private CompositionEasingFunction GetEasing(Compositor compositor)
        {
            return _easing ??= compositor.CreateCubicBezierEasingFunction(new Vector2(0.25f, 0.1f), new Vector2(0.25f, 1));
        }

        private void SetOffset(CompositionShape shape, float x, bool animate)
        {
            if (animate is false)
            {
                shape.Offset = new Vector2(x, 0);
                return;
            }

            var animation = shape.Compositor.CreateVector2KeyFrameAnimation();

            // StartingValue rather than shape.Offset: reading the property back gives the last
            // value we assigned, never where a running animation has got to.
            animation.InsertExpressionKeyFrame(0, "this.StartingValue");
            animation.InsertKeyFrame(1, new Vector2(x, 0), GetEasing(shape.Compositor));
            animation.Duration = _duration;

            shape.StartAnimation("Offset", animation);
        }

        private void AnimateIn(CompositionSpriteShape shape, CompositionColorBrush brush, Color color)
        {
            var compositor = shape.Compositor;
            var easing = GetEasing(compositor);

            var scale = compositor.CreateVector2KeyFrameAnimation();
            scale.InsertKeyFrame(0, new Vector2(0.4f));
            scale.InsertKeyFrame(1, Vector2.One, easing);
            scale.Duration = _duration;

            // A shape has no opacity of its own, so the fade is the brush's alpha - which is why
            // every glyph owns its brush instead of sharing one.
            var fade = compositor.CreateColorKeyFrameAnimation();
            fade.InsertKeyFrame(0, Color.FromArgb(0, color.R, color.G, color.B));
            fade.InsertKeyFrame(1, color, easing);
            fade.Duration = _duration;

            shape.StartAnimation("Scale", scale);
            brush.StartAnimation("Color", fade);
        }

        private void AnimateOut(CompositionSpriteShape shape, CompositionColorBrush brush, Color color)
        {
            var compositor = shape.Compositor;
            var easing = GetEasing(compositor);

            var scale = compositor.CreateVector2KeyFrameAnimation();
            scale.InsertExpressionKeyFrame(0, "this.StartingValue");
            scale.InsertKeyFrame(1, new Vector2(0.4f), easing);
            scale.Duration = _duration;

            var fade = compositor.CreateColorKeyFrameAnimation();
            fade.InsertExpressionKeyFrame(0, "this.StartingValue");
            fade.InsertKeyFrame(1, Color.FromArgb(0, color.R, color.G, color.B), easing);
            fade.Duration = _duration;

            shape.StartAnimation("Scale", scale);
            brush.StartAnimation("Color", fade);
        }

        /// <summary>
        /// Walks a text layout and hands back one outline per character, at the origin, along with
        /// where in the layout each glyph of it sits.
        /// </summary>
        private sealed class GlyphOutlineRenderer : ICanvasTextRenderer
        {
            private readonly ICanvasResourceCreator _device;
            private readonly Compositor _compositor;
            private readonly Dictionary<OutlineKey, GlyphOutline> _outlines;
            private readonly List<PlacedGlyph> _placed = new();

            public GlyphOutlineRenderer(ICanvasResourceCreator device, Compositor compositor, Dictionary<OutlineKey, GlyphOutline> outlines)
            {
                _device = device;
                _compositor = compositor;
                _outlines = outlines;
            }

            public List<PlacedGlyph> Placed => _placed;

            // 96 keeps the geometry in DIPs; snapping would round baselines against Transform.
            public float Dpi => 96;
            public bool PixelSnappingDisabled => true;
            public Matrix3x2 Transform => Matrix3x2.Identity;

            public void DrawGlyphRun(Vector2 point, CanvasFontFace fontFace, float fontSize,
                CanvasGlyph[] glyphs, bool isSideways, uint bidiLevel, object brush,
                CanvasTextMeasuringMode measuringMode, string localeName, string textString,
                int[] clusterMapIndices, uint textPosition, CanvasGlyphOrientation glyphOrientation)
            {
                if (glyphs == null)
                {
                    return;
                }

                // Which character a glyph came from is not worked out here. The cluster map and
                // textPosition are the only things that could say, and what they are indexed
                // against across several runs isn't written down anywhere - getting it wrong loses
                // whole runs silently. The caller pairs them off by reading order instead, which
                // an amount can afford: see UpdateGlyphs.
                //
                // point is the run's baseline origin - its left end, or its right end when bidiLevel
                // is odd - and each glyph sits one accumulated advance further along in that
                // direction. AscenderOffset points up, so it subtracts from y.
                var direction = (bidiLevel & 1) == 1 ? -1f : 1f;
                var pen = 0f;
                var single = new CanvasGlyph[1];

                foreach (var glyph in glyphs)
                {
                    var x = point.X + (direction * (pen + glyph.AdvanceOffset));
                    pen += glyph.Advance;

                    // Keyed by the glyph the font drew rather than by the character that asked for
                    // it: that is what the outline is of, and it needs no mapping to know. One face
                    // throughout, which a font change enforces by dropping the cache.
                    var key = new OutlineKey(glyph.Index, fontSize);

                    if (_outlines.TryGetValue(key, out var outline) is false)
                    {
                        single[0] = new CanvasGlyph { Index = glyph.Index, Advance = glyph.Advance };

                        // Built at x = 0 so that the outline is reusable wherever the glyph lands.
                        // Only the baseline stays where the layout put it, which is what keeps the
                        // two sizes sitting on the same line.
                        using var geometry = CanvasGeometry.CreateGlyphRun(_device,
                            new Vector2(0, point.Y - glyph.AscenderOffset), fontFace, fontSize, single,
                            isSideways, bidiLevel, measuringMode, glyphOrientation);

                        var bounds = geometry.ComputeBounds();

                        outline = new GlyphOutline
                        {
                            Geometry = _compositor.CreatePathGeometry(new CompositionPath(geometry)),
                            Center = new Vector2((float)(bounds.X + (bounds.Width / 2)), (float)(bounds.Y + (bounds.Height / 2)))
                        };

                        _outlines[key] = outline;
                    }

                    _placed.Add(new PlacedGlyph(x, outline));
                }
            }

            public void DrawStrikethrough(Vector2 point, float width, float thickness, float offset,
                CanvasTextDirection direction, object brush, CanvasTextMeasuringMode measuringMode,
                string localeName, CanvasGlyphOrientation glyphOrientation)
            { }

            public void DrawUnderline(Vector2 point, float width, float thickness, float offset,
                float runThickness, CanvasTextDirection direction, object brush,
                CanvasTextMeasuringMode measuringMode, string localeName,
                CanvasGlyphOrientation glyphOrientation)
            { }

            public void DrawInlineObject(Vector2 point, ICanvasTextInlineObject inlineObject,
                bool isSideways, bool isRightToLeft, object brush,
                CanvasGlyphOrientation glyphOrientation)
            { }
        }

        #endregion

        #region Input

        /// <summary>
        /// Offers the field one character, and answers whether it took it.
        /// </summary>
        /// <remarks>
        /// Fed rather than listened for. Where a keystroke came from is the screen's business, not
        /// the field's: the popup wants them whichever of its parts has focus, and the one thing
        /// this must not do is guess.
        ///
        /// Backspace is the only way to take anything off - there is no caret, so there is nothing
        /// else a delete could mean.
        /// </remarks>
        public bool TryAppend(char character)
        {
            var current = _typed.ToString();

            if (character == '\b')
            {
                // A field showing only the zero is one nothing has been entered into, so there is
                // nothing behind it to go back to.
                if (current == Zero)
                {
                    return false;
                }

                Remember();

                _typed.Length--;

                if (_typed.Length == 0)
                {
                    _typed.Append(Zero);
                }
            }
            else if (char.IsDigit(character) || character == Separator)
            {
                string candidate;

                if (character == Separator)
                {
                    // One separator, in the app's own language: what is typed is read back by
                    // TryParseUnits, and a field holding both would not parse.
                    if (current.IndexOf(Separator) >= 0)
                    {
                        return false;
                    }

                    candidate = current + Separator;
                }
                else if (current == Zero)
                {
                    // The zero stands for an empty field rather than for a digit of the amount, so
                    // a digit takes its place instead of following it: there is no amount that
                    // begins with a zero and goes on in whole numbers. Another zero is the one
                    // exception and can only mean the start of a fraction, so it writes the
                    // separator that has to come after it.
                    candidate = character == '0'
                        ? Zero + Separator
                        : character.ToString();
                }
                else
                {
                    candidate = current + character;
                }

                if (Validate != null && !Validate(candidate))
                {
                    return false;
                }

                Remember();

                _typed.Clear();
                _typed.Append(candidate);
            }
            else
            {
                return false;
            }

            UpdateText();
            TextChanged?.Invoke(this, EventArgs.Empty);

            return true;
        }

        /// <summary>
        /// Offers the field a whole amount, replacing what it holds, and answers whether it took
        /// it. This is what a paste is.
        /// </summary>
        /// <remarks>
        /// Replaces rather than inserts for the same reason there is no caret: the field is a
        /// number being entered, not text being edited, so there is no position for a paste to
        /// land at. <see cref="TryUndo"/> is what makes that safe to do.
        /// </remarks>
        public bool TryReplace(string text)
        {
            var candidate = Normalize(text);
            if (candidate == null)
            {
                return false;
            }

            if (Validate != null && !Validate(candidate))
            {
                return false;
            }

            Remember();

            _typed.Clear();
            _typed.Append(candidate);

            UpdateText();
            TextChanged?.Invoke(this, EventArgs.Empty);

            return true;
        }

        /// <summary>
        /// Puts back what the field held before the last change, and answers whether there was one.
        /// </summary>
        public bool TryUndo()
        {
            if (_history.Count == 0)
            {
                return false;
            }

            var previous = _history[_history.Count - 1];
            _history.RemoveAt(_history.Count - 1);

            _typed.Clear();
            _typed.Append(previous);

            UpdateText();
            TextChanged?.Invoke(this, EventArgs.Empty);

            return true;
        }

        private void Remember()
        {
            _history.Add(_typed.ToString());

            if (_history.Count > HistoryDepth)
            {
                _history.RemoveAt(0);
            }
        }

        /// <summary>
        /// Reads a pasted string as an amount, or answers null when it is not one.
        /// </summary>
        /// <remarks>
        /// Digits and at most one separator, either spelling of it, after the surrounding
        /// whitespace is dropped. Deliberately strict: a grouped number like "1,234.56" carries
        /// two separators and no way to tell which is which that does not amount to guessing, and
        /// a wrong guess here is a wrong amount rather than a rejected paste.
        /// </remarks>
        private static string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var trimmed = text.Trim();
            var builder = new StringBuilder(trimmed.Length);
            var separators = 0;

            foreach (var character in trimmed)
            {
                if (char.IsDigit(character))
                {
                    builder.Append(character);
                }
                else if (character == '.' || character == ',')
                {
                    if (++separators > 1)
                    {
                        return null;
                    }

                    builder.Append(Separator);
                }
                else
                {
                    return null;
                }
            }

            if (builder.Length == 0)
            {
                return null;
            }

            // A leading zero is only ever written to introduce a fraction, so a pasted "0123" has
            // to lose it: the field would otherwise draw a grouped "0,123" for the amount 123.
            var candidate = builder.ToString();
            var start = 0;

            while (start + 1 < candidate.Length && candidate[start] == '0' && char.IsDigit(candidate[start + 1]))
            {
                start++;
            }

            return start > 0 ? candidate.Substring(start) : candidate;
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            base.OnPointerPressed(e);

            // The number is the field, so pressing it is what focuses it - there is no box to
            // click into.
            Focus(FocusState.Pointer);
        }

        #endregion

        #region Automation

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new AmountTextBlockAutomationPeer(this);
        }

        #endregion
    }

    /// <summary>
    /// Makes the control read as the editable number it is rather than as the static text it is
    /// made of.
    /// </summary>
    public partial class AmountTextBlockAutomationPeer : FrameworkElementAutomationPeer, IValueProvider
    {
        private readonly AmountTextBlock _owner;

        private string _announced = string.Empty;

        public AmountTextBlockAutomationPeer(AmountTextBlock owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override object GetPatternCore(PatternInterface patternInterface)
        {
            if (patternInterface == PatternInterface.Value)
            {
                return this;
            }

            return base.GetPatternCore(patternInterface);
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.Edit;
        }

        protected override string GetLocalizedControlTypeCore()
        {
            return AutomationProperties.GetLocalizedControlType(_owner);
        }

        public void RaiseValueChanged()
        {
            var value = _owner.Text;

            RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, _announced, value);
            _announced = value;
        }

        public bool IsReadOnly => false;

        public string Value => _owner.Text;

        public void SetValue(string value)
        {
            _owner.Text = value;
        }
    }
}
