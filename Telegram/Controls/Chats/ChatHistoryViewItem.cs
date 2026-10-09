//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Telegram.Common;
using Telegram.Controls.Messages;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Chats
{
    public enum ChatHistoryViewItemType
    {
        Outgoing,
        Incoming,
        Service,
        ServiceUnread,
        ServiceForumTopic,
        ServicePhoto,
        ServiceBirthdate,
        ServiceBackground,
        ServiceGift,
        ServiceGiftCode,
        ServiceUpgradedGift,
        ServiceUpgradedGiftPurchaseOffer,
        ServiceChatHasProtectedContentDisableRequested,
        ServiceMessageTonWalletTransfer,
        ServiceAccountInfo,
        ServiceNewThread,
        Unsupported,
    }

    public partial class ChatHistoryViewItem : ListViewItem
    {
        private ChatHistoryViewItemType _typeName;

        public ChatHistoryViewItem(ChatHistoryViewItemType typeName)
        {
            _typeName = typeName;
        }

        public ChatHistoryViewItemType TypeName
        {
            get => _typeName;
            set
            {
                if (_typeName != value)
                {
                    _typeName = value;
                    _brush = null;
                }
            }
        }

        private MessageBubbleBackgroundBrush _brush;

        /// <summary>
        /// The brush of the bubble in this container, which <see cref="MessageBubbleBackgroundCoordinator"/>
        /// sets; kept here because the container's padding is part of where the bubble sits.
        /// </summary>
        public MessageBubbleBackgroundBrush Brush
        {
            get => _brush;
            set
            {
                _brush = value;
                _brush?.Padding = (float)_paddingTop;
            }
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ChatListViewAutomationPeer(this);
        }

        private double _paddingTop;
        private double _paddingBottom;

        public void UpdatePadding(double top, double bottom)
        {
            var newTop = top >= 0 ? top : _paddingTop;
            var newBottom = bottom >= 0 ? bottom : _paddingBottom;

            if (_paddingTop != newTop || _paddingBottom != newBottom)
            {
                _paddingTop = newTop;
                _paddingBottom = newBottom;

                _brush?.Padding = (float)newTop;

                Padding = new Thickness(0, newTop, 0, newBottom);
            }
        }
    }

    /// <summary>
    /// Gives the outgoing bubbles under one owner their gradient: one fill across a shared frame,
    /// each bubble showing the slice behind it.
    /// </summary>
    /// <remarks>
    /// The owner decides the frame. A <see cref="ChatHistoryView"/> or a <see cref="ScrollViewer"/>
    /// scrolls its bubbles under a fill the size of its viewport; anything else is the frame
    /// itself, and does not scroll.
    /// </remarks>
    public sealed partial class MessageBubbleBackgroundCoordinator
    {
        private enum FrameKind
        {
            History,
            Scroll,
            Static
        }

        private readonly FrameworkElement _owner;
        private readonly WindowContext _window;
        private readonly FrameKind _kind;

        // History containers recycle and are walked from the panel instead.
        private readonly List<MessageBubble> _bubbles;
        private readonly HashSet<MessageBubbleBackgroundBrush> _connected = new();

        // Only what SizeChanged is subscribed on, so it can be removed from the same instance.
        private FrameworkElement _frame;
        private bool _loaded;

        // Read on demand, never from _frame: a brush in a page being shown again connects before the
        // owner's Loaded, and has to get the real size then.
        private FrameworkElement Frame => _owner is ChatHistoryView view
            ? view.ScrollingHost
            : _owner;

        private ThemeSettings _settings;
        private Vector<int> _stops;
        private Color _fallbackColor;
        private bool _animated;

        // Bumped on every theme change, so a brush that is already current costs nothing to attach.
        private int _version;

        /// <param name="window">
        /// The window whose chat theme the bubbles follow; may be null when <see cref="Settings"/>
        /// is always set.
        /// </param>
        public MessageBubbleBackgroundCoordinator(FrameworkElement owner, WindowContext window)
        {
            _owner = owner;
            _window = window;

            _kind = owner switch
            {
                ChatHistoryView => FrameKind.History,
                ScrollViewer => FrameKind.Scroll,
                _ => FrameKind.Static
            };

            if (_kind != FrameKind.History)
            {
                _bubbles = new List<MessageBubble>();
            }

            // The owner's own events: they live exactly as long as the owner, which is why these two
            // are never removed. Everything else is subscribed only while the owner is in the tree.
            _owner.Loaded += OnChanged;
            _owner.Unloaded += OnChanged;

            // Usually created once the owner is already loaded, in which case no Loaded is coming.
            OnChanged(null, null);
        }

        /// <summary>
        /// The settings the fill comes from; null follows the window's theme.
        /// </summary>
        public ThemeSettings Settings
        {
            get => _settings;
            set
            {
                _settings = value;
                Update();
            }
        }

        /// <summary>
        /// Gives a bubble its fill, or takes it away when the theme has none. Cheap to repeat: an
        /// attached bubble whose theme has not changed is left alone.
        /// </summary>
        public void Attach(MessageBubble bubble)
        {
            if (!_bubbles.Contains(bubble))
            {
                _bubbles.Add(bubble);
            }

            if (_loaded)
            {
                Apply(bubble, null);
            }
        }

        /// <summary>
        /// As <see cref="Attach(MessageBubble)"/>, for a bubble in a <see cref="ChatHistoryView"/>,
        /// whose position comes from its container.
        /// </summary>
        public void Attach(MessageBubble bubble, ChatHistoryViewItem container)
        {
            if (_loaded)
            {
                Apply(bubble, container);
            }
        }

        // The same check as the FrameworkElementEx family: Loaded and Unloaded arrive out of order
        // when an element is reparented, so the parent decides, and a repeat is a no-op.
        private void OnChanged(object sender, RoutedEventArgs e)
        {
            var parent = _owner.GetParent();
            if (parent != null && !_loaded)
            {
                _loaded = true;
                Connect();
            }
            else if (parent == null && _loaded)
            {
                _loaded = false;
                Disconnect();
            }
        }

        private void Connect()
        {
            _frame = Frame;

            if (_frame != null)
            {
                _frame.SizeChanged += OnSizeChanged;
            }

            _owner.ActualThemeChanged += OnActualThemeChanged;

            if (_window != null)
            {
                _window.ChatThemeChanged += OnChatThemeChanged;
            }

            // Brushes that connected before Loaded already have a size, but a resize while the owner
            // was away raises no SizeChanged now.
            UpdateEndPoints();

            // The theme may have changed while the owner was away; if it did not, nothing is redone.
            Update();
        }

        private void Disconnect()
        {
            if (_frame != null)
            {
                _frame.SizeChanged -= OnSizeChanged;
                _frame = null;
            }

            _owner.ActualThemeChanged -= OnActualThemeChanged;

            if (_window != null)
            {
                _window.ChatThemeChanged -= OnChatThemeChanged;
            }
        }

        private void OnChatThemeChanged(object sender, EventArgs e)
        {
            Update();
        }

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            Update();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateEndPoints();
        }

        private void UpdateEndPoints()
        {
            if (Frame is not FrameworkElement frame)
            {
                return;
            }

            var size = frame.ActualSize;

            foreach (var brush in _connected)
            {
                brush.EndPoint = size;
            }
        }

        private void Update()
        {
            if (!_loaded)
            {
                return;
            }

            Resolve();

            if (_owner is ChatHistoryView view)
            {
                if (view.ItemsPanelRoot == null)
                {
                    return;
                }

                foreach (var child in view.ItemsPanelRoot.Children)
                {
                    if (child is ChatHistoryViewItem container && container.ContentTemplateRoot is MessageSelector { ContentTemplateRoot: MessageBubble bubble })
                    {
                        Apply(bubble, container);
                    }
                }
            }
            else
            {
                foreach (var bubble in _bubbles)
                {
                    Apply(bubble, null);
                }
            }
        }

        private void Resolve()
        {
            var light = _owner.ActualTheme == ElementTheme.Light;
            var settings = _settings;

            Color fallbackColor = default;

            if (_window != null)
            {
                settings = light ? _window.LightSettings : _window.DarkSettings;
                settings ??= AppSettings.Appearance.GetSettings(light ? Services.Settings.TelegramTheme.Light : Services.Settings.TelegramTheme.Dark);
            }

            if (settings != null)
            {
                var outgoingMessageAccentColor = settings.GetOutgoingMessageAccentColor();
                var outgoingColorizer = ThemeAccentInfo.Colorize(settings, outgoingMessageAccentColor, "MessageBackgroundBrush");
                fallbackColor = outgoingColorizer;
            }

            Vector<int> stops = null;
            var animated = false;

            if (settings?.OutgoingMessageFill is BackgroundFillGradient gradient)
            {
                stops = new[] { gradient.TopColor, gradient.BottomColor };
            }
            else if (settings?.OutgoingMessageFill is BackgroundFillFreeformGradient freeformGradient)
            {
                stops = freeformGradient.Colors;
                animated = settings.AnimateOutgoingMessageFill;
            }

            stops = stops?.Count > 0 ? stops : null;

            // Only a real change moves the version: every reconnect and every window event lands
            // here, and an unchanged theme must leave the brushes alone.
            if (animated == _animated && fallbackColor == _fallbackColor && AreTheSame(stops, _stops))
            {
                return;
            }

            _stops = stops;
            _animated = animated;
            _fallbackColor = fallbackColor;
            _version++;
        }

        private static bool AreTheSame(Vector<int> x, Vector<int> y)
        {
            if (x == null || y == null)
            {
                return x == null && y == null;
            }

            if (x.Count != y.Count)
            {
                return false;
            }

            for (int i = 0; i < x.Count; i++)
            {
                if (x[i] != y[i])
                {
                    return false;
                }
            }

            return true;
        }

        private void Apply(MessageBubble bubble, ChatHistoryViewItem container)
        {
            if (container != null && container.TypeName != ChatHistoryViewItemType.Outgoing)
            {
                return;
            }

            if (_stops == null)
            {
                if (bubble.Brush != null)
                {
                    bubble.UpdateContainer(null);
                }

                container?.Brush = null;
                return;
            }

            var brush = bubble.Brush;
            if (brush == null || brush.Coordinator != this)
            {
                brush = new MessageBubbleBackgroundBrush(this, bubble);
            }
            else if (brush.Version == _version && brush.Container == container)
            {
                return;
            }

            brush.Container = container;
            brush.Update(_stops, _animated, _fallbackColor, _version);

            container?.Brush = brush;

            if (bubble.Brush != brush)
            {
                bubble.UpdateContainer(brush);
            }
        }

        internal void Connected(MessageBubbleBackgroundBrush brush)
        {
            _connected.Add(brush);
        }

        internal void Disconnected(MessageBubbleBackgroundBrush brush)
        {
            _connected.Remove(brush);
        }

        /// <summary>
        /// Where the slice behind <paramref name="brush"/> starts: minus the bubble's position in the
        /// frame, kept current by composition rather than by layout events.
        /// </summary>
        internal ExpressionAnimation CreateAnimation(MessageBubbleBackgroundBrush brush, Compositor compositor, out Vector2 frame)
        {
            frame = Frame?.ActualSize ?? Vector2.Zero;

            if (_owner is ChatHistoryView view)
            {
                if (brush.Container?.ContentTemplateRoot is not MessageSelector selector || view.ItemsPanelRoot == null)
                {
                    return null;
                }

                var animation = compositor.CreateExpressionAnimation("-(scroll.Translation.Y + panel.Offset.Y + reference.Offset.Y + padding + bubble.Offset.Y)");
                animation.SetReferenceParameter("reference", ElementCompositionPreview.GetElementVisual(brush.Container));
                animation.SetReferenceParameter("bubble", selector.ContentTemplateVisual);
                animation.SetReferenceParameter("panel", ElementCompositionPreview.GetElementVisual(view.ItemsPanelRoot));
                animation.SetReferenceParameter("scroll", view.ScrollingPropertySet);
                animation.SetScalarParameter("padding", brush.Padding);

                return animation;
            }
            else if (_owner is ScrollViewer scrollViewer)
            {
                // Down to the content, whose own offset counts: the scroll translation moves it.
                if (scrollViewer.Content is not UIElement content)
                {
                    return null;
                }

                var animation = CreateChain(compositor, brush.Bubble, content, true, "scroll.Translation.Y + ");
                animation?.SetReferenceParameter("scroll", ElementCompositionPreview.GetScrollViewerManipulationPropertySet(scrollViewer));

                return animation;
            }

            return CreateChain(compositor, brush.Bubble, _owner, false, string.Empty);
        }

        // The bubble's offset in the frame is the sum of its ancestors' up to it. Null when the
        // bubble is not under the frame at all.
        private static ExpressionAnimation CreateChain(Compositor compositor, UIElement bubble, UIElement frame, bool includeFrame, string prefix)
        {
            var animation = compositor.CreateExpressionAnimation();
            var expression = new StringBuilder("-(");
            expression.Append(prefix);

            var count = 0;

            DependencyObject current = bubble;
            while (current != null && current != frame)
            {
                if (current is UIElement element)
                {
                    var name = "v" + count++;
                    animation.SetReferenceParameter(name, ElementCompositionPreview.GetElementVisual(element));
                    expression.Append(name).Append(".Offset.Y + ");
                }

                current = VisualTreeHelper.GetParent(current);
            }

            if (current == null)
            {
                animation.Dispose();
                return null;
            }

            if (includeFrame)
            {
                var name = "v" + count;
                animation.SetReferenceParameter(name, ElementCompositionPreview.GetElementVisual(frame));
                expression.Append(name).Append(".Offset.Y + ");
            }

            animation.Expression = expression.Append("0)").ToString();
            return animation;
        }
    }

    /// <summary>
    /// The gradient behind one outgoing bubble, as <see cref="MessageBubbleBackgroundCoordinator"/>
    /// hands it out. Its stops are the fill's; the coordinator decides where the slice starts.
    /// </summary>
    public partial class MessageBubbleBackgroundBrush : XamlCompositionBrushBase
    {
        public MessageBubbleBackgroundBrush(MessageBubbleBackgroundCoordinator coordinator, MessageBubble bubble)
        {
            Coordinator = coordinator;
            Bubble = bubble;
        }

        public MessageBubbleBackgroundCoordinator Coordinator { get; }

        public MessageBubble Bubble { get; }

        /// <summary>
        /// The history container the bubble sits in, or null outside a <see cref="ChatHistoryView"/>.
        /// </summary>
        public ChatHistoryViewItem Container { get; set; }

        /// <summary>
        /// The coordinator's theme version these stops were taken from.
        /// </summary>
        public int Version { get; private set; }

        /// <summary>
        /// Whether the fill was authored to be animated, which decides the order of the stops.
        /// </summary>
        public bool IsAnimated { get; private set; }

        private CompositionLinearGradientBrush _gradientBrush;
        private ExpressionAnimation _animation;

        private Vector<int> _colors;

        public void Update(Vector<int> colors, bool animated, Color fallbackColor, int version)
        {
            // Before the stops: it decides their order.
            IsAnimated = animated;
            FallbackColor = fallbackColor;
            Version = version;

            _colors = colors;

            if (_gradientBrush != null)
            {
                _gradientBrush.ColorStops.Clear();
                ApplyColorStops(colors);
            }
        }

        private void ApplyColorStops(Vector<int> value)
        {
            Span<int> sorted = stackalloc int[4];
            var count = IsAnimated
                ? TdBackground.SortByLuminance(value, sorted)
                : TdBackground.Reverse(value, sorted);

            var offset = 0f;
            var step = 1f / (count - 1);

            for (int i = 0; i < count; i++)
            {
                _gradientBrush.ColorStops.Add(_gradientBrush.Compositor.CreateColorGradientStop(offset, sorted[i].ToColor()));
                offset += step;
            }
        }

        private float _padding;
        public float Padding
        {
            get => _padding;
            set
            {
                if (_padding != value)
                {
                    _padding = value;

                    // Started again: composition copies an animation's parameters when it starts,
                    // so changing one on the running instance does nothing.
                    if (_animation != null)
                    {
                        _animation.SetScalarParameter("padding", value);
                        _gradientBrush?.StartAnimation("Offset.Y", _animation);
                    }
                }
            }
        }

        public Vector2 EndPoint
        {
            set
            {
                _gradientBrush?.EndPoint = new Vector2(0, value.Y);
            }
        }

        protected override void OnConnected()
        {
            base.OnConnected();

            var compositor = BootStrapper.Current.Compositor;

            _animation = Coordinator.CreateAnimation(this, compositor, out Vector2 frame);
            if (_animation == null)
            {
                return;
            }

            _gradientBrush = compositor.CreateLinearGradientBrush();
            _gradientBrush.MappingMode = CompositionMappingMode.Absolute;
            _gradientBrush.StartPoint = new Vector2(0, 0);
            _gradientBrush.EndPoint = new Vector2(0, frame.Y);

            _gradientBrush.StartAnimation("Offset.Y", _animation);

            ApplyColorStops(_colors);

            CompositionBrush = _gradientBrush;
            Coordinator.Connected(this);
        }

        protected override void OnDisconnected()
        {
            base.OnDisconnected();

            Coordinator.Disconnected(this);
            CompositionBrush = null;

            if (_gradientBrush != null)
            {
                _gradientBrush.StopAnimation("Offset.Y");
                _gradientBrush.Dispose();
                _gradientBrush = null;
            }

            _animation?.Dispose();
            _animation = null;
        }
    }

    public partial class TableAccessibleChatListViewItem : TableListViewItem
    {
        private readonly ListViewBase _parent;

        public TableAccessibleChatListViewItem(ListViewBase parent)
        {
            _parent = parent;
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ChatListViewAutomationPeer(_parent, this);
        }
    }

    public partial class ChatListViewAutomationPeer : ListViewItemAutomationPeer
    {
        private readonly ListViewBase _parent;
        private readonly ListViewItem _owner;

        public ChatListViewAutomationPeer(ListViewItem owner)
            : base(owner)
        {
            _owner = owner;
        }

        public ChatListViewAutomationPeer(ListViewBase parent, ListViewItem owner)
            : base(owner)
        {
            _parent = parent;
            _owner = owner;
        }

        protected override string GetNameCore()
        {
            if (_owner.ContentTemplateRoot is MessageSelector selector)
            {
                var bubble = selector.Content as MessageBubble;
                if (bubble != null)
                {
                    return bubble.GetAutomationName() ?? base.GetNameCore();
                }
            }
            else if (_owner.ContentTemplateRoot is MessageBubble child)
            {
                return child.GetAutomationName() ?? base.GetNameCore();
            }
            else if (_owner.ContentTemplateRoot is MessageService service)
            {
                return AutomationProperties.GetName(service);
            }
            else if (_owner.ContentTemplateRoot is StackPanel panel && panel.Children.Count > 0)
            {
                if (panel.Children[0] is MessageService sservice)
                {
                    return AutomationProperties.GetName(sservice);
                }
            }

            var content = _parent?.ItemFromContainer(_owner);
            if (content is MessageWithOwner messageWithOwner)
            {
                return Automation.GetSummaryWithName(messageWithOwner, true);
            }

            return base.GetNameCore();
        }
    }

    public partial class ChatGridViewItem : GridViewItem
    {
        private readonly ListViewBase _parent;

        public ChatGridViewItem(ListViewBase parent)
        {
            _parent = parent;
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ChatGridViewAutomationPeer(_parent, this);
        }
    }

    public partial class ChatGridViewAutomationPeer : GridViewItemAutomationPeer
    {
        private readonly ListViewBase _parent;
        private readonly ChatGridViewItem _owner;

        public ChatGridViewAutomationPeer(ListViewBase parent, ChatGridViewItem owner)
            : base(owner)
        {
            _parent = parent;
            _owner = owner;
        }

        protected override string GetNameCore()
        {
            if (_owner.ContentTemplateRoot is MessageSelector selector)
            {
                var bubble = selector.Content as MessageBubble;
                if (bubble != null)
                {
                    return bubble.GetAutomationName() ?? base.GetNameCore();
                }
            }
            else if (_owner.ContentTemplateRoot is MessageBubble child)
            {
                return child.GetAutomationName() ?? base.GetNameCore();
            }

            var content = _parent.ItemFromContainer(_owner);
            if (content is MessageWithOwner messageWithOwner)
            {
                return Automation.GetSummaryWithName(messageWithOwner, true);
            }

            return base.GetNameCore();
        }
    }
}
