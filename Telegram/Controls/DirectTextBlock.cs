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
using Telegram.Composition;
using Telegram.Converters;
using Telegram.Native;
using Telegram.Native.Controls;
using Telegram.Native.Highlight;
using Telegram.Services;
using Telegram.Navigation;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls
{
    /// <summary>
    /// Text that owns its layout: one <see cref="DirectTextLayout"/> measured and drawn into a
    /// surface, where <see cref="FormattedTextBlock"/> builds an element per run and lays the
    /// same text out a second time to answer questions about it.
    ///
    /// Styled runs, coloured ranges, links, spoilers, custom emoji and inline buttons as
    /// inline objects, selection, and a UIA text pattern. No quotes and no code blocks: those
    /// are still a block each on <see cref="FormattedTextBlock"/>.
    /// </summary>
    public partial class DirectTextBlock : DirectTextBlockBase, ISelectableControl, IRelativeDateHost, ITrimmableText, ITextPresenter
    {
        private DirectTextLayout _layout;
        private string _text;

        public DirectTextBlock()
        {
            TextThroughput.ControlsMade++;

            // Built here rather than on the first render: adding a child in a layout pass
            // invalidates the pass that is running, and the pass never converges.
            _host = new Border();
            Children.Add(_host);
        }

        // The text is drawn into a visual on a child of its own rather than on this panel: an
        // element's child visual is drawn above everything under it, and the emoji have to sit
        // on top of the text, not beneath it.
        private Border _host;
        private List<CustomEmojiIcon> _emoji;
        private IList<CustomEmojiRange> _ranges;

        private AnimatedImage _spoilerPresenter;
        private IList<SpoilerRange> _spoilers;
        private IClientService _clientService;
        private Rect _spoilerBounds;

        private ContainerVisual _root;
        private SpriteVisual _visual;
        private CompositionSurfaceBrush _brush;

        // Where the text sits inside the block. The layout lays out in a box of its own width,
        // so a block arranged wider than its text has room to give away: right to left it
        // belongs before the text, centred it is split, and left to right there is none.
        //
        // Everything the layout answers is in its own coordinates, so this is added on the way
        // out and taken off on the way in - and it is zero for almost every text there is.
        private double _contentLeft;
        private CompositionDrawingSurface _surface;

        // What the surface was drawn for. Anything here moving means drawing it again.
        private Size _renderedSize;
        private double _renderedScale;
        private Color _renderedColor;

        // Dependency properties, so a template can set them and a theme resource can drive
        // them. The layout is the one that holds the values: these forward to it.
        public double FontSize
        {
            get => (double)GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public static readonly DependencyProperty FontSizeProperty =
            DependencyProperty.Register("FontSize", typeof(double), typeof(DirectTextBlock), new PropertyMetadata(14d, OnFontSizeChanged));

        private static void OnFontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as DirectTextBlock;
            sender.Layout().FontSize = (double)e.NewValue;
            sender.Invalidate();
        }

        /// <summary>
        /// The family the text is drawn in. XAML takes a chain and falls back down it; this
        /// hands DirectWrite the first name and lets it fall back on its own, which is the
        /// nearest the two get - and near enough, as the direct engine both measures and draws.
        /// </summary>
        public FontFamily FontFamily
        {
            get => (FontFamily)GetValue(FontFamilyProperty);
            set => SetValue(FontFamilyProperty, value);
        }

        public static readonly DependencyProperty FontFamilyProperty =
            DependencyProperty.Register("FontFamily", typeof(FontFamily), typeof(DirectTextBlock), new PropertyMetadata(null, OnFontFamilyChanged));

        private static void OnFontFamilyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as DirectTextBlock;
            var source = (e.NewValue as FontFamily)?.Source;

            var comma = source?.IndexOf(',') ?? -1;

            sender.Layout().FontFamily = comma < 0
                ? source ?? string.Empty
                : source[..comma].Trim();

            sender.Invalidate();
        }

        public FontWeight FontWeight
        {
            get => (FontWeight)GetValue(FontWeightProperty);
            set => SetValue(FontWeightProperty, value);
        }

        public static readonly DependencyProperty FontWeightProperty =
            DependencyProperty.Register("FontWeight", typeof(FontWeight), typeof(DirectTextBlock), new PropertyMetadata(FontWeights.Normal, OnFontWeightChanged));

        private static void OnFontWeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as DirectTextBlock;

            sender.Layout().FontWeight = ((FontWeight)e.NewValue).Weight;
            sender.Invalidate();
        }

        public FontStyle FontStyle
        {
            get => (FontStyle)GetValue(FontStyleProperty);
            set => SetValue(FontStyleProperty, value);
        }

        public static readonly DependencyProperty FontStyleProperty =
            DependencyProperty.Register("FontStyle", typeof(FontStyle), typeof(DirectTextBlock), new PropertyMetadata(FontStyle.Normal, OnFontStyleChanged));

        private static void OnFontStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as DirectTextBlock;

            // Oblique is italic to DirectWrite here: nothing in the app asks for it, and a
            // slanted upright is not what any of these texts mean.
            sender.Layout().Italic = (FontStyle)e.NewValue != FontStyle.Normal;
            sender.Invalidate();
        }

        public Brush Foreground
        {
            get => (Brush)GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        public static readonly DependencyProperty ForegroundProperty =
            DependencyProperty.Register("Foreground", typeof(Brush), typeof(DirectTextBlock), new PropertyMetadata(null, OnForegroundChanged));

        private static void OnForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as DirectTextBlock;

            // Read off the property when the text is drawn, formulas included: they are drawn
            // into the same surface, in the same colour.
            sender.InvalidateArrange();
        }

        /// <summary>
        /// The colour the link ranges are drawn in.
        /// </summary>
        public Brush HyperlinkForeground
        {
            get => (Brush)GetValue(HyperlinkForegroundProperty);
            set => SetValue(HyperlinkForegroundProperty, value);
        }

        public static readonly DependencyProperty HyperlinkForegroundProperty =
            DependencyProperty.Register("HyperlinkForeground", typeof(Brush), typeof(DirectTextBlock), new PropertyMetadata(null, OnHyperlinkForegroundChanged));

        private static void OnHyperlinkForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as DirectTextBlock;

            sender._hyperlinkBrush.UnregisterColorChangedCallback(ref sender._hyperlinkToken);
            sender._hyperlinkBrush = e.NewValue as SolidColorBrush;

            if (sender.IsConnected)
            {
                sender._hyperlinkBrush.RegisterColorChangedCallback(sender.OnHyperlinkColorChanged, ref sender._hyperlinkToken);
            }

            sender.UpdateLinkColor();
        }

        /// <summary>
        /// The colour a selection is drawn in, under the glyphs.
        /// </summary>
        public Brush SelectionHighlightColor
        {
            get => (Brush)GetValue(SelectionHighlightColorProperty);
            set => SetValue(SelectionHighlightColorProperty, value);
        }

        public static readonly DependencyProperty SelectionHighlightColorProperty =
            DependencyProperty.Register("SelectionHighlightColor", typeof(Brush), typeof(DirectTextBlock), new PropertyMetadata(null, OnSelectionHighlightColorChanged));

        private static void OnSelectionHighlightColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as DirectTextBlock;

            // Null until the first render builds the shapes, and it reads the property then.
            sender._selectionSource?.PropertyChanged(e.NewValue as SolidColorBrush, sender.IsConnected);
        }

        // The brushes above are theme resources, and a chat theme changes one by swapping the
        // Color on the very same SolidColorBrush - the property callbacks above never fire for
        // that, so the brush itself has to be watched. Only while this element is in the tree:
        // the brush outlives it by the length of the session, and a registration left on it
        // holds this element and everything it refers to.
        //
        // Foreground is left on the property alone: a theme resource re-evaluates on a light
        // or dark switch, which is a property change, and Render compares the colour it drew
        // with. A colour swapped in place on that brush would not reach it - if that turns out
        // to happen, it wants the same treatment.
        private SolidColorBrush _hyperlinkBrush;
        private long _hyperlinkToken;

        private CompositionColorSource _selectionSource;

        protected override void OnLoaded()
        {
            _hyperlinkBrush.RegisterColorChangedCallback(OnHyperlinkColorChanged, ref _hyperlinkToken);
            UpdateLinkColor();

            // Re-reads the colour as well as registering, for the one that changed while this
            // element was out of the tree.
            _selectionSource?.Register();

            // Subscribed here rather than where the text is set: a block is given its text
            // before it is added to anything, and the service is keyed by XamlRoot.
            SubscribeDates();
        }

        protected override void OnUnloaded()
        {
            _hyperlinkBrush.UnregisterColorChangedCallback(ref _hyperlinkToken);
            _selectionSource?.Unregister();

            // A pointer does not always leave before the element does, and a running timer
            // holds this block through its handler.
            UpdateToolTip(-1);

            UnsubscribeDates();
        }

        private void OnHyperlinkColorChanged(DependencyObject sender, DependencyProperty dp)
        {
            UpdateLinkColor();
        }

        private Color HyperlinkColor => HyperlinkForeground is SolidColorBrush brush ? brush.Color : Colors.Blue;

        // What the link ranges were last coloured with: the colour is re-applied whenever it
        // moves, and this is what says whether it did.
        private Color _linkColor;

        private void UpdateLinkColor()
        {
            var color = HyperlinkColor;

            if (color == _linkColor)
            {
                return;
            }

            _linkColor = color;

            if (_links == null || _links.Count == 0)
            {
                return;
            }

            ApplyLinkColors();

            _renderedSize = default;
            InvalidateArrange();
        }

        /// <summary>
        /// The colour a custom emoji drawn from a monochrome sticker takes. Nothing else reads
        /// it: code takes the colour of the text around it, which the other engine needs a
        /// property for only because it draws code as hyperlinks.
        /// </summary>
        public Brush IconForeground
        {
            get => (Brush)GetValue(IconForegroundProperty);
            set => SetValue(IconForegroundProperty, value);
        }

        public static readonly DependencyProperty IconForegroundProperty =
            DependencyProperty.Register("IconForeground", typeof(Brush), typeof(DirectTextBlock), new PropertyMetadata(null, OnIconForegroundChanged));

        private static void OnIconForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as DirectTextBlock;

            for (int i = 0; i < sender._emoji?.Count; i++)
            {
                // The player watches the brush it is given for its colour changing in place,
                // so this only has to hand it over.
                sender._emoji[i].ReplacementColor = e.NewValue as Brush;
            }
        }

        public int MaxLines
        {
            get => (int)GetValue(MaxLinesProperty);
            set => SetValue(MaxLinesProperty, value);
        }

        /// <summary>
        /// Whether the text is longer than the lines it is collapsed to - which is not the same
        /// as being trimmed right now: an expanded quote is under no limit and still answers
        /// yes, or nothing would offer to collapse it again. Known only once the text has been
        /// laid out at a width, so it changes at measure.
        /// </summary>
        public bool IsTextTrimmable { get; private set; }
        public event EventHandler IsTextTrimmableChanged;

        /// <summary>
        /// Where the lines sit in the block. The default leaves it to the direction of the
        /// text, which is what <see cref="TextAlignment.DetectFromContent"/> means.
        /// </summary>
        public TextAlignment TextAlignment
        {
            get => (TextAlignment)GetValue(TextAlignmentProperty);
            set => SetValue(TextAlignmentProperty, value);
        }

        public static readonly DependencyProperty TextAlignmentProperty =
            DependencyProperty.Register("TextAlignment", typeof(TextAlignment), typeof(DirectTextBlock), new PropertyMetadata(TextAlignment.DetectFromContent, OnTextAlignmentChanged));

        private static void OnTextAlignmentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as DirectTextBlock;

            sender.Layout().Alignment = (TextAlignment)e.NewValue switch
            {
                TextAlignment.Left => TextAlignmentMode.Left,
                TextAlignment.Center => TextAlignmentMode.Center,
                TextAlignment.Right => TextAlignmentMode.Right,
                _ => TextAlignmentMode.Leading
            };

            sender.Invalidate();
        }

        public static readonly DependencyProperty MaxLinesProperty =
            DependencyProperty.Register("MaxLines", typeof(int), typeof(DirectTextBlock), new PropertyMetadata(0, OnMaxLinesChanged));

        private static void OnMaxLinesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as DirectTextBlock;
            var value = (int)e.NewValue;

            // Remembered while the limit is off: an expanded quote still has to say whether it
            // is longer than the three lines it collapses to, or the chevron that expanded it
            // goes away and it can never be collapsed again.
            if (value > 0)
            {
                sender._trimLines = value;
            }

            sender.Layout().MaxLines = value;
            sender.Invalidate();
        }

        private int _trimLines;

        /// <summary>
        /// Which way the text runs, for the one paragraph a plain text is: a styled one carries
        /// a direction per paragraph and says so through <see cref="SetParagraphs"/>, which is
        /// where anything with paragraphs of its own goes.
        /// </summary>
        private void SetDirection(TextDirectionality direction)
        {
            // Neutral - a text with no strong character in it, or one that never said - reads
            // the way the app does.
            var rtl = direction switch
            {
                TextDirectionality.RightToLeft => true,
                TextDirectionality.LeftToRight => false,
                _ => LocaleService.Current.FlowDirection == FlowDirection.RightToLeft
            };

            if (Layout().RightToLeft != rtl)
            {
                Layout().RightToLeft = rtl;
                Invalidate();
            }

            _rightToLeft = rtl;
        }

        // Which side the block as a whole reads from, which the layout no longer answers: it
        // holds a paragraph each and they may disagree. The first one that has a direction
        // decides, as it is the one the text opens with.
        private bool _rightToLeft;

        // Created on demand and kept: it holds the text and everything said about it, and
        // rebuilds itself when any of that changes.
        private DirectTextLayout Layout()
        {
            if (_layout == null)
            {
                TextThroughput.LayoutsMade++;
                _layout = Direct2D.Current.CreateLayout();
            }

            return _layout;
        }

        public void SetText(string text, IList<TextStylePart> entities = null)
        {
            _text = text;
            _links = null;
            _entities = null;
            _styled = null;
            _offset = 0;
            _dates = null;
            _hasDates = false;
            _revealed = false;

            _queryOffset = -1;
            _queryLength = 0;

            UpdateToolTip(-1);

            UpdateInteraction();

            Layout().SetText(text ?? string.Empty, entities);
            Select(0, 0);

            // Nothing here says which way it runs, so it runs the way the app does - and it is
            // one paragraph until a styled overload divides it, which they do after this.
            SetDirection(TextDirectionality.Neutral);

            Invalidate();
        }

        /// <summary>
        /// A colour over a range of the text - the sender a chat preview is prefixed with.
        /// Set after <see cref="SetText"/>, which clears what was said about the old text.
        /// </summary>
        public void SetColor(int offset, int length, Color color)
        {
            Layout().SetColor(offset, length, color);

            // Drawn again, not measured again: a colour moves nothing.
            _renderedSize = default;
            InvalidateArrange();
        }

        public readonly record struct CustomEmojiRange(int Offset, int Length, long CustomEmojiId);

        public readonly record struct SpoilerRange(int Offset, int Length);

        // A square the size the animated ones are decoded at, and a baseline that sits it on
        // the text rather than hanging it from the top of the line.
        private const double EmojiSize = 20;
        private const double EmojiBaseline = 16;

        /// <summary>
        /// The custom emoji in the text: a box each for the layout to flow around, and a player
        /// each, positioned where the layout put the box. Set after <see cref="SetText"/>, and
        /// after <see cref="SetSpoilers"/> - an emoji under a spoiler plays nothing.
        /// </summary>
        public void SetCustomEmoji(IClientService clientService, IList<CustomEmojiRange> ranges)
        {
            _ranges = ranges;

            for (int i = 0; i < ranges?.Count; i++)
            {
                Layout().SetInlineObject(ranges[i].Offset, ranges[i].Length, new Size(EmojiSize, EmojiSize), EmojiBaseline);

                var player = GetOrCreateEmoji(i);

                // The box stays under a spoiler, so the text does not move when it is
                // revealed, but nothing plays in it.
                player.Source = IsSpoilered(ranges[i].Offset)
                    ? null
                    : new CustomEmojiFileSource(clientService, ranges[i].CustomEmojiId);

                // Shown again: Clear collapses them, and this may be a recycled element.
                if (player.Visibility != Visibility.Visible)
                {
                    player.Visibility = Visibility.Visible;
                }
            }

            // The players of whatever this element rendered before: out of the way rather than
            // removed, as the next text may want as many again.
            for (int i = ranges?.Count ?? 0; i < _emoji?.Count; i++)
            {
                _emoji[i].Source = null;
                _emoji[i].Visibility = Visibility.Collapsed;
            }

            if (ranges?.Count > 0)
            {
                RegisterViewportChanged();
                UpdateViewport();
            }
            else
            {
                UnregisterViewportChanged();
            }

            Invalidate();
        }

        private CustomEmojiIcon GetOrCreateEmoji(int index)
        {
            _emoji ??= new List<CustomEmojiIcon>();

            if (index < _emoji.Count)
            {
                return _emoji[index];
            }

            var player = new CustomEmojiIcon
            {
                LoopCount = 0,
                IsHitTestVisible = false,
                IsViewportAware = false,
                ReplacementColor = IconForeground
            };

            _emoji.Add(player);
            Children.Add(player);

            return player;
        }

        // Whether the players are on screen: an emoji nobody is looking at should not decode
        // frames. One subscription for the block rather than one per player - every box is
        // inside it, so its viewport answers for all of them - and the subscription itself is
        // the base class's, in C++, because the framework allocates an event args and its
        // wrapper on every raise and a scrolling list raises it constantly.
        private double _viewportLeft;
        private double _viewportTop;
        private double _viewportRight;
        private double _viewportBottom;

        protected override void OnViewportChanged(double left, double top, double right, double bottom)
        {
            _viewportLeft = left;
            _viewportTop = top;
            _viewportRight = right;
            _viewportBottom = bottom;

            UpdateViewport();
        }

        // Never from a layout pass: a player told it became visible builds its presentation,
        // and that would invalidate the pass running it.
        private void UpdateViewport()
        {
            for (int i = 0; i < _ranges?.Count; i++)
            {
                var player = _emoji[i];

                // A player under a spoiler has no source and nothing to play, and one that has
                // not been arranged yet has no box: the viewport raise that follows the first
                // layout is what starts those.
                if (player.Source == null)
                {
                    continue;
                }

                player.ViewportChanged(
                    player.ActualOffset.X + player.ActualSize.X > _viewportLeft &&
                    player.ActualOffset.X < _viewportRight &&
                    player.ActualOffset.Y + player.ActualSize.Y > _viewportTop &&
                    player.ActualOffset.Y < _viewportBottom);
            }
        }

        public readonly record struct MathRange(int Offset, int Length, string Expression);

        /// <summary>
        /// The formulas in the text. Unlike every other box the text flows around, a formula
        /// is drawn by the layout itself, into the same surface as the text and in the same
        /// colour - there is no element to size, arrange or recycle, and nothing to keep in
        /// step with the line it sits on.
        ///
        /// One that does not parse leaves no box, and what was written stays as text - which is
        /// what the inline path falls back to as well. Set after <see cref="SetText"/>, which
        /// is what drops the ones before it.
        /// </summary>
        public void SetMath(IList<MathRange> ranges)
        {
            for (int i = 0; i < ranges?.Count; i++)
            {
                Layout().SetMathObject(ranges[i].Offset, ranges[i].Length, ranges[i].Expression);
            }

            Invalidate();
        }

        public readonly record struct InlineButtonRange(int Offset, int Length, InlineButton Button);

        private List<ReplyMarkupInlineButton> _buttons;

        // What each button last measured to, which is what says whether the layout still has
        // the right box for it.
        private List<Size> _buttonSizes;
        private IList<InlineButtonRange> _buttonRanges;

        // How far a button hangs below the line it sits on, as the inline one does through the
        // negative margin of its wrapper.
        private const double ButtonOverhang = 4;

        /// <summary>
        /// The buttons in the text: the variable sized inline object. Unlike an emoji, which
        /// is a square of a size we choose, a button is as wide as its label - so the box the
        /// layout flows the text around is whatever it measures to, which is settled in the
        /// measure pass and not here.
        /// </summary>
        public void SetInlineButtons(IClientService clientService, IList<InlineButtonRange> buttons)
        {
            _buttonRanges = buttons;

            for (int i = 0; i < buttons?.Count; i++)
            {
                var button = buttons[i].Button;
                var element = GetOrCreateButton(i);

                element.Tag = button;
                element.SetButton(clientService, null, 0, button.Style, button.Type, inline: true);

                if (button.Text is RichTextPlain plain)
                {
                    element.Content = plain.Text;
                }
                else
                {
                    // A block of its own for a button whose label is not plain text, on the
                    // same engine: it is text in a box, which is what this control is.
                    var block = element.Content as DirectTextBlock ?? new DirectTextBlock();

                    block.Foreground = element.Foreground;
                    block.SetText(clientService, TextStyleRun.GetText(button.Text));

                    element.Content = block;
                }

                // Not measured here: its size is worked out in the next measure pass, which
                // is where the layout can be told about it. The slot beside it holds what that
                // pass last found, so a size that has not moved does not rebuild the layout.
                _buttonSizes ??= new List<Size>();

                while (_buttonSizes.Count <= i)
                {
                    _buttonSizes.Add(default);
                }

                _buttonSizes[i] = default;
            }

            // The buttons of whatever this element rendered before: out of the way rather than
            // removed, as the next message may want as many again.
            for (int i = buttons?.Count ?? 0; i < _buttons?.Count; i++)
            {
                _buttons[i].Tag = null;
                _buttons[i].Content = null;
            }

            Invalidate();
        }

        private ReplyMarkupInlineButton GetOrCreateButton(int index)
        {
            _buttons ??= new List<ReplyMarkupInlineButton>();

            if (index < _buttons.Count)
            {
                return _buttons[index];
            }

            var element = new ReplyMarkupInlineButton();

            element.Click += OnInlineButtonClick;

            _buttons.Add(element);
            Children.Add(element);

            return element;
        }

        private void OnInlineButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is ReplyMarkupInlineButton { Tag: InlineButton button })
            {
                TextEntityClick?.Invoke(this, new TextEntityClickEventArgs(new TextEntityTypeButton(button)));
            }
        }

        /// <summary>
        /// The spoilers in the text. The text under one is drawn in nothing and the particles
        /// fill the box it left, which is how the other control does it too. Set after
        /// <see cref="SetText"/>.
        /// </summary>
        public void SetSpoilers(IList<SpoilerRange> ranges)
        {
            _spoilers = ranges;

            ApplySpoilerColors();
            UpdateInteraction();

            if (ranges?.Count > 0)
            {
                if (_spoilerPresenter == null)
                {
                    var color = Foreground is SolidColorBrush solid ? solid.Color : Colors.Black;

                    _spoilerPresenter = new AnimatedImage
                    {
                        IsViewportAware = true,
                        IsHitTestVisible = false,
                        FrameSize = new Size(0, 0),
                        ResizeMode = AnimatedImageResizeMode.Fill,
                        DecodeFrameType = DecodePixelType.Logical,
                        Stretch = Stretch.UniformToFill,
                        Source = new ParticlesImageSource(color)
                    };

                    Children.Add(_spoilerPresenter);
                }
                else
                {
                    // Collapsed by Clear, and this may be a recycled element.
                    _spoilerPresenter.Visibility = Visibility.Visible;
                }
            }

            Invalidate();
        }

        // The text under a spoiler is drawn in nothing. Apart from SetSpoilers, as anything
        // that colours a range over one has to put it back.
        private void ApplySpoilerColors()
        {
            for (int i = 0; i < _spoilers?.Count; i++)
            {
                Layout().SetColor(_spoilers[i].Offset, _spoilers[i].Length, Colors.Transparent);
            }
        }

        // The box every spoiler in the text covers, in one rectangle.
        private Rect SpoilerBounds()
        {
            var minX = double.MaxValue;
            var minY = double.MaxValue;
            var maxX = double.MinValue;
            var maxY = double.MinValue;

            for (int i = 0; i < _spoilers?.Count; i++)
            {
                var rects = Layout().Ranges(_spoilers[i].Offset, _spoilers[i].Length);

                for (int j = 0; j < rects?.Length; j++)
                {
                    minX = Math.Min(minX, rects[j].Left);
                    minY = Math.Min(minY, rects[j].Top);
                    maxX = Math.Max(maxX, rects[j].Right);
                    maxY = Math.Max(maxY, rects[j].Bottom);
                }
            }

            return minX > maxX || minY > maxY
                ? default
                : new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        /// <summary>
        /// Uncovers every spoiler in the text, which is what clicking one does: the colour comes
        /// off the ranges, the emoji under them start playing, and the particles go.
        /// </summary>
        public void RevealSpoilers()
        {
            if (_spoilers == null || _spoilers.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _spoilers.Count; i++)
            {
                Layout().ClearColor(_spoilers[i].Offset, _spoilers[i].Length);
            }

            var revealed = _spoilers;

            _spoilers = null;
            _revealed = true;

            for (int i = 0; i < _ranges?.Count; i++)
            {
                if (_emoji[i].Source == null && _clientService != null && IsWithin(revealed, _ranges[i].Offset))
                {
                    _emoji[i].Source = new CustomEmojiFileSource(_clientService, _ranges[i].CustomEmojiId);
                }
            }

            UpdateViewport();

            // The text is drawn again with the ranges uncovered, and the particles are arranged
            // into nothing now that there are no spoilers left to cover.
            _renderedSize = default;
            UpdateToolTip(-1);

            InvalidateArrange();
        }

        // Whether the spoilers of this text have been uncovered, so that rendering it again -
        // which a date ticking does - does not put them back.
        private bool _revealed;

        private static bool IsWithin(IList<SpoilerRange> spoilers, int offset)
        {
            for (int i = 0; i < spoilers?.Count; i++)
            {
                if (offset >= spoilers[i].Offset && offset < spoilers[i].Offset + spoilers[i].Length)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsSpoilered(int offset)
        {
            for (int i = 0; i < _spoilers?.Count; i++)
            {
                if (offset >= _spoilers[i].Offset && offset < _spoilers[i].Offset + _spoilers[i].Length)
                {
                    return true;
                }
            }

            return false;
        }

        // The particles over every spoiler at once: one presenter clipped to the boxes, rather
        // than one per range. Arranging only, as this runs inside the arrange pass.
        private void UpdateSpoilers()
        {
            if (_spoilerPresenter == null)
            {
                return;
            }

            if (_spoilers == null || _spoilers.Count == 0 || _spoilerBounds.Width < 1)
            {
                _spoilerPresenter.Arrange(new Rect(0, 0, 0, 0));
                return;
            }

            using var builder = new CanvasPathBuilder(null);

            for (int i = 0; i < _spoilers.Count; i++)
            {
                var rects = Layout().Ranges(_spoilers[i].Offset, _spoilers[i].Length);

                for (int j = 0; j < rects?.Length; j++)
                {
                    var rect = rects[j];
                    builder.AddGeometry(CanvasGeometry.CreateRectangle(null, new Rect(rect.X - _spoilerBounds.X, rect.Y - _spoilerBounds.Y, rect.Width, rect.Height)));
                }
            }

            _spoilerPresenter.Arrange(FromLayout(_spoilerBounds));

            var visual = ElementCompositionPreview.GetElementVisual(_spoilerPresenter);
            visual.Clip = visual.Compositor.CreateGeometricClip(visual.Compositor.CreatePathGeometry(new CompositionPath(CanvasGeometry.CreatePath(builder))));
        }


        public readonly record struct TextRange(int Offset, int Length);

        private IList<TextRange> _links;
        private PointerCursorType _cursor;

        /// <summary>
        /// The ranges that are links: what the pointer becomes over them and what a click
        /// reports. They take <see cref="HyperlinkForeground"/>. Set after <see cref="SetText"/>.
        /// </summary>
        public void SetLinks(IList<TextRange> links)
        {
            _links = links;
            _linkColor = HyperlinkColor;

            ApplyLinkColors();
            UpdateInteraction();
        }

        private void ApplyLinkColors()
        {
            for (int i = 0; i < _links?.Count; i++)
            {
                if (IsColoredLink(i))
                {
                    Layout().SetColor(_links[i].Offset, _links[i].Length, _linkColor);
                }
            }
        }

        // Takes a position rather than a point: hit testing costs a call into the layout, and
        // a pointer move asks for the link, the cursor and the tooltip at the same place.
        private int LinkAt(int position)
        {
            for (int i = 0; i < _links?.Count; i++)
            {
                if (position >= _links[i].Offset && position < _links[i].Offset + _links[i].Length)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool _interactive;
        private bool _tappable;
        private bool _pressable;

        // The one place that decides what the pointer can do here. It was three, and they
        // disagreed: whether the block is hit-testable and whether it listens for the pointer
        // are separate questions, and links, a drag of our own and the manager's selection
        // answer them differently.
        private void UpdateInteraction()
        {
            var links = _links?.Count > 0;
            var spoilers = _spoilers?.Count > 0;

            // The buttons count too: hit testing is off for a whole subtree, so a block that
            // wants no pointer of its own would swallow the clicks meant for them.
            var pointer = links || spoilers || _buttonRanges?.Count > 0 || _selectable || _selectionEnabled;

            // Hit-testable for anything that wants the pointer, the manager included: it walks
            // up from whatever was hit to find a control that takes part in a selection.
            //
            // And painted, which is the half that is not obvious: an element with no brush is
            // not hit tested at all, whatever IsHitTestVisible says, and the pointer goes
            // straight through to what is behind it. The host covers the whole block, so a
            // point on it is a point on the text.
            IsHitTestVisible = pointer;

            if (pointer && _host.Background == null)
            {
                _host.Background = new SolidColorBrush(Colors.Transparent);
            }

            // The cursor is this control's business whatever drives the selection, so the
            // move and exit handlers go on as soon as anything wants the pointer.
            //
            // Handlers rather than overrides, as Panel has none of the pointer ones - those
            // are on Control - and never detached: they name this element from a child of it,
            // which keeps nothing else alive.
            if (pointer && !_interactive)
            {
                _interactive = true;

                _host.PointerMoved += OnPointerMoved;
                _host.PointerExited += OnPointerExited;
            }

            // A link and a spoiler are acted on when tapped, not when the pointer is released:
            // the selection manager captures the pointer on its root as soon as one goes down,
            // so every pointer event after that is routed there and never reaches this block.
            // A tap survives it, and it is also the right gesture - a drag that selects text
            // is not a click on what it started over.
            if ((links || spoilers) && !_tappable)
            {
                _tappable = true;
                _host.Tapped += OnTapped;
            }

            // Press and release are only for a drag of this control's own, which no caller in
            // the app asks for yet.
            if (_selectable && !_pressable)
            {
                _pressable = true;

                _host.PointerPressed += OnPointerPressed;
                _host.PointerReleased += OnPointerReleased;
            }
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(this).Position;

            if (_selecting)
            {
                Select(_selectionStart, Layout().HitTest(ToLayout(point), out _));
                e.Handled = true;
                return;
            }

            // On the text, not merely nearest it: the space past the end of a line is not the
            // link that line ends with.
            var position = Layout().HitTestContent(ToLayout(point), out var inside);
            var link = inside ? LinkAt(position) : -1;

            UpdateToolTip(link, point);

            // The hand over what a click acts on - a link, or a spoiler waiting to be
            // revealed - and the beam over text that can be selected, by this control or by
            // the manager. The beam covers the whole block, not only the glyphs: the empty end
            // of a short line selects like the rest of it.
            var cursor = link >= 0 || (inside && IsSpoilered(position))
                ? PointerCursorType.Hand
                : _selectable || _selectionEnabled
                ? PointerCursorType.IBeam
                : PointerCursorType.Arrow;

            // Written only when it changes: the cursor is a window wide setting, and setting
            // it per pointer sample would fight every other control.
            if (cursor != _cursor)
            {
                _cursor = cursor;
                WindowContext.SetPointerCursor(cursor);
            }
        }

        // The link a masked one really goes to. There is no element to hang it on - a link is a
        // range - so one ToolTip is attached to the host while the pointer is over a link and
        // taken off again when it leaves.
        //
        // Attached, because IsOpen on a ToolTip that belongs to nothing throws. And opened here
        // rather than left to the framework, which asks whether an element has a tooltip when
        // the pointer ENTERS it and never asks again: at that moment this block has none, and
        // the pointer goes from one link to the next without ever leaving it.
        private ToolTip _toolTip;
        private DispatcherTimer _toolTipTimer;
        private int _toolTipLink = -1;

        // The framework's own numbers, from ToolTipService: the wait is the system hover time
        // doubled for a mouse, or one and a half times it when another tooltip was showing
        // within the last 200ms, and what is shown goes away again after the system's message
        // duration. Read here rather than left to ToolTipService, which cannot open this one.
        private const ulong BetweenShowDelay = 200;
        private static ulong _lastToolTipOpened;

        private static TimeSpan ToolTipDelay()
        {
            var hover = BootStrapper.Current.UISettings.MouseHoverTime;
            var reshow = Logger.TickCount - _lastToolTipOpened < BetweenShowDelay;

            return TimeSpan.FromMilliseconds(reshow ? hover * 3 / 2 : hover * 2);
        }

        private void UpdateToolTip(int link)
        {
            UpdateToolTip(link, default);
        }

        private void UpdateToolTip(int link, Point point)
        {
            if (link == _toolTipLink)
            {
                return;
            }

            _toolTipLink = link;

            // A masked link says where it really goes; a date says which one it is, since what
            // it shows is how long ago rather than when.
            var content = link >= 0 && link < _entities?.Count
                ? _entities[link].Type switch
                {
                    TextEntityTypeTextUrl textUrl => textUrl.Url,
                    TextEntityTypeDateTime dateTime => Formatter.LongDateAt(dateTime.UnixTime),
                    _ => null
                }
                : null;

            if (content == null)
            {
                HideToolTip();
                return;
            }

            if (_toolTip == null)
            {
                // Against the line of the link rather than at the pointer: PlacementMode.Mouse
                // is the framework's own hover flow, which knows where the pointer is - one
                // opened by hand has no such position and lands against the whole host. Top is
                // what ToolTip itself falls back to when Mouse makes no sense.
                _toolTip = new ToolTip { Placement = PlacementMode.Top };

                _toolTipTimer = new DispatcherTimer();
                _toolTipTimer.Tick += OnToolTipTick;
            }

            // Closed rather than moved: where a tooltip sits is settled when it opens, so the
            // one belonging to the link before this hangs over the wrong words otherwise. This
            // is also what the framework does between two elements, down to showing the next
            // one after the shorter of the two waits.
            _toolTip.IsOpen = false;

            _toolTip.Content = content;
            _toolTip.PlacementRect = LinkRect(link, point);

            ToolTipService.SetToolTip(_host, _toolTip);

            _toolTipTimer.Stop();
            _toolTipTimer.Interval = ToolTipDelay();
            _toolTipTimer.Start();
        }

        // The line of the link the pointer is on: a link that wraps has a rectangle per line,
        // and the tooltip belongs under the one being read.
        private Rect? LinkRect(int link, Point point)
        {
            var rects = GetLinkRectangles(link);

            for (int i = 0; i < rects?.Length; i++)
            {
                if (rects[i].Contains(point))
                {
                    return rects[i];
                }
            }

            return rects?.Length > 0 ? rects[0] : null;
        }

        private void OnToolTipTick(object sender, object e)
        {
            _toolTipTimer.Stop();

            // Open, so this is the end of the time it is shown for. Left attached: the pointer
            // is still over the link, and an automatic tooltip does not come back either until
            // it is asked for again.
            if (_toolTip.IsOpen)
            {
                _toolTip.IsOpen = false;
                return;
            }

            if (_toolTipLink >= 0)
            {
                _toolTip.IsOpen = true;
                _lastToolTipOpened = Logger.TickCount;

                _toolTipTimer.Interval = TimeSpan.FromSeconds(BootStrapper.Current.UISettings.MessageDuration);
                _toolTipTimer.Start();
            }
        }

        private void HideToolTip()
        {
            if (_toolTip == null)
            {
                return;
            }

            _toolTipTimer.Stop();
            _toolTip.IsOpen = false;

            // Off the host as well: what is left attached is what the framework would open by
            // itself the next time the pointer enters, wherever it enters.
            ToolTipService.SetToolTip(_host, null);
        }

        private void OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            UpdateToolTip(-1);

            if (_cursor != PointerCursorType.Arrow)
            {
                _cursor = PointerCursorType.Arrow;
                WindowContext.SetPointerCursor(PointerCursorType.Arrow);
            }
        }

        private void OnTapped(object sender, TappedRoutedEventArgs e)
        {
            var point = e.GetPosition(this);
            var position = Layout().HitTestContent(ToLayout(point), out var inside);

            if (!inside)
            {
                return;
            }

            // A spoiler takes the tap before a link does: what is under it cannot be read yet,
            // so it cannot be what was tapped.
            if (IsSpoilered(position))
            {
                RevealSpoilers();
                e.Handled = true;

                return;
            }

            var link = LinkAt(position);

            if (link >= 0)
            {
                InvokeLink(link);

                e.Handled = true;
            }
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);

            if (_selectable && point.Properties.IsLeftButtonPressed)
            {
                _selecting = true;
                _selectionStart = Layout().HitTest(ToLayout(point.Position), out _);

                Select(_selectionStart, _selectionStart);
                CapturePointer(e.Pointer);

                e.Handled = true;
            }
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_selecting)
            {
                _selecting = false;
                ReleasePointerCapture(e.Pointer);
            }
        }

        private bool _selectable;
        private bool _selecting;
        private int _selectionStart;
        private int _selectionFrom;
        private int _selectionTo;

        private ShapeVisual _selectionVisual;
        private CompositionSpriteShape _selectionShape;
        private CompositionPathGeometry _selectionGeometry;

        /// <summary>
        /// Whether a pointer can select text in this block on its own. Off by default: a chat
        /// list cell is a click target, not a document, and a message is selected across every
        /// block at once by <see cref="Telegram.Common.TextSelectionManager"/>.
        /// </summary>
        public bool IsTextSelectionEnabled
        {
            get => _selectable;
            set
            {
                _selectable = value;
                UpdateInteraction();
            }
        }

        public string SelectedText
        {
            get
            {
                var from = Math.Min(_selectionFrom, _selectionTo);
                var to = Math.Max(_selectionFrom, _selectionTo);

                return to > from && _text != null && to <= _text.Length
                    ? _text.Substring(from, to - from)
                    : string.Empty;
            }
        }

        /// <summary>
        /// The X where the last line of text ends, for a caller placing something beside it -
        /// the message footer.
        /// </summary>
        public float ContentEnd()
        {
            return (float)(Layout().ContentEnd().X + _contentLeft);
        }

        public void ClearSelection()
        {
            Select(0, 0);
        }

        // The selection is drawn under the glyphs as shapes, not into the surface: a drag
        // changes it every pointer sample, and rasterizing the text again for each of them
        // would be the one expensive thing in this control.
        public void Select(int from, int to)
        {
            if (from == _selectionFrom && to == _selectionTo)
            {
                return;
            }

            _selectionFrom = from;
            _selectionTo = to;

            UpdateSelection();
        }

        // Apart from Select, as the shapes it draws into are built on the first render and the
        // text moves under them on every one: both have to be able to draw what is selected
        // without a caller selecting it again.
        private void UpdateSelection()
        {
            var start = Math.Min(_selectionFrom, _selectionTo);
            var length = Math.Abs(_selectionTo - _selectionFrom);

            // Hidden rather than emptied: the Path of a CompositionPathGeometry cannot be set
            // back to null, and doing it takes the process with it.
            if (length <= 0)
            {
                if (_selectionVisual != null)
                {
                    _selectionVisual.IsVisible = false;
                }

                return;
            }

            // Built here rather than with the text: this is the first time this block is known
            // to need it, and most never do.
            EnsureSelectionVisual();

            if (_selectionShape == null)
            {
                return;
            }

            var rects = Layout().Ranges(start, length);

            using var builder = new CanvasPathBuilder(null);

            for (int i = 0; i < rects?.Length; i++)
            {
                builder.AddGeometry(CanvasGeometry.CreateRectangle(null, rects[i]));
            }

            _selectionGeometry.Path = new CompositionPath(CanvasGeometry.CreatePath(builder));
            _selectionVisual.IsVisible = true;
        }

        #region Code

        // A token of the tree the tokenizer answers with, flattened to the range it covers.
        // Kept, rather than only applied: the colours are the theme's, and tokenizing again to
        // pick up a new one would be a round trip for something already known.
        private readonly record struct TokenRange(int Offset, int Length, string Type, string Alias);

        private List<TokenRange> _tokens;
        private int _tokenized;
        private bool _themeChanged;

        /// <summary>
        /// Colours the text as code in <paramref name="language"/>, which is asynchronous - the
        /// tokenizer runs off the UI thread. Called after the text is set, whose colours it adds
        /// to; a null language only forgets the tokens, as setting the text took the colours of
        /// whatever this block rendered before with it.
        /// </summary>
        public async void SetCode(string language)
        {
            // Whatever is in flight was started for a text this block no longer renders.
            var tokenized = ++_tokenized;

            if (_tokens != null)
            {
                _tokens = null;
                UnregisterThemeChanged();
            }

            if (string.IsNullOrEmpty(language) || string.IsNullOrEmpty(_text))
            {
                return;
            }

            try
            {
                var root = await SyntaxToken.TokenizeAsync(language.ToLowerInvariant(), _text);

                if (_tokenized != tokenized)
                {
                    return;
                }

                var tokens = new List<TokenRange>();
                CollectTokens(root.Children, tokens, 0);

                _tokens = tokens;
                ApplyTokens();

                if (!_themeChanged)
                {
                    _themeChanged = true;
                    ActualThemeChanged += OnActualThemeChanged;
                }
            }
            catch
            {
                // Tokenization may fail
            }
        }

        // Parent before children, which is the order the colours have to be applied in: a token
        // inside another one is drawn in its own colour, and the last one written over a range
        // is the one that shows. The length is only known once the children have been walked,
        // so the parent takes its place in the list first and is filled in after.
        private static int CollectTokens(IList<Token> tokens, List<TokenRange> ranges, int offset)
        {
            for (int i = 0; i < tokens?.Count; i++)
            {
                if (tokens[i] is SyntaxToken syntax)
                {
                    var index = ranges.Count;
                    var start = offset;

                    ranges.Add(default);
                    offset = CollectTokens(syntax.Children, ranges, offset);

                    ranges[index] = new TokenRange(start, offset - start, syntax.Type, syntax.Alias);
                }
                else if (tokens[i] is TextToken text)
                {
                    offset += text.Value.Length;
                }
            }

            return offset;
        }

        private void ApplyTokens()
        {
            var theme = ActualTheme;

            for (int i = 0; i < _tokens?.Count; i++)
            {
                var token = _tokens[i];

                if (SyntaxPalette.TryGetColor(token.Type, theme, out var color)
                    || (token.Alias.Length > 0 && SyntaxPalette.TryGetColor(token.Alias, theme, out color)))
                {
                    Layout().SetColor(token.Offset, token.Length, color);
                }
            }

            _renderedSize = default;
            InvalidateArrange();
        }

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            ApplyTokens();
        }

        private void UnregisterThemeChanged()
        {
            if (_themeChanged)
            {
                _themeChanged = false;
                ActualThemeChanged -= OnActualThemeChanged;
            }
        }

        #endregion

        #region Query

        private string _query;

        private int _queryOffset = -1;
        private int _queryLength;

        private ShapeVisual _queryVisual;
        private CompositionPathGeometry _queryGeometry;

        /// <summary>
        /// The search term to mark in the text. The first occurrence only, which is what the
        /// inline path marks too, in the same colours.
        /// </summary>
        public void SetQuery(string query, bool force = false)
        {
            if (!force && (_query ?? string.Empty) == (query ?? string.Empty))
            {
                return;
            }

            _query = query;
            UpdateQuery();
        }

        private void UpdateQuery()
        {
            var offset = _query?.Length > 0 && _text != null
                ? _text.IndexOf(_query, StringComparison.OrdinalIgnoreCase)
                : -1;

            if (offset == _queryOffset && (offset < 0 || _query.Length == _queryLength))
            {
                return;
            }

            // What was under the last match goes back to the colour the text says it should
            // have: a link, a piece of code or a spoiler may have been under it, and each of
            // those is a range this one was written over.
            if (_queryOffset >= 0)
            {
                Layout().ClearColor(_queryOffset, _queryLength);

                ApplySpoilerColors();
                ApplyLinkColors();
                ApplyTokens();
            }

            _queryOffset = offset;
            _queryLength = offset >= 0 ? _query.Length : 0;

            if (offset >= 0)
            {
                Layout().SetColor(offset, _queryLength, Colors.White);
            }

            _renderedSize = default;
            InvalidateArrange();
        }

        // The shape under the match, drawn like the selection is and for the same reason: the
        // text is rasterized, and colouring a rectangle behind it costs nothing to move.
        private void UpdateQueryHighlight()
        {
            if (_queryOffset < 0 || _queryLength <= 0)
            {
                if (_queryVisual != null)
                {
                    _queryVisual.IsVisible = false;
                }

                return;
            }

            EnsureQueryVisual();

            if (_queryGeometry == null)
            {
                return;
            }

            var rects = Layout().Ranges(_queryOffset, _queryLength);

            using var builder = new CanvasPathBuilder(null);

            for (int i = 0; i < rects?.Length; i++)
            {
                builder.AddGeometry(CanvasGeometry.CreateRectangle(null, rects[i]));
            }

            _queryGeometry.Path = new CompositionPath(CanvasGeometry.CreatePath(builder));
            _queryVisual.IsVisible = true;
        }

        #endregion

        #region Skeleton

        private ContainerVisual _skeleton;

        // The size the block was last arranged at, for a skeleton asked for between two layout
        // passes: ActualSize is the one before this arrange until this arrange is over.
        private Size _arranged;

        /// <summary>
        /// A shimmer in the shape of the text, for a message waiting on its translation.
        /// </summary>
        public void ShowHideSkeleton(bool show)
        {
            if (show == (_skeleton != null))
            {
                return;
            }

            if (show)
            {
                var compositor = BootStrapper.Current.Compositor;

                var ease = compositor.CreateLinearEasingFunction();
                var animation = compositor.CreateVector3KeyFrameAnimation();
                animation.InsertKeyFrame(0, new Vector3(-1, 0, 0), ease);
                animation.InsertKeyFrame(1, new Vector3(0, 0, 0), ease);
                animation.IterationBehavior = AnimationIterationBehavior.Forever;
                animation.Duration = TimeSpan.FromSeconds(1);

                // The fallback is the one the other engine falls back to, and it matters: a
                // colour that did not resolve is transparent, and a transparent shimmer is
                // nothing at all.
                var color = Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF);

                var lookup = ThemeService.GetLookup(ActualTheme);
                lookup.TryGetColor("SystemControlDisabledChromeDisabledLowBrush", out color);

                var gradient = compositor.CreateLinearGradientBrush();
                gradient.ColorStops.Add(compositor.CreateColorGradientStop(0, Color.FromArgb(0x00, color.R, color.G, color.B)));
                gradient.ColorStops.Add(compositor.CreateColorGradientStop(0.67f, Color.FromArgb(0x67, color.R, color.G, color.B)));
                gradient.ColorStops.Add(compositor.CreateColorGradientStop(1, Color.FromArgb(0x00, color.R, color.G, color.B)));
                gradient.StartPoint = new Vector2(0, 0);
                gradient.EndPoint = new Vector2(0.5f, 0);
                gradient.ExtendMode = CompositionGradientExtendMode.Wrap;

                // Twice the width and swept across: the wrap is what makes one gradient look
                // like a light travelling over the whole of it.
                var foreground = compositor.CreateSpriteVisual();
                foreground.RelativeSizeAdjustment = new Vector2(2, 1);
                foreground.Brush = gradient;
                foreground.StartAnimation("RelativeOffsetAdjustment", animation);

                _skeleton = compositor.CreateContainerVisual();
                _skeleton.Children.InsertAtTop(foreground);

                // On this element rather than on the host: the host carries the text, and a
                // child visual is drawn over everything the element holds.
                ElementCompositionPreview.SetElementChildVisual(this, _skeleton);

                UpdateSkeleton(_arranged);
            }
            else
            {
                _skeleton = null;
                ElementCompositionPreview.SetElementChildVisual(this, null);
            }
        }

        // The shimmer is clipped to the shape of the text, so it covers the lines and not the
        // box around them. Rebuilt with the layout, as the lines move with it.
        private void UpdateSkeleton(Size size)
        {
            if (_skeleton == null)
            {
                return;
            }

            // The gradient inside is sized from its parent - twice as wide, so that sweeping it
            // across covers the whole - and a container left at nothing makes it nothing.
            _skeleton.Size = new Vector2((float)size.Width, (float)size.Height);

            var lines = Layout().Lines(0, ContentLength);

            // Every shape end to end, with the count of each beside it: one list rather than a
            // list per shape, each of which would have to be wrapped to cross the ABI.
            var rects = new List<Rect>();
            var shapes = new List<int>();
            var count = 0;
            var last = default(Rect);

            for (int i = 0; i < lines?.Length; i++)
            {
                if (lines[i].Width < 1 || lines[i].Height < 1)
                {
                    continue;
                }

                // Through the offset like every other rectangle that leaves the layout: the
                // shimmer hangs off this element rather than off the container the text is
                // drawn in, so nothing else moves it.
                var line = FromLayout(lines[i]);
                var rect = new Rect(line.X - 2, line.Y, line.Width + 4, line.Height);

                if (count > 0 && !rect.IntersectsOrTouches(last))
                {
                    shapes.Add(count);
                    count = 0;
                }

                rects.Add(rect);
                last = rect;
                count++;
            }

            if (count > 0)
            {
                shapes.Add(count);
            }

            _skeleton.Clip = rects.Count > 0
                ? BootStrapper.Current.Compositor.CreateGeometricClip(BootStrapper.Current.Compositor.CreatePathGeometry(Direct2D.Current.GetRoundedPolygon(rects.ToArray(), shapes.ToArray())))
                : null;
        }

        #endregion

        #region ISelectableControl

        private StyledText _styled;

        // Where this block's text starts in the styled text it is a slice of. Zero for the
        // block that renders all of it, which is most of them.
        private int _offset;

        /// <summary>
        /// A rich text, as an instant view page carries it. Styled first, like everything else
        /// this renders.
        /// </summary>
        public void SetText(IClientService clientService, RichText text)
        {
            SetText(clientService, TextStyleRun.GetText(text));
        }

        /// <summary>
        /// The styled text this block renders, kept so that a selection can be handed back as
        /// text and entities rather than as a substring.
        /// </summary>
        public void SetText(StyledText styled)
        {
            // After, not before: the overload below clears what the last styled text said.
            SetText(styled?.Text, styled?.Parts);
            _styled = styled;
        }

        /// <summary>
        /// Everything a styled text says, in one call: the text and its styles, the spoilers to
        /// hide, the emoji and buttons to leave a box for, and the ranges that are links. What
        /// a caller would otherwise walk the entities for itself.
        /// </summary>
        public void SetText(IClientService clientService, StyledText styled)
        {
            SetText(clientService, styled, 0, (styled?.Paragraphs.Count ?? 1) - 1);
        }

        /// <summary>
        /// The same, for the paragraphs <paramref name="first"/> to <paramref name="last"/>
        /// only: a message with a quote or a code block in it is a block per one of those and a
        /// block per run of ordinary paragraphs, all reading from the one styled text.
        ///
        /// Positions here are this slice's own, and <see cref="GetSourceOffset"/> is what puts
        /// them back where they came from.
        /// </summary>
        public void SetText(IClientService clientService, StyledText styled, int first, int last)
        {
            var started = TextThroughput.Begin();
            SetTextCore(clientService, styled, first, last);
            TextThroughput.Record(ref TextThroughput.DirectSetText, started, true);
        }

        private void SetTextCore(IClientService clientService, StyledText styled, int first, int last)
        {
            _clientService = clientService;

            UnsubscribeDates();

            if (styled == null || styled.Paragraphs.Count == 0)
            {
                // Cast: the two argument overloads are a text with its styles and a styled text
                // with a client service, and two nulls fit both.
                SetText((string)null);

                SetSpoilers(null);
                SetCustomEmoji(clientService, null);
                SetInlineButtons(clientService, null);
                SetMath(null);
                SetLinks(null);

                return;
            }

            _first = first;
            _last = last;

            CollectDates(styled, first, last);
            ApplyText(styled, first, last);

            if (IsConnected)
            {
                SubscribeDates();
            }
        }

        // What SetText does every time, and a date tick does again with the same text: the
        // subscriptions are the same runs either way, so they stay where they are.
        private void ApplyText(StyledText styled, int first, int last)
        {
            var offset = styled.Paragraphs[first].Offset;
            var length = styled.Paragraphs[last].Offset + styled.Paragraphs[last].Length - offset;

            if (!_hasDates)
            {
                // The source text, not the paragraphs joined: whatever separates them is part
                // of what this slice renders, and every offset below is measured in it.
                SetText(length == styled.Text.Length ? styled.Text : styled.Text.Substring(offset, length), GetParts(styled, offset, length));
            }
            else
            {
                BuildText(styled, first, last, offset, length);
            }

            _styled = styled;
            _offset = offset;

            SetParagraphs(styled, first, last, offset);

            List<SpoilerRange> spoilers = null;
            List<CustomEmojiRange> emoji = null;
            List<InlineButtonRange> buttons = null;
            List<MathRange> math = null;
            List<TextRange> links = null;

            // Paragraph offsets are relative to the paragraph, and this layout holds a slice of
            // the whole text, so everything moves by where its paragraph starts and back by
            // where the slice does.
            for (int i = first; i <= last; i++)
            {
                var paragraph = styled.Paragraphs[i];

                for (int j = 0; j < paragraph.Entities.Count; j++)
                {
                    var entity = paragraph.Entities[j];

                    // Both ends through the map, so an entity that contains a date is as long
                    // as what is drawn for it rather than as long as it was written.
                    var start = SourceToRendered(paragraph.Offset + entity.Offset - offset);
                    var count = SourceToRendered(paragraph.Offset + entity.Offset + entity.Length - offset) - start;

                    if (entity.Type is TextEntityTypeSpoiler)
                    {
                        spoilers ??= new List<SpoilerRange>();
                        spoilers.Add(new SpoilerRange(start, count));
                    }
                    else if (entity.Type is TextEntityTypeCustomEmoji customEmoji)
                    {
                        emoji ??= new List<CustomEmojiRange>();
                        emoji.Add(new CustomEmojiRange(start, count, customEmoji.CustomEmojiId));
                    }
                    else if (entity.Type is TextEntityTypeButton inlineButton)
                    {
                        buttons ??= new List<InlineButtonRange>();
                        buttons.Add(new InlineButtonRange(start, count, inlineButton.Button));
                    }
                    else if (entity.Type is TextEntityTypeMathematicalExpression expression)
                    {
                        math ??= new List<MathRange>();
                        math.Add(new MathRange(start, count, expression.Expression));
                    }
                    else if (IsLink(entity.Type))
                    {
                        links ??= new List<TextRange>();
                        links.Add(new TextRange(start, count));

                        _entities ??= new List<TextEntity>();
                        _entities.Add(new TextEntity(start, count, entity.Type));
                    }
                }
            }

            // Spoilers first: an emoji under one plays nothing.
            SetSpoilers(spoilers);

            var started = TextThroughput.Begin();
            SetCustomEmoji(_clientService, emoji);
            TextThroughput.Record(ref TextThroughput.DirectEmoji, started);

            started = TextThroughput.Begin();
            SetInlineButtons(_clientService, buttons);
            TextThroughput.Record(ref TextThroughput.DirectButtons, started);

            started = TextThroughput.Begin();
            SetMath(math);
            TextThroughput.Record(ref TextThroughput.DirectMath, started);

            SetLinks(links);

            // Last: it is written over whatever the ranges above coloured.
            UpdateQuery();
        }

        /// <summary>
        /// The paragraphs of the slice, each with the direction it reads in: the layout draws
        /// one per paragraph and stacks them, so paragraphs that disagree - a message that
        /// mixes scripts - are laid out together rather than split across blocks.
        /// </summary>
        private void SetParagraphs(StyledText styled, int first, int last, int offset)
        {
            var alignment = TextAlignment switch
            {
                TextAlignment.Left => TextAlignmentMode.Left,
                TextAlignment.Center => TextAlignmentMode.Center,
                TextAlignment.Right => TextAlignmentMode.Right,
                _ => TextAlignmentMode.Leading
            };

            var paragraphs = new List<TextParagraph>(last - first + 1);
            var direction = false;
            var directed = false;

            for (int i = first; i <= last; i++)
            {
                var paragraph = styled.Paragraphs[i];

                // A neutral paragraph - digits, an emoji, punctuation - reads whichever way the
                // app does, as one with no strong character of its own has nothing to say.
                var rtl = paragraph.Direction switch
                {
                    TextDirectionality.RightToLeft => true,
                    TextDirectionality.LeftToRight => false,
                    _ => LocaleService.Current.FlowDirection == FlowDirection.RightToLeft
                };

                if (!directed && paragraph.Direction != TextDirectionality.Neutral)
                {
                    directed = true;
                    direction = rtl;
                }

                paragraphs.Add(new TextParagraph
                {
                    Offset = SourceToRendered(paragraph.Offset - offset),
                    Length = SourceToRendered(paragraph.Offset + paragraph.Length - offset) - SourceToRendered(paragraph.Offset - offset),
                    RightToLeft = rtl,
                    Alignment = alignment
                });
            }

            _rightToLeft = directed
                ? direction
                : LocaleService.Current.FlowDirection == FlowDirection.RightToLeft;

            Layout().SetParagraphs(paragraphs);
            Invalidate();
        }

        // The styles over a slice, clipped to it and moved into its space. The whole text is
        // the common case and hands its own list straight over.
        private static IList<TextStylePart> GetParts(StyledText styled, int offset, int length)
        {
            if (offset == 0 && length == styled.Text.Length)
            {
                return styled.Parts;
            }

            List<TextStylePart> parts = null;

            for (int i = 0; i < styled.Parts.Count; i++)
            {
                var part = styled.Parts[i];

                var from = Math.Max(part.Offset, offset);
                var to = Math.Min(part.Offset + part.Length, offset + length);

                if (to > from)
                {
                    (parts ??= new List<TextStylePart>()).Add(new TextStylePart
                    {
                        Offset = from - offset,
                        Length = to - from,
                        Type = part.Type
                    });
                }
            }

            return parts ?? TextStyleRun.NoParts;
        }

        #region Dates

        // A date is drawn as the text it formats to, and the styled text holds the text it was
        // written as. Where the two differ, so does every offset after it - which is why a
        // slice with no date in it, the overwhelming majority, skips all of this.
        private readonly record struct DateRange(int Source, int SourceLength, int Rendered, int RenderedLength);

        // A relative date the service has to tick, and the key it was subscribed with. A key of
        // its own rather than the run: the same styled text can be rendered by two blocks at
        // once - a message and the pinned header above it - and the run would collide.
        private readonly record struct DateSubscription(object Token, StyledParagraph Paragraph, TextStyleRun Run, TextEntityTypeDateTime Type);

        private List<DateRange> _dates;
        private List<DateSubscription> _dateRuns;
        private bool _hasDates;

        private int _first;
        private int _last;

        private void CollectDates(StyledText styled, int first, int last)
        {
            _hasDates = false;
            _dateRuns = null;

            for (int i = first; i <= last; i++)
            {
                var runs = styled.Paragraphs[i].Runs;

                for (int j = 0; j < runs.Count; j++)
                {
                    if (runs[j].Type is TextEntityTypeDateTime { FormattingType: not null } date)
                    {
                        // An absolute one is formatted once and stays as it is, so it changes
                        // the text without ever needing to be told the time again.
                        _hasDates = true;

                        if (date.FormattingType is DateTimeFormattingTypeRelative)
                        {
                            (_dateRuns ??= new List<DateSubscription>()).Add(
                                new DateSubscription(new object(), styled.Paragraphs[i], runs[j], date));
                        }
                    }
                }
            }
        }

        // The text as it is drawn: each paragraph asked for its own, which is where a date is
        // substituted, and whatever separates them taken from the source.
        private void BuildText(StyledText styled, int first, int last, int offset, int length)
        {
            var builder = new StringBuilder(length);
            var dates = new List<DateRange>();

            List<TextStylePart> parts = null;

            for (int i = first; i <= last; i++)
            {
                var paragraph = styled.Paragraphs[i];

                if (i > first)
                {
                    var previous = styled.Paragraphs[i - 1];
                    var between = previous.Offset + previous.Length;

                    builder.Append(styled.Text, between, paragraph.Offset - between);
                }

                var start = builder.Length;
                var shift = 0;

                // Formatted before the text is asked for: GetParts substitutes whatever each
                // run is holding, and one that was never updated holds an empty string.
                for (int j = 0; j < paragraph.Runs.Count; j++)
                {
                    var run = paragraph.Runs[j];

                    if (run.Type is not TextEntityTypeDateTime { FormattingType: not null })
                    {
                        continue;
                    }

                    var formatted = run.Update(paragraph);

                    dates.Add(new DateRange(
                        paragraph.Offset + run.Offset - offset,
                        run.Length,
                        start + run.Offset + shift,
                        formatted.Length));

                    shift += formatted.Length - run.Length;
                }

                var rendered = paragraph.GetParts(out var text);
                builder.Append(text);

                for (int j = 0; j < rendered.Count; j++)
                {
                    var part = rendered[j];

                    (parts ??= new List<TextStylePart>()).Add(new TextStylePart
                    {
                        Offset = start + part.Offset,
                        Length = part.Length,
                        Type = part.Type
                    });
                }
            }

            SetText(builder.ToString(), parts ?? TextStyleRun.NoParts);

            // After: setting the text is what drops everything said about the last one, and
            // this text is the one being set.
            _dates = dates;
            _hasDates = true;
        }

        private int SourceToRendered(int offset)
        {
            for (int i = 0; i < _dates?.Count; i++)
            {
                var date = _dates[i];

                if (offset <= date.Source)
                {
                    return offset + Shift(i);
                }
                else if (offset < date.Source + date.SourceLength)
                {
                    // Inside a date, which has no inside: the whole of it stands for the whole
                    // of what it was written as.
                    return date.Rendered;
                }
            }

            return offset + Shift(_dates?.Count ?? 0);
        }

        private int RenderedToSource(int offset)
        {
            for (int i = 0; i < _dates?.Count; i++)
            {
                var date = _dates[i];

                if (offset <= date.Rendered)
                {
                    return offset - Shift(i);
                }
                else if (offset < date.Rendered + date.RenderedLength)
                {
                    return date.Source;
                }
            }

            return offset - Shift(_dates?.Count ?? 0);
        }

        // What the dates before `count` add up to.
        private int Shift(int count)
        {
            var shift = 0;

            for (int i = 0; i < count; i++)
            {
                shift += _dates[i].RenderedLength - _dates[i].SourceLength;
            }

            return shift;
        }

        private void SubscribeDates()
        {
            if (_dateRuns == null || XamlRoot == null || _dateSubscribed)
            {
                return;
            }

            _dateSubscribed = true;

            for (int i = 0; i < _dateRuns.Count; i++)
            {
                var date = _dateRuns[i];

                // No segment: the block rebuilds its text from the paragraph rather than
                // patching the run it drew, so there is nothing to shift.
                RelativeDateService.Subscribe(date.Token, this, date.Paragraph, date.Run, date.Type, 0);
            }
        }

        private void UnsubscribeDates()
        {
            if (_dateRuns == null || !_dateSubscribed)
            {
                return;
            }

            _dateSubscribed = false;

            if (XamlRoot != null)
            {
                for (int i = 0; i < _dateRuns.Count; i++)
                {
                    RelativeDateService.Unsubscribe(_dateRuns[i].Token, XamlRoot);
                }
            }
        }

        private bool _dateSubscribed;

        void IRelativeDateHost.UpdateDate(object element, StyledParagraph paragraph, TextStyleRun run, int segment, string text, int delta)
        {
            if (_styled == null)
            {
                return;
            }

            // The whole text again rather than the run alone: the layout holds one string, and
            // a date that grew moves everything after it. Only what is drawn is rebuilt - the
            // subscriptions are these same runs, and the service is iterating them right now.
            var revealed = _revealed;

            ApplyText(_styled, _first, _last);

            if (revealed)
            {
                RevealSpoilers();
            }
        }

        #endregion

        private static bool IsLink(TextEntityType type)
        {
            return type is TextEntityTypeUrl
                or TextEntityTypeTextUrl
                or TextEntityTypeEmailAddress
                or TextEntityTypePhoneNumber
                or TextEntityTypeMention
                or TextEntityTypeMentionName
                or TextEntityTypeHashtag
                or TextEntityTypeCashtag
                or TextEntityTypeBotCommand
                or TextEntityTypeBankCardNumber
                // A date is one too, and for the same reason the inline path draws it as a
                // hyperlink: TextStyleRun gives it the Url style. A click opens what it means.
                or TextEntityTypeDateTime
                // And inline code, which is clickable without being a link: it keeps the
                // colour of the text around it, and only the monospace style says what it is.
                or TextEntityTypeCode;
        }

        // Whether a link range takes the hyperlink colour. Inline code does not: it is clicked
        // like a link but read as text.
        private bool IsColoredLink(int index)
        {
            return _entities == null
                || index >= _entities.Count
                || _entities[index].Type is not TextEntityTypeCode;
        }

        // The entities behind the link ranges, so a click can be reported as the entity it is
        // rather than as an index into a list the caller no longer has.
        private List<TextEntity> _entities;

        public event EventHandler<TextEntityClickEventArgs> TextEntityClick;

        /// <summary>
        /// Reports the link at <paramref name="index"/> as clicked - what a pointer does, and
        /// what automation does when it invokes one.
        /// </summary>
        public void InvokeLink(int index)
        {
            if (_entities != null && index >= 0 && index < _entities.Count && _text != null)
            {
                var entity = _entities[index];
                var text = _text.Substring(entity.Offset, entity.Length);

                var args = new TextEntityClickEventArgs(entity.Type, text);
                args.Handled = false;

                TextEntityClick?.Invoke(this, args);

                // What the inline path does with one nobody handled: copying it is the only
                // thing there is to do with a piece of code, and the caller does not have to
                // know that.
                if (!args.Handled && entity.Type is TextEntityTypeCode or TextEntityTypePre or TextEntityTypePreCode)
                {
                    MessageHelper.CopyText(XamlRoot, text);
                }
            }
        }

        /// <summary>
        /// The link at a point, as a click would report it, or null where there is none. What a
        /// context menu needs: the inline path reads it off the Hyperlink element, and here
        /// there is no element to read - a link is a range.
        /// </summary>
        public TextEntityClickEventArgs GetEntityFromPoint(Point point)
        {
            var position = Layout().HitTestContent(ToLayout(point), out var inside);
            var link = inside ? LinkAt(position) : -1;

            if (link < 0 || _entities == null || link >= _entities.Count || _text == null)
            {
                return null;
            }

            var entity = _entities[link];
            return new TextEntityClickEventArgs(entity.Type, _text.Substring(entity.Offset, entity.Length));
        }

        // What the automation tree needs to know about the links: how many there are, what each
        // says, and where each one is. A link is a range, so it has none of its own.
        public int LinkCount => _links?.Count ?? 0;

        public string GetLinkText(int index)
        {
            if (_links == null || index < 0 || index >= _links.Count || _text == null)
            {
                return string.Empty;
            }

            var link = _links[index];
            return _text.Substring(link.Offset, Math.Min(link.Length, _text.Length - link.Offset));
        }

        public Rect[] GetLinkRectangles(int index)
        {
            return _links == null || index < 0 || index >= _links.Count
                ? null
                : FromLayout(Layout().Ranges(_links[index].Offset, _links[index].Length));
        }

        /// <summary>
        /// Whether this block takes part in the selection the bubble drives across its blocks.
        /// Separate from <see cref="IsTextSelectionEnabled"/>, which is this control dragging a
        /// selection of its own.
        /// </summary>
        public bool IsSelectionEnabled
        {
            get => _selectionEnabled;
            set
            {
                _selectionEnabled = value;
                UpdateInteraction();
            }
        }

        private bool _selectionEnabled;

        public int ContentLength => _text?.Length ?? 0;

        /// <summary>
        /// The text as rendered, for the automation peer and anything else that reads rather
        /// than draws it.
        /// </summary>
        public string Text => _text;

        public void GetSelection(out int start, out int end)
        {
            start = Math.Min(_selectionFrom, _selectionTo);
            end = Math.Max(_selectionFrom, _selectionTo);
        }

        // The layout's own answers, for the peer: which rectangles a range covers, and how many
        // characters each line holds.
        public Rect[] GetRangeRectangles(int start, int end) => FromLayout(Layout().Ranges(start, end - start));

        /// <summary>
        /// The rectangles covering [<paramref name="from"/>, <paramref name="to"/>) of the
        /// text, one per line, for a caller drawing a shape down them.
        /// </summary>
        public IList<Rect> GetHighlightRectangles(int from, int to)
        {
            // Asked in source offsets, like every block of the message is: what falls outside
            // this slice is another block's to draw, and what a date renders as is longer or
            // shorter than what it was written as.
            from = SourceToRendered(Math.Max(from - _offset, 0));
            to = Math.Min(SourceToRendered(to - _offset), ContentLength);

            return to > from
                ? FromLayout(Layout().Lines(from, to - from))
                : null;
        }

        public int[] GetLineLengths() => Layout().LineLengths();

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new DirectTextBlockAutomationPeer(this);
        }

        // A position is an offset into what this block renders, and the source is the styled
        // text it is a slice of: the slice it starts at, and the dates in it, are the whole of
        // the difference between the two.
        public int GetSourceOffset(int position) => _offset + RenderedToSource(position);

        public FormattedText GetSelectedText(int start, int end)
        {
            var from = GetSourceOffset(start);
            var to = GetSourceOffset(end);

            return to > from ? _styled?.Substring(from, to - from) : null;
        }

        public FormattedText GetSourceText(int from, int to) => _styled?.Substring(from, to - from);

        public int GetPositionFromPoint(Point point, out int hit)
        {
            hit = SelectionHit.None;

            // The nearest position, whether or not the point is on the text: a drag through
            // the space past the end of a line belongs to that line, and answering with the
            // start or the end of the whole text is what made it jump.
            var position = Layout().HitTest(ToLayout(point), out _);

            return position < 0
                ? 0
                : Math.Clamp(position, 0, ContentLength);
        }

        public void GetSelectionBoundary(int position, int hit, TextSelectionGranularity granularity, out int start, out int end)
        {
            start = end = Math.Clamp(position, 0, ContentLength);

            if (granularity == TextSelectionGranularity.Character || string.IsNullOrEmpty(_text))
            {
                return;
            }

            if (granularity == TextSelectionGranularity.Paragraph)
            {
                start = _text.LastIndexOf('\n', Math.Max(0, Math.Min(start - 1, _text.Length - 1)));
                start = start < 0 ? 0 : start + 1;

                end = end < _text.Length ? _text.IndexOf('\n', end) : -1;
                end = end < 0 ? _text.Length : end;

                return;
            }

            // A word: out from the position while there is one to expand into. A position
            // between two words expands to neither, which is what a double tap on a space does.
            while (start > 0 && !char.IsWhiteSpace(_text[start - 1]))
            {
                start--;
            }

            while (end < _text.Length && !char.IsWhiteSpace(_text[end]))
            {
                end++;
            }
        }

        #endregion

        private void Invalidate()
        {
            _renderedSize = default;
            InvalidateMeasure();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var started = TextThroughput.Begin();
            var size = MeasureCore(availableSize);
            TextThroughput.Record(ref TextThroughput.DirectMeasure, started, true);

            return size;
        }

        private Size MeasureCore(Size availableSize)
        {
            // The buttons first, and before the text: the layout has to flow it around a box
            // whose size is whatever the button measures to. Unbounded, because a button is as
            // wide as its label and the line it lands on does not decide that.
            var unbounded = new Size(double.PositiveInfinity, double.PositiveInfinity);

            for (int i = 0; i < _buttonRanges?.Count; i++)
            {
                var element = _buttons[i];
                element.Measure(unbounded);

                // Told to the layout only when it moves: an inline object rebuilds it.
                if (element.DesiredSize != _buttonSizes[i])
                {
                    _buttonSizes[i] = element.DesiredSize;

                    Layout().SetInlineObject(_buttonRanges[i].Offset, _buttonRanges[i].Length,
                        element.DesiredSize, element.DesiredSize.Height - ButtonOverhang);
                }
            }

            // The emoji are a box of a size we chose, so the layout already has it: they are
            // measured to it, and nothing about them can change what the text does.
            var emoji = new Size(EmojiSize, EmojiSize);

            for (int i = 0; i < _ranges?.Count; i++)
            {
                _emoji[i].Measure(emoji);
            }

            // A layout box has to be finite: an unconstrained measure is the natural width.
            var width = double.IsInfinity(availableSize.Width) ? 100000 : availableSize.Width;

            // The layout shrinks its own box to the text where that matters - a paragraph
            // that is not drawn from the leading edge - and leaves it alone where it does not,
            // which is most of the time and half the cost of a measure.
            var layout = TextThroughput.Begin();
            var size = Layout().Measure(width);
            TextThroughput.Record(ref TextThroughput.DirectLayout, layout);

            // Whether the text fits is settled by that measure, and a quote offering to expand
            // is waiting on the answer. Asked both ways round because only one of them holds in
            // either state: the layout knows it was cut while the limit is on, and the lines it
            // takes are there to be counted while it is off.
            var trimmable = Layout().IsTrimmed || (_trimLines > 1 && Layout().LineCount > _trimLines);

            if (IsTextTrimmable != trimmable)
            {
                IsTextTrimmable = trimmable;
                IsTextTrimmableChanged?.Invoke(this, EventArgs.Empty);
            }

            _host.Measure(size);

            // The spoilers cover boxes of the text, so their bounds are only known once it has
            // been laid out - and the presenter has to be measured here like everything else.
            _spoilerBounds = SpoilerBounds();
            _spoilerPresenter?.Measure(new Size(_spoilerBounds.Width, _spoilerBounds.Height));

            return size;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var started = TextThroughput.Begin();
            var size = ArrangeCore(finalSize);
            TextThroughput.Record(ref TextThroughput.DirectArrange, started, true);

            return size;
        }

        private Size ArrangeCore(Size finalSize)
        {
            _arranged = finalSize;

            var started = TextThroughput.Begin();
            Render(finalSize);
            TextThroughput.Record(ref TextThroughput.DirectRender, started);

            _host?.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));

            UpdateSpoilers();
            UpdateSkeleton(finalSize);

            // Each player where the layout put the box it flowed around. A box that was
            // trimmed away has no rectangle, and its player is arranged into nothing - not
            // collapsed, as changing that here would invalidate the pass doing it.
            for (int i = 0; i < _ranges?.Count; i++)
            {
                var rects = Layout().Ranges(_ranges[i].Offset, _ranges[i].Length);

                _emoji[i].Arrange(rects?.Length > 0
                    ? FromLayout(new Rect(rects[0].X, rects[0].Y, EmojiSize, EmojiSize))
                    : new Rect(0, 0, 0, 0));
            }

            // Same for the buttons, at the size each reported: the layout reserved exactly
            // that, so the box it left and the control put in it are the same width.
            for (int i = 0; i < _buttonRanges?.Count; i++)
            {
                var rects = Layout().Ranges(_buttonRanges[i].Offset, _buttonRanges[i].Length);
                var size = _buttons[i].DesiredSize;

                _buttons[i].Arrange(rects?.Length > 0
                    ? FromLayout(new Rect(rects[0].X, rects[0].Y, size.Width, size.Height))
                    : new Rect(0, 0, 0, 0));
            }

            return finalSize;
        }

        // The visuals the text itself needs. The selection and the search highlight are not
        // among them: most blocks are never selected and never searched, and a shape visual, a
        // sprite shape and a path geometry each are worth more than they look next to a draw.
        private void EnsureVisual()
        {
            if (_visual != null)
            {
                return;
            }

            // The compositor of the view this element is in - a CompositionObject cannot cross
            // views, and this one belongs to the window.
            var compositor = BootStrapper.Current.Compositor;

            _brush = compositor.CreateSurfaceBrush();
            _visual = compositor.CreateSpriteVisual();
            _visual.Brush = _brush;

            // An element takes a single child visual, and what is drawn under the text - a
            // search match, a selection - moves with it when the text does not start at the
            // block's edge, so they share a container.
            _root = compositor.CreateContainerVisual();
            _root.RelativeSizeAdjustment = Vector2.One;
            _root.Children.InsertAtTop(_visual);

            ElementCompositionPreview.SetElementChildVisual(_host, _root);
        }

        // Under the glyphs, and under the search match when there is one: inserted rather than
        // appended, because the order of the three is what decides what covers what.
        private void EnsureSelectionVisual()
        {
            if (_selectionVisual != null || _root == null)
            {
                return;
            }

            var compositor = BootStrapper.Current.Compositor;

            _selectionGeometry = compositor.CreatePathGeometry();
            _selectionShape = compositor.CreateSpriteShape(_selectionGeometry);

            // Through a source rather than a colour brush of its own: it keeps the brush in
            // step with the property, including the colour changing in place.
            _selectionSource = new CompositionColorSource(SelectionHighlightColor, IsConnected);
            _selectionShape.FillBrush = _selectionSource;

            _selectionVisual = compositor.CreateShapeVisual();
            _selectionVisual.RelativeSizeAdjustment = Vector2.One;
            _selectionVisual.Shapes.Add(_selectionShape);

            if (_queryVisual != null)
            {
                _root.Children.InsertAbove(_selectionVisual, _queryVisual);
            }
            else
            {
                _root.Children.InsertAtBottom(_selectionVisual);
            }
        }

        private void EnsureQueryVisual()
        {
            if (_queryVisual != null || _root == null)
            {
                return;
            }

            var compositor = BootStrapper.Current.Compositor;

            _queryGeometry = compositor.CreatePathGeometry();

            var queryShape = compositor.CreateSpriteShape(_queryGeometry);
            queryShape.FillBrush = compositor.CreateColorBrush(Colors.Orange);

            _queryVisual = compositor.CreateShapeVisual();
            _queryVisual.RelativeSizeAdjustment = Vector2.One;
            _queryVisual.Shapes.Add(queryShape);

            _root.Children.InsertAtBottom(_queryVisual);
        }

        private void Render(Size size)
        {
            if (size.Width < 1 || size.Height < 1)
            {
                return;
            }

            var scale = XamlRoot?.RasterizationScale ?? 1;
            var color = Foreground is SolidColorBrush solid ? solid.Color : Colors.Black;

            if (_surface != null && _renderedSize == size && _renderedScale == scale && _renderedColor == color)
            {
                return;
            }

            // The layout owns the surface: the same one comes back every time, and it redraws
            // itself when the rendering device is replaced, so this is only told about it when
            // it is a different object - which is the first render, and a resize that failed.
            var render = TextThroughput.Begin();
            var surface = Layout().Render(color, scale);
            TextThroughput.Record(ref TextThroughput.DirectSurface, render);

            if (surface == null)
            {
                return;
            }

            _renderedSize = size;
            _renderedScale = scale;
            _renderedColor = color;

            var previous = _surface;
            _surface = surface;

            EnsureVisual();

            if (previous != surface)
            {
                _brush.Surface = surface;

                // The layout gave the old one back rather than resizing it, and this is the
                // last reference to it: an atlas region waiting on a finalizer is a region
                // nobody else can have.
                previous?.Dispose();
            }

            // The size the surface actually got, back in DIPs, and not the size the element was
            // arranged at: the surface is a whole number of pixels and the arranged size is not,
            // so sizing the visual to the latter makes the brush resample the text by the
            // fraction between them - which blurs every glyph. The surface is rounded up past
            // the text, and that margin was cleared, so what hangs over the block is nothing.
            //
            // It sits at the origin, because that is where the layout box starts: the text is
            // drawn at the position the layout gives it, which is the position everything else
            // here reads.
            var pixels = surface.SizeInt32;
            _visual.Size = new Vector2((float)(pixels.Width / scale), (float)(pixels.Height / scale));

            // What the layout actually drew, which is what the text takes: the surface is
            // rounded up from it so that a recycled block redraws without resizing one.
            var content = Layout().RenderedPixels;

            // The room the block was given over what the text takes, handed to whichever side
            // the text reads from. Here, because this is where the size it is measured against
            // is known and where the visual it moves is sized.
            _contentLeft = ContentLeft(size.Width, content.Width / scale);
            _root.Offset = new Vector3((float)_contentLeft, 0, 0);

            // The text moved, so everything drawn around it has to follow.
            UpdateSelection();
            UpdateQueryHighlight();
        }

        // A point the caller gave, in the layout's coordinates; a rectangle the layout gave, in
        // the caller's. Both are the identity for the text that starts where the block does,
        // which is nearly all of it.
        private Point ToLayout(Point point)
        {
            return _contentLeft > 0
                ? new Point(point.X - _contentLeft, point.Y)
                : point;
        }

        private Rect FromLayout(Rect rect)
        {
            return _contentLeft > 0
                ? new Rect(rect.X + _contentLeft, rect.Y, rect.Width, rect.Height)
                : rect;
        }

        private Rect[] FromLayout(Rect[] rects)
        {
            for (int i = 0; i < rects?.Length && _contentLeft > 0; i++)
            {
                rects[i] = FromLayout(rects[i]);
            }

            return rects;
        }

        private double ContentLeft(double width, double content)
        {
            if (content <= 0 || width <= content)
            {
                return 0;
            }

            return TextAlignment switch
            {
                TextAlignment.Center => (width - content) / 2,
                TextAlignment.Right => width - content,
                TextAlignment.Left => 0,
                // What is left is the default, where the direction decides: the near edge is
                // the far one when the text reads the other way.
                _ => _rightToLeft ? width - content : 0
            };
        }

        /// <summary>
        /// Releases the layout, and with it the surface: both hold the text of whatever this
        /// element was last used for. For a block that is being put away rather than handed
        /// the next message - see <see cref="Recycle"/>, which is the other one.
        /// </summary>
        public void Clear()
        {
            Recycle();

            // Last, so that nothing above answers a question with a layout built to be thrown
            // away. Closed rather than dropped: the surface it owns is a region of the device's
            // atlas, and waiting for the wrapper to be collected holds it for as long as that
            // takes.
            if (_layout != null)
            {
                TextThroughput.LayoutsDisposed++;

                _layout.Dispose();
                _layout = null;
            }

            _surface = null;

            if (_brush != null)
            {
                _brush.Surface = null;
            }
        }

        /// <summary>
        /// Everything the last message left here, without the layout it was laid out with or
        /// the surface it was drawn into: a recycled bubble is handed those back, and building
        /// them again per message is a DirectWrite layout and a region of the device's atlas
        /// per message - measured, and the single largest thing recycling saves.
        /// </summary>
        public void Recycle()
        {
            // Collapsed rather than emptied: the layout still holds the last message, and the
            // surface still has it drawn, so a block left visible would show text the bubble no
            // longer has. Collapsed it is neither measured nor composed, and the next message
            // brings it back.
            Visibility = Visibility.Collapsed;

            _text = null;
            _links = null;
            _entities = null;
            _styled = null;
            _offset = 0;
            _revealed = false;

            _tokens = null;
            _tokenized++;
            UnregisterThemeChanged();

            UnsubscribeDates();
            ShowHideSkeleton(false);

            _dateRuns = null;
            _dates = null;
            _hasDates = false;

            Select(0, 0);
            UpdateToolTip(-1);

            _renderedSize = default;
            _ranges = null;
            _buttonRanges = null;
            _spoilers = null;

            _spoilerBounds = default;

            UnregisterViewportChanged();

            if (_spoilerPresenter != null)
            {
                _spoilerPresenter.Visibility = Visibility.Collapsed;
            }

            for (int i = 0; i < _emoji?.Count; i++)
            {
                _emoji[i].Source = null;
                _emoji[i].Visibility = Visibility.Collapsed;
            }

            for (int i = 0; i < _buttons?.Count; i++)
            {
                _buttons[i].Tag = null;
                _buttons[i].Content = null;
            }

            // The layout stays, holding the text it was given and the surface it drew into:
            // the next message replaces the first and draws into the second.
        }
    }
}
