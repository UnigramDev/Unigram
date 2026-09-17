//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    // The header MasterDetailView draws for a HostedPage, moved into the page and into its scrolled
    // content, so that a page can put content above it (SettingsNetworkPage's chart) and so that a
    // tab strip can stick under it.
    //
    // It publishes the collapse as two scalars - Progress and Offset - that everything else binds
    // its own visuals to: StickyHeaderTabs, and whatever elements stay outside the scrolled content
    // and follow the collapse without travelling with it (the back button, the action). That is the
    // whole contract: no control reaches into another's visual tree, and nothing here runs per frame
    // on the UI thread.
    public partial class StickyHeader : Control
    {
        // The distance over which the header collapses, and the height it settles at. Both are the
        // numbers MasterDetailView has always used, and the scale factors below are derived from
        // them, so a page that adopts this control looks the same as one that does not.
        public const float CollapseRange = 32;
        public const float CollapsedHeight = 48;

        // The background only starts to appear once the collapse is half done.
        private const float BackgroundFade = 16;

        private Border BackgroundPart;
        private Border SeparatorPart;
        private TextBlock TextPart;

        private ScrollViewer _scrollingHost;
        private FrameworkElement _contentRoot;

        private CompositionPropertySet _properties;
        private CompositionPropertySet _layout;

        private StickyHeaderTabs _tabs;
        private float _threshold = float.NaN;
        private bool _running;

        public StickyHeader()
        {
            DefaultStyleKey = typeof(StickyHeader);

            // Once pinned, the header is drawn over the content that scrolls beneath it. A page that
            // needs a different order sets Canvas.ZIndex itself: its XAML value is applied after this.
            Canvas.SetZIndex(this, 1);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            // Watched for the whole life of the control, not just while attached: the first real
            // layout is what retries an attach that Loaded was too early for - a header that was
            // collapsed then, or whose scrolling host had not been templated yet.
            SizeChanged += OnSizeChanged;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            Attach();
        }

        /// <summary>
        /// The state everything else binds to:
        /// <list type="bullet">
        /// <item><c>Progress</c> - 0 while the header is at rest, 1 once it has fully collapsed.</item>
        /// <item><c>Offset</c> - the Y translation that keeps the header pinned to the top.</item>
        /// </list>
        /// Both are 0 until the header finds its scrolling host, so this is safe to read at any
        /// point, in any order, including before the header has loaded.
        /// </summary>
        public CompositionPropertySet Properties
        {
            get
            {
                if (_properties == null)
                {
                    var compositor = ElementComposition.GetElementVisual(this).Compositor;

                    _properties = compositor.CreatePropertySet();
                    _properties.InsertScalar("Progress", 0);
                    _properties.InsertScalar("Offset", 0);

                    // Kept apart from the two above so that the expressions computing them can read
                    // Threshold without a property set appearing in an expression that targets it.
                    _layout = compositor.CreatePropertySet();
                    _layout.InsertScalar("Threshold", 0);
                    _layout.InsertScalar("Range", CollapseRange);
                }

                return _properties;
            }
        }

        #region Header

        /// <summary>
        /// Set on an element that stays <b>outside</b> the scrolled content - the back button, the
        /// action - to make it shrink and rise with the header without travelling with the content.
        /// Keeping those two out of the content is also what keeps the back gesture attached to an
        /// element that never moves.
        /// </summary>
        public static StickyHeader GetHeader(DependencyObject obj)
        {
            return (StickyHeader)obj.GetValue(HeaderProperty);
        }

        public static void SetHeader(DependencyObject obj, StickyHeader value)
        {
            obj.SetValue(HeaderProperty, value);
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.RegisterAttached("Header", typeof(StickyHeader), typeof(StickyHeader), new PropertyMetadata(null, OnHeaderChanged));

        private static void OnHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not UIElement element)
            {
                return;
            }

            // Unfollow first, and unconditionally: swapping one header for another would otherwise
            // leave whatever the old one animated that the new one does not.
            if (e.OldValue is StickyHeader old)
            {
                old.Unfollow(element);
            }

            // Nothing here needs the element to be in the tree, and the header publishes a resting
            // state until it finds its scrolling host, so there is no order to get right.
            if (e.NewValue is StickyHeader header)
            {
                header.Follow(element);
            }
        }

        /// <summary>
        /// Binds an element's own visual to the collapse. No bookkeeping: the animations live on
        /// that element's visual and go away with it, or with <see cref="Unfollow"/>.
        /// </summary>
        public void Follow(UIElement element)
        {
            var properties = Properties;
            var compositor = properties.Compositor;

            ElementCompositionPreview.SetIsTranslationEnabled(element, true);

            // 1 -> 0.8, the scale MasterDetailView shrinks its back button by.
            var scale = compositor.CreateExpressionAnimation("vector3(1 - header.Progress * 0.2, 1 - header.Progress * 0.2, 1)");
            scale.SetReferenceParameter("header", properties);

            // Lands centred in the collapsed bar, in place of MasterDetailView's flat -16. This
            // reads the element's own layout offset, so it assumes the element's parent starts
            // where the header pins - the top of the scrolling host - which is how a page lays out
            // chrome that sits over the content.
            var offset = compositor.CreateExpressionAnimation($"-header.Progress * (this.Target.Offset.Y + this.Target.Size.Y * 0.5 - {CollapsedHeight / 2})");
            offset.SetReferenceParameter("header", properties);

            // Anchored at its own centre vertically, so the landing point does not depend on the
            // scale; horizontally at the edge it is aligned to.
            var anchor = compositor.CreateExpressionAnimation("vector3(this.Target.Size.X * Anchor, this.Target.Size.Y * 0.5, 0)");
            anchor.SetScalarParameter("Anchor", element is FrameworkElement { HorizontalAlignment: HorizontalAlignment.Right } ? 1 : 0.5f);

            var visual = ElementComposition.GetElementVisual(element);
            visual.StartAnimation("CenterPoint", anchor);
            visual.StartAnimation("Translation.Y", offset);

            if (element is BackButton)
            {
                visual.StartAnimation("Scale", scale);
            }
        }

        public void Unfollow(UIElement element)
        {
            var visual = ElementComposition.GetElementVisual(element);
            visual.StopAnimation("CenterPoint");
            visual.StopAnimation("Scale");
            visual.StopAnimation("Translation.Y");
        }

        #endregion

        #region ScrollingHost

        /// <summary>
        /// The <see cref="ScrollViewer"/> or <see cref="ListViewBase"/> that drives the collapse.
        /// When left unset the header uses the nearest ScrollViewer above it, which is what a header
        /// sitting in the page's own scrolled content wants.
        /// </summary>
        public object ScrollingHost
        {
            get => GetValue(ScrollingHostProperty);
            set => SetValue(ScrollingHostProperty, value);
        }

        public static readonly DependencyProperty ScrollingHostProperty =
            DependencyProperty.Register("ScrollingHost", typeof(object), typeof(StickyHeader), new PropertyMetadata(null));

        #endregion

        #region Text

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(StickyHeader), new PropertyMetadata(null, OnTextChanged));

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StickyHeader header && header.TextPart != null)
            {
                header.TextPart.Text = e.NewValue as string ?? string.Empty;
            }
        }

        #endregion

        protected override void OnApplyTemplate()
        {
            BackgroundPart = GetTemplateChild(nameof(BackgroundPart)) as Border;
            SeparatorPart = GetTemplateChild(nameof(SeparatorPart)) as Border;
            TextPart = GetTemplateChild(nameof(TextPart)) as TextBlock;

            // Hidden here rather than in the template: XAML pushes UIElement.Opacity onto the
            // element visual, so a local value there would overwrite the expression that drives it.
            if (BackgroundPart != null)
            {
                ElementComposition.GetElementVisual(BackgroundPart).Opacity = 0;
            }

            if (SeparatorPart != null)
            {
                ElementComposition.GetElementVisual(SeparatorPart).Opacity = 0;
            }

            if (TextPart != null)
            {
                TextPart.Text = Text ?? string.Empty;
                UpdateTextCenterPoint();
            }

            base.OnApplyTemplate();
        }

        /// <summary>
        /// The title scales about its own vertical centre, which is what makes where it lands
        /// independent of the scale, and horizontally about its centre or its leading edge,
        /// following the alignment. Driven off the target's own size, so it needs no SizeChanged.
        /// </summary>
        private void UpdateTextCenterPoint()
        {
            var visual = ElementComposition.GetElementVisual(TextPart);

            var center = visual.Compositor.CreateExpressionAnimation("vector3(this.Target.Size.X * Anchor, this.Target.Size.Y * 0.5, 0)");
            center.SetScalarParameter("Anchor", HorizontalContentAlignment == HorizontalAlignment.Center ? 0.5f : 0);

            visual.StartAnimation("CenterPoint", center);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Attach();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Detach();
        }

        private void Attach()
        {
            if (_scrollingHost != null)
            {
                // Already attached, but the expressions may still be waiting for a layout.
                Start();
                return;
            }

            var scrollingHost = ScrollingHost switch
            {
                ScrollViewer scrollViewer => scrollViewer,
                ListViewBase listView => listView.GetScrollViewer(),
                _ => this.GetParent<ScrollViewer>()
            };

            if (scrollingHost == null)
            {
                // A ListViewBase only has a ScrollViewer once its template has been applied.
                if (ScrollingHost is ListViewBase listView)
                {
                    listView.Loaded -= OnScrollingHostLoaded;
                    listView.Loaded += OnScrollingHostLoaded;
                }

                return;
            }

            // The scrolled content is what the threshold is measured against, so there is nothing
            // to attach to without it; the next layout retries.
            if (scrollingHost.Content is not FrameworkElement contentRoot)
            {
                return;
            }

            _scrollingHost = scrollingHost;
            _contentRoot = contentRoot;

            _scrollingHost.SizeChanged += OnLayoutChanged;
            _contentRoot.SizeChanged += OnLayoutChanged;

            LiftItemsHost(this, contentRoot);

            Start();
        }

        /// <summary>
        /// Raises the header or footer host holding <paramref name="element"/> above the items.
        /// </summary>
        /// <remarks>
        /// An ItemsPresenter lays its children out as [header host, items panel, footer host], in
        /// that order and all at the same Z, so anything sticky in the header or the footer is
        /// painted under the items. Applied at every level up to the scrolled content, because a
        /// list's header can itself be a list - which is what StarsPage does.
        /// </remarks>
        internal static void LiftItemsHost(DependencyObject element, DependencyObject contentRoot)
        {
            var parent = VisualTreeHelper.GetParent(element);

            while (parent != null && parent != contentRoot)
            {
                if (parent is ContentControl host && VisualTreeHelper.GetParent(host) is ItemsPresenter)
                {
                    Canvas.SetZIndex(host, 1);
                }

                parent = VisualTreeHelper.GetParent(parent);
            }
        }

        /// <summary>
        /// Starts the expressions, but not before the header has been laid out: an unmeasured
        /// threshold is 0, which reads as already collapsed.
        /// </summary>
        private void Start()
        {
            UpdateThreshold();

            if (_running || float.IsNaN(_threshold))
            {
                return;
            }

            _running = true;
            StartAnimations();
        }

        private void Detach()
        {
            if (ScrollingHost is ListViewBase listView)
            {
                listView.Loaded -= OnScrollingHostLoaded;
            }

            if (_scrollingHost == null)
            {
                return;
            }

            _scrollingHost.SizeChanged -= OnLayoutChanged;

            if (_contentRoot != null)
            {
                _contentRoot.SizeChanged -= OnLayoutChanged;
                _contentRoot = null;
            }

            _scrollingHost = null;
            _threshold = float.NaN;
            _running = false;

            // The expressions reference the scroll viewer's manipulation property set: left running
            // they would outlive a page sitting in the back stack. Loaded rebuilds them.
            StopAnimations();
        }

        private void OnScrollingHostLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is ListViewBase listView)
            {
                listView.Loaded -= OnScrollingHostLoaded;
                Attach();
            }
        }

        private void OnLayoutChanged(object sender, SizeChangedEventArgs e)
        {
            // Start rather than UpdateThreshold: a resize of the content is also a chance to make
            // good on a start that had no layout to measure.
            Start();
        }

        /// <summary>
        /// Recomputes where in the scrolled content the header sits. The header watches its own
        /// size, the scrolling host's and the content root's, which covers anything that moves it;
        /// a page that changes the layout above it without changing the content's height calls this.
        /// </summary>
        public void UpdateThreshold()
        {
            if (_scrollingHost == null || _layout == null)
            {
                return;
            }

            // Nothing to measure against until the header has been arranged.
            if (ActualHeight == 0)
            {
                return;
            }

            // Measured against the scrolled content, never against the scrolling host: a transform
            // to the host is viewport-relative, and undoing that with VerticalOffset only holds
            // while the two agree - under a compositor-driven manipulation they do not, and the
            // result is nonsense. Against the content there is no scroll position in it at all.
            //
            // Measured through the parent and the arrange offset rather than through this element,
            // because once pinned the header carries a composition translation of its own.
            var parent = VisualTreeHelper.GetParent(this) as UIElement;
            if (parent == null || _contentRoot == null)
            {
                return;
            }

            var point = parent.TransformToVisual(_contentRoot).TransformPoint(new Point());
            var threshold = (float)(point.Y + ActualOffset.Y);

            if (_threshold != threshold)
            {
                _threshold = threshold;
                _layout.InsertScalar("Threshold", threshold);

                _tabs?.UpdateThreshold();
            }
        }

        private void StartAnimations()
        {
            var properties = Properties;
            var compositor = properties.Compositor;

            var scroll = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(_scrollingHost);

            // How far the content has scrolled past the point where the header reaches the top.
            const string past = "(-scroll.Translation.Y - layout.Threshold)";

            var progress = compositor.CreateExpressionAnimation($"clamp({past} / layout.Range, 0, 1)");
            progress.SetReferenceParameter("scroll", scroll);
            progress.SetReferenceParameter("layout", _layout);

            var offset = compositor.CreateExpressionAnimation($"max({past}, 0)");
            offset.SetReferenceParameter("scroll", scroll);
            offset.SetReferenceParameter("layout", _layout);

            properties.StartAnimation("Progress", progress);
            properties.StartAnimation("Offset", offset);

            ElementCompositionPreview.SetIsTranslationEnabled(this, true);

            var root = ElementComposition.GetElementVisual(this);
            var pin = compositor.CreateExpressionAnimation("header.Offset");
            pin.SetReferenceParameter("header", properties);

            root.StartAnimation("Translation.Y", pin);

            if (TextPart != null)
            {
                // 1 -> 0.583, the title settling into the collapsed header.
                var scale = compositor.CreateExpressionAnimation("vector3(1 - header.Progress * 0.417, 1 - header.Progress * 0.417, 1)");
                scale.SetReferenceParameter("header", properties);

                // Where it lands is aimed rather than incidental: the scale is anchored at the
                // title's own centre, so that centre is invariant, and this puts it on the middle
                // of the collapsed bar however deep in the header the title is laid out.
                var slide = compositor.CreateExpressionAnimation($"-header.Progress * (this.Target.Offset.Y + this.Target.Size.Y * 0.5 - {CollapsedHeight / 2})");
                slide.SetReferenceParameter("header", properties);

                ElementCompositionPreview.SetIsTranslationEnabled(TextPart, true);

                var text = ElementComposition.GetElementVisual(TextPart);
                text.StartAnimation("Scale", scale);
                text.StartAnimation("Translation.Y", slide);
            }

            var fade = compositor.CreateExpressionAnimation($"clamp((header.Progress * layout.Range - {BackgroundFade}) / {BackgroundFade}, 0, 1)");
            fade.SetReferenceParameter("header", properties);
            fade.SetReferenceParameter("layout", _layout);

            if (BackgroundPart != null)
            {
                ElementComposition.GetElementVisual(BackgroundPart).StartAnimation("Opacity", fade);
            }

            if (SeparatorPart != null)
            {
                ElementCompositionPreview.SetIsTranslationEnabled(SeparatorPart, true);
                ElementComposition.GetElementVisual(SeparatorPart).StartAnimation("Opacity", fade);
            }

            UpdateSurface();
        }

        private void StopAnimations()
        {
            if (_properties == null)
            {
                return;
            }

            _properties.StopAnimation("Progress");
            _properties.StopAnimation("Offset");
            _properties.InsertScalar("Progress", 0);
            _properties.InsertScalar("Offset", 0);

            ElementComposition.GetElementVisual(this).StopAnimation("Translation.Y");

            if (TextPart != null)
            {
                var text = ElementComposition.GetElementVisual(TextPart);
                text.StopAnimation("Scale");
                text.StopAnimation("Translation.Y");
            }

            if (BackgroundPart != null)
            {
                var background = ElementComposition.GetElementVisual(BackgroundPart);
                background.StopAnimation("Scale");
                background.StopAnimation("Opacity");
            }

            if (SeparatorPart != null)
            {
                var separator = ElementComposition.GetElementVisual(SeparatorPart);
                separator.StopAnimation("Translation.Y");
                separator.StopAnimation("Opacity");
            }
        }

        internal void AttachTabs(StickyHeaderTabs tabs)
        {
            _tabs = tabs;
            UpdateSurface();
        }

        internal void DetachTabs(StickyHeaderTabs tabs)
        {
            if (_tabs == tabs)
            {
                _tabs = null;
                UpdateSurface();
            }
        }

        /// <summary>
        /// Sizes the pinned surface. The header owns the only one: rather than letting a strip draw
        /// a second bar beneath it, the background grows to swallow the strip, because two surfaces
        /// meeting mid-transition show a step however well their fades are matched.
        /// </summary>
        private void UpdateSurface()
        {
            if (_scrollingHost == null)
            {
                return;
            }

            var properties = Properties;
            var compositor = properties.Compositor;

            // How far the surface reaches past its own height, as a fraction of where it comes to
            // rest. It does not simply grow into place: it sets out long before the strip arrives -
            // which is what Approach is for - reaches half again as far as it will end up, past the
            // strip's resting place and down towards where the strip actually is, and spends the
            // last third of the run coming back onto it. Starting at contact instead leaves the
            // overshoot nowhere to go but after the strip, and it reads as a lunge. This is the
            // shape ProfileHeader gives its own background through the branches of its
            // clipperTranslation.
            //
            // Written out rather than interpolated from constants: a float in an interpolated
            // string takes the current culture with it, and "1,5" is not an expression.
            const string reach = "(clamp(tabs.Approach / 0.65, 0, 1) * 1.5 - 0.5 * clamp((tabs.Approach - 0.65) / 0.35, 0, 1))";

            var growth = _tabs != null
                ? $"{reach} * max(strip.Size.Y - {StickyHeaderTabs.HeaderOverlap}, 0)"
                : "0";

            // Only Y: the background spans the width already, and scaling X would just push its
            // right edge off-screen the way MasterDetailView's does.
            var scale = compositor.CreateExpressionAnimation($"vector3(1, 1.357 - header.Progress * 0.357 + ({growth}) / {CollapsedHeight}, 1)");

            // The separator is drawn apart from the background so it can follow that bottom edge -
            // scaling the background would stretch the line itself.
            var follow = compositor.CreateExpressionAnimation($"{CollapsedHeight} * (0.357 - header.Progress * 0.357) + ({growth})");

            scale.SetReferenceParameter("header", properties);
            follow.SetReferenceParameter("header", properties);

            if (_tabs != null)
            {
                var strip = ElementComposition.GetElementVisual(_tabs);

                scale.SetReferenceParameter("tabs", _tabs.Properties);
                scale.SetReferenceParameter("strip", strip);

                follow.SetReferenceParameter("tabs", _tabs.Properties);
                follow.SetReferenceParameter("strip", strip);
            }

            if (BackgroundPart != null)
            {
                ElementComposition.GetElementVisual(BackgroundPart).StartAnimation("Scale", scale);
            }

            if (SeparatorPart != null)
            {
                ElementComposition.GetElementVisual(SeparatorPart).StartAnimation("Translation.Y", follow);
            }
        }
    }
}
