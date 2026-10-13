//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Numerics;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Controls
{
    /// <summary>
    /// A toggle block of a rich message: a header that opens and closes it, the blocks it holds
    /// as its content, and a line where it ends, open or not.
    /// </summary>
    public partial class DetailsBlock : ContentControl
    {
        private DetailsBlockHeader Toggle;
        private ContentPresenter Body;

        public DetailsBlock()
        {
            DefaultStyleKey = typeof(DetailsBlock);
        }

        protected override void OnApplyTemplate()
        {
            if (Toggle != null)
            {
                Toggle.Click -= OnToggleClick;
            }

            Toggle = GetTemplateChild(nameof(Toggle)) as DetailsBlockHeader;
            Body = GetTemplateChild(nameof(Body)) as ContentPresenter;

            if (Toggle != null)
            {
                Toggle.Click += OnToggleClick;
            }

            UpdateBody();
            base.OnApplyTemplate();
        }

        private void OnToggleClick(object sender, RoutedEventArgs e)
        {
            IsOpen = !IsOpen;
        }

        private void UpdateBody()
        {
            Body?.Visibility = IsOpen ? Visibility.Visible : Visibility.Collapsed;
        }

        #region Header

        public object Header
        {
            get => GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register("Header", typeof(object), typeof(DetailsBlock), new PropertyMetadata(null));

        #endregion

        #region IsOpen

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register("IsOpen", typeof(bool), typeof(DetailsBlock), new PropertyMetadata(false, OnIsOpenChanged));

        private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((DetailsBlock)d).UpdateBody();
        }

        #endregion
    }

    /// <summary>
    /// The header of a <see cref="DetailsBlock"/>. A hyperlink button for the hand cursor, with
    /// no states of its own: the chevron turning is what says it was clicked.
    /// </summary>
    public partial class DetailsBlockHeader : HyperlinkButton
    {
        private FrameworkElement Chevron;

        public DetailsBlockHeader()
        {
            DefaultStyleKey = typeof(DetailsBlockHeader);
        }

        protected override void OnApplyTemplate()
        {
            Chevron = GetTemplateChild(nameof(Chevron)) as FrameworkElement;

            // Where it stands at first is not a change, so it is put there rather than turned.
            Turn(false);
            base.OnApplyTemplate();
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new DetailsBlockHeaderAutomationPeer(this);
        }

        #region IsOpen

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register("IsOpen", typeof(bool), typeof(DetailsBlockHeader), new PropertyMetadata(false, OnIsOpenChanged));

        private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = (DetailsBlockHeader)d;

            // Not yet loaded, the header is being shown for the first time, as the editor does
            // when it rebuilds a block on undo: it was never anywhere else to turn from.
            sender.Turn(sender.IsLoaded);

            if (FrameworkElementAutomationPeer.FromElement(sender) is DetailsBlockHeaderAutomationPeer peer)
            {
                peer.RaiseExpandCollapseChanged((bool)e.OldValue, (bool)e.NewValue);
            }
        }

        #endregion

        // The chevron points down while closed and up while open, turning between the two.
        private void Turn(bool animate)
        {
            if (Chevron == null)
            {
                return;
            }

            var visual = ElementCompositionPreview.GetElementVisual(Chevron);
            var angle = IsOpen ? 180f : 0f;

            // The glyph's box is a fixed size in the template, so its middle is known before layout.
            visual.CenterPoint = new Vector3((float)Chevron.Width / 2, (float)Chevron.Height / 2, 0);

            if (!animate)
            {
                visual.RotationAngleInDegrees = angle;
                return;
            }

            var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
            animation.InsertKeyFrame(1, angle);
            animation.Duration = Constants.FastAnimation;

            visual.StartAnimation("RotationAngleInDegrees", animation);
        }
    }

    /// <summary>Read as what it is: something that expands and collapses, not just a link.</summary>
    public partial class DetailsBlockHeaderAutomationPeer : HyperlinkButtonAutomationPeer, IExpandCollapseProvider
    {
        private readonly DetailsBlockHeader _owner;

        public DetailsBlockHeaderAutomationPeer(DetailsBlockHeader owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override object GetPatternCore(PatternInterface patternInterface)
        {
            return patternInterface == PatternInterface.ExpandCollapse
                ? this
                : base.GetPatternCore(patternInterface);
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.Button;
        }

        public ExpandCollapseState ExpandCollapseState => _owner.IsOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public void Expand()
        {
            if (!_owner.IsOpen)
            {
                Invoke();
            }
        }

        public void Collapse()
        {
            if (_owner.IsOpen)
            {
                Invoke();
            }
        }

        internal void RaiseExpandCollapseChanged(bool wasOpen, bool isOpen)
        {
            RaisePropertyChangedEvent(
                ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                wasOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
                isOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
        }
    }
}
