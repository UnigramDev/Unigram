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
    // A strip - a TopNavView, a SelectorBar, anything - that travels with the content until it
    // reaches the collapsed StickyHeader, and then stays under it.
    //
    // It resolves its own scrolling host rather than asking the header for one, so the two only meet
    // over the separator: while the strip is pinned it is the bottom edge of a single surface, so it
    // draws the line and the header does not. This is the simple case only, for pages that want the
    // profile page's behaviour without the profile page's machinery (a nested frame, snap points, a
    // second scroller) - it is not a replacement for that.
    public partial class StickyHeaderTabs : ContentControl
    {
        // The strip fades its background in over the last stretch of its travel.
        private const float BackgroundFade = 16;

        // How far out the strip announces itself. Approach is the long ramp the handover happens
        // on - the header's surface reaches out on it and the card gives way on it - because a
        // handover that starts at contact leaves the surface's overshoot nowhere to spend itself
        // but after the strip, and it reads as a lunge. Progress is the short one, left for a strip
        // used without a header, which has only its own surface to fade in.
        private const float ApproachRange = 96;

        // It comes to rest 8 pixels inside the collapsed header rather than flush under it. Read by
        // StickyHeader, which grows its own surface by the rest of the strip's height.
        internal const float HeaderOverlap = 8;

        private Border BackgroundPart;
        private Border SeparatorPart;
        private Border CardPart;

        private ScrollViewer _scrollingHost;
        private FrameworkElement _contentRoot;

        private CompositionPropertySet _properties;
        private CompositionPropertySet _layout;

        private StickyHeader _header;
        private float _threshold = float.NaN;
        private bool _running;

        public StickyHeaderTabs()
        {
            DefaultStyleKey = typeof(StickyHeaderTabs);

            Canvas.SetZIndex(this, 1);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            // Watched for the whole life of the control, not just while attached: the first real
            // layout is what retries an attach that Loaded was too early for - a strip that was
            // collapsed then, or whose scrolling host had not been templated yet.
            SizeChanged += OnSizeChanged;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            Attach();
        }

        /// <summary>
        /// <c>Progress</c> - 0 while the strip travels with the content, 1 once it is pinned under
        /// the header - <c>Approach</c>, the same thing measured over a much longer run-up, and
        /// <c>Offset</c>, the translation that pins it. The handover runs on Approach: the card
        /// fades on it, and <see cref="StickyHeader"/> grows its surface on it.
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
                    _properties.InsertScalar("Approach", 0);
                    _properties.InsertScalar("Offset", 0);

                    _layout = compositor.CreatePropertySet();
                    _layout.InsertScalar("Threshold", 0);
                }

                return _properties;
            }
        }

        #region Header

        /// <summary>
        /// The header this strip sticks under. Optional: without one it pins to the top of the
        /// viewport and owns the separator outright.
        /// </summary>
        public StickyHeader Header
        {
            get => (StickyHeader)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register("Header", typeof(StickyHeader), typeof(StickyHeaderTabs), new PropertyMetadata(null, OnHeaderChanged));

        private static void OnHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var tabs = d as StickyHeaderTabs;

            (e.OldValue as StickyHeader)?.DetachTabs(tabs);

            tabs._header = e.NewValue as StickyHeader;
            tabs._header?.AttachTabs(tabs);

            tabs.UpdateThreshold();
        }

        #endregion

        #region ScrollingHost

        /// <inheritdoc cref="StickyHeader.ScrollingHost"/>
        public object ScrollingHost
        {
            get => GetValue(ScrollingHostProperty);
            set => SetValue(ScrollingHostProperty, value);
        }

        public static readonly DependencyProperty ScrollingHostProperty =
            DependencyProperty.Register("ScrollingHost", typeof(object), typeof(StickyHeaderTabs), new PropertyMetadata(null));

        #endregion

        protected override void OnApplyTemplate()
        {
            BackgroundPart = GetTemplateChild(nameof(BackgroundPart)) as Border;
            SeparatorPart = GetTemplateChild(nameof(SeparatorPart)) as Border;
            CardPart = GetTemplateChild(nameof(CardPart)) as Border;

            // Hidden here rather than in the template: XAML pushes UIElement.Opacity onto the
            // element visual, so a local value there would overwrite the expression that drives it.
            // CardPart is the resting state, so it is the one part left visible.
            if (BackgroundPart != null)
            {
                ElementComposition.GetElementVisual(BackgroundPart).Opacity = 0;
            }

            if (SeparatorPart != null)
            {
                ElementComposition.GetElementVisual(SeparatorPart).Opacity = 0;
            }

            base.OnApplyTemplate();
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

            StickyHeader.LiftItemsHost(this, contentRoot);

            _header?.AttachTabs(this);

            Start();
        }

        /// <summary>
        /// Starts the expressions, but not before the strip has been laid out: an unmeasured
        /// threshold is 0, which reads as already pinned, and the header would hand it the
        /// separator on the strength of it.
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

            _header?.DetachTabs(this);

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

        /// <inheritdoc cref="StickyHeader.UpdateThreshold"/>
        public void UpdateThreshold()
        {
            if (_scrollingHost == null || _layout == null)
            {
                return;
            }

            // Nothing to measure against until the strip has been arranged - it may be collapsed,
            // as GramsPage's is until the transactions arrive.
            if (ActualHeight == 0)
            {
                return;
            }

            // Measured against the scrolled content, never against the scrolling host: see the
            // remarks on StickyHeader.UpdateThreshold.
            var parent = VisualTreeHelper.GetParent(this) as UIElement;
            if (parent == null || _contentRoot == null)
            {
                return;
            }

            var point = parent.TransformToVisual(_contentRoot).TransformPoint(new Point());

            // Pinned under the collapsed header rather than at the top of the viewport, less the
            // overlap.
            var reserved = _header != null ? StickyHeader.CollapsedHeight - HeaderOverlap : 0;
            var threshold = (float)(point.Y + ActualOffset.Y) - reserved;

            if (_threshold != threshold)
            {
                _threshold = threshold;
                _layout.InsertScalar("Threshold", threshold);
            }
        }

        private void StartAnimations()
        {
            var properties = Properties;
            var compositor = properties.Compositor;

            var scroll = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(_scrollingHost);

            const string past = "(-scroll.Translation.Y - layout.Threshold)";

            var progress = compositor.CreateExpressionAnimation($"clamp({past} / {BackgroundFade}, 0, 1)");
            progress.SetReferenceParameter("scroll", scroll);
            progress.SetReferenceParameter("layout", _layout);

            // The same ramp reached much further out, so that whatever grows on it is already under
            // way when the strip arrives rather than setting off after it.
            var approach = compositor.CreateExpressionAnimation($"clamp(({past} + {ApproachRange}) / {ApproachRange}, 0, 1)");
            approach.SetReferenceParameter("scroll", scroll);
            approach.SetReferenceParameter("layout", _layout);

            var offset = compositor.CreateExpressionAnimation($"max({past}, 0)");
            offset.SetReferenceParameter("scroll", scroll);
            offset.SetReferenceParameter("layout", _layout);

            properties.StartAnimation("Progress", progress);
            properties.StartAnimation("Approach", approach);
            properties.StartAnimation("Offset", offset);

            ElementCompositionPreview.SetIsTranslationEnabled(this, true);

            var pin = compositor.CreateExpressionAnimation("tabs.Offset");
            pin.SetReferenceParameter("tabs", properties);

            ElementComposition.GetElementVisual(this).StartAnimation("Translation.Y", pin);

            // The card dissolves across the approach, on the same long run-up the header's surface
            // reaches out on, so the one gives way exactly as the other arrives. It is gone by the
            // moment the strip lands.
            var floating = compositor.CreateExpressionAnimation("1 - tabs.Approach");
            floating.SetReferenceParameter("tabs", properties);

            if (CardPart != null)
            {
                ElementComposition.GetElementVisual(CardPart).StartAnimation("Opacity", floating);
            }

            // With a header there is only one pinned surface and the header owns it - it grows down
            // over this strip. These two stay hidden, and exist for a strip used on its own.
            if (_header != null)
            {
                return;
            }

            var pinned = compositor.CreateExpressionAnimation("clamp(tabs.Progress * 2, 0, 1)");
            pinned.SetReferenceParameter("tabs", properties);

            if (BackgroundPart != null)
            {
                ElementComposition.GetElementVisual(BackgroundPart).StartAnimation("Opacity", pinned);
            }

            if (SeparatorPart != null)
            {
                ElementComposition.GetElementVisual(SeparatorPart).StartAnimation("Opacity", pinned);
            }
        }

        private void StopAnimations()
        {
            if (_properties == null)
            {
                return;
            }

            _properties.StopAnimation("Progress");
            _properties.StopAnimation("Approach");
            _properties.StopAnimation("Offset");
            _properties.InsertScalar("Progress", 0);
            _properties.InsertScalar("Approach", 0);
            _properties.InsertScalar("Offset", 0);

            ElementComposition.GetElementVisual(this).StopAnimation("Translation.Y");

            if (BackgroundPart != null)
            {
                ElementComposition.GetElementVisual(BackgroundPart).StopAnimation("Opacity");
            }

            if (SeparatorPart != null)
            {
                ElementComposition.GetElementVisual(SeparatorPart).StopAnimation("Opacity");
            }

            if (CardPart != null)
            {
                ElementComposition.GetElementVisual(CardPart).StopAnimation("Opacity");
            }
        }
    }
}
