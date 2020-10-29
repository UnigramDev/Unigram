using System;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls.Messages;
using Unigram.Services;
using Unigram.ViewModels;
using Windows.UI.Composition;
using Windows.UI.Composition.Interactions;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Chats
{
    public class ChatListViewItem : LazoListViewItem, IInteractionTrackerOwner
    {
        private readonly ChatListView _parent;

        private SpriteVisual _hitTest;
        private ContainerVisual _container;
        private Visual _visual;
        private ContainerVisual _indicator;

        private bool _hasInitialLoadedEventFired;
        private InteractionTracker _tracker;
        private VisualInteractionSource _interactionSource;

        private bool _forward;
        private bool _reply;

        private ListViewItemPresenter _presenter;

        public ChatListViewItem(ChatListView parent)
            : base(parent)
        {
            _parent = parent;

            AddHandler(PointerPressedEvent, new PointerEventHandler(OnPointerPressed), true);
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ChatListViewAutomationPeer(this);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _presenter = GetTemplateChild("Presenter") as ListViewItemPresenter;

            DetachEventHandlers();
            AttachEventHandlers();
        }

        private void AttachEventHandlers()
        {
            Loaded += OnLoaded;
            SizeChanged += OnSizeChanged;
        }

        private void DetachEventHandlers()
        {
            Loaded -= OnLoaded;
            SizeChanged -= OnSizeChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!_hasInitialLoadedEventFired)
            {
                _visual = ElementCompositionPreview.GetElementVisual(_presenter);

                _hitTest = _visual.Compositor.CreateSpriteVisual();
                _hitTest.Brush = _visual.Compositor.CreateColorBrush(Windows.UI.Colors.Transparent);

                _container = _visual.Compositor.CreateContainerVisual();
                _container.Children.InsertAtBottom(_hitTest);
                _container.Size = _hitTest.Size = new Vector2((float)ActualWidth, (float)ActualHeight);

                ElementCompositionPreview.SetElementChildVisual(this, _container);

                ConfigureInteractionTracker();
            }

            _hasInitialLoadedEventFired = true;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_hitTest != null)
            {
                _hitTest.Size = _container.Size = e.NewSize.ToVector2();
            }
        }

        private void ConfigureInteractionTracker()
        {
            _interactionSource = VisualInteractionSource.Create(_hitTest);

            //Configure for y-direction panning
            _interactionSource.ManipulationRedirectionMode = VisualInteractionSourceRedirectionMode.CapableTouchpadOnly;
            _interactionSource.PositionXSourceMode = InteractionSourceMode.EnabledWithInertia;
            _interactionSource.PositionXChainingMode = InteractionChainingMode.Never;
            _interactionSource.IsPositionXRailsEnabled = true;

            //Create tracker and associate interaction source
            _tracker = InteractionTracker.CreateWithOwner(_visual.Compositor, this);
            _tracker.InteractionSources.Add(_interactionSource);

            _tracker.MaxPosition = new Vector3(_reply ? 72 : 0);
            _tracker.MinPosition = new Vector3(_forward ? -72 : 0);

            _tracker.Properties.InsertBoolean("CanReply", _reply);
            _tracker.Properties.InsertBoolean("CanForward", _forward);

            //ConfigureAnimations(_visual, null);
            ConfigureRestingPoints();
        }

        private void ConfigureRestingPoints()
        {
            var neutralX = InteractionTrackerInertiaRestingValue.Create(_visual.Compositor);
            neutralX.Condition = _visual.Compositor.CreateExpressionAnimation("true");
            neutralX.RestingValue = _visual.Compositor.CreateExpressionAnimation("0");

            _tracker.ConfigurePositionXInertiaModifiers(new InteractionTrackerInertiaModifier[] { neutralX });
        }

        private void ConfigureAnimations(Visual visual, Visual indicator)
        {
            // Create an animation that changes the offset of the photoVisual and shadowVisual based on the manipulation progress
            var offsetExp = _visual.Compositor.CreateExpressionAnimation("(tracker.Position.X > 0 && !tracker.CanReply) || (tracker.Position.X <= 0 && !tracker.CanForward) ? 0 : -tracker.Position.X");
            //var photoOffsetExp = _visual.Compositor.CreateExpressionAnimation("tracker.Position.X > 0 && !tracker.CanReply || tracker.Position.X <= 0 && !tracker.CanForward ? 0 : Max(-72, Min(72, -tracker.Position.X))");
            //var photoOffsetExp = _visual.Compositor.CreateExpressionAnimation("-tracker.Position.X");
            offsetExp.SetReferenceParameter("tracker", _tracker);
            visual.StartAnimation("Offset.X", offsetExp);
        }

        #region ContentMargin

        public Thickness ContentMargin
        {
            get { return (Thickness)GetValue(ContentMarginProperty); }
            set { SetValue(ContentMarginProperty, value); }
        }

        public static readonly DependencyProperty ContentMarginProperty =
            DependencyProperty.Register("ContentMargin", typeof(Thickness), typeof(ChatListViewItem), new PropertyMetadata(default(Thickness)));

        #endregion

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Touch || e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Pen)
            {
                try
                {
                    _interactionSource.TryRedirectForManipulation(e.GetCurrentPoint(this));
                }
                catch (Exception)
                {
                    // Ignoring the failed redirect to prevent app crashing
                }
            }
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            if (_parent.SelectionMode == ListViewSelectionMode.Multiple && !IsSelected)
            {
                e.Handled = CantSelect();
            }

            base.OnPointerPressed(e);
        }

        public override bool CantSelect()
        {
            return ContentTemplateRoot is FrameworkElement element && element.Tag is MessageViewModel message && message.IsService();
        }

        public void PrepareForItemOverride(MessageViewModel message)
        {
            _reply = CanReply();
            _forward = CanForward();

            if (_tracker != null)
            {
                _tracker.Properties.InsertBoolean("CanReply", _reply);
                _tracker.Properties.InsertBoolean("CanForward", _forward);
                _tracker.MaxPosition = new Vector3(_reply ? 72 : 0);
                _tracker.MinPosition = new Vector3(_forward ? -72 : 0);

                if (_tracker.Position.X != 0)
                {
                    _tracker.TryUpdatePosition(new Vector3());
                }

                if (_visual != null && _visual.Offset.X != 0)
                {
                    _visual.Offset = new Vector3();
                }
            }
        }

        private bool CanReply()
        {
            if (ContentTemplateRoot is FrameworkElement element && element.Tag is MessageViewModel message)
            {
                if (message.IsService())
                {
                    return false;
                }

                var chat = message.GetChat();
                if (chat != null && chat.Type is ChatTypeSupergroup supergroupType)
                {
                    var supergroup = _parent.ViewModel.ProtoService.GetSupergroup(supergroupType.SupergroupId);
                    if (supergroup.IsChannel)
                    {
                        return supergroup.Status is ChatMemberStatusCreator || supergroup.Status is ChatMemberStatusAdministrator;
                    }
                    else if (supergroup.Status is ChatMemberStatusRestricted restricted)
                    {
                        return restricted.Permissions.CanSendMessages;
                    }
                }
                else if (chat != null)
                {
                    return chat.Permissions.CanSendMessages;
                }

                return true;
            }

            return false;
        }

        private bool CanForward()
        {
            if (ContentTemplateRoot is FrameworkElement element && element.Tag is MessageViewModel message)
            {
                return message.CanBeForwarded;
            }

            return false;
        }

        public void ValuesChanged(InteractionTracker sender, InteractionTrackerValuesChangedArgs args)
        {
            if (_indicator == null && ApiInfo.CanUseDirectComposition && (_tracker.Position.X > 0.0001f || _tracker.Position.X < -0.0001f) /*&& Math.Abs(e.Cumulative.Translation.X) >= 45*/)
            {
                var sprite = _visual.Compositor.CreateSpriteVisual();
                sprite.Size = new Vector2(30, 30);
                sprite.CenterPoint = new Vector3(15);

                var surface = LoadedImageSurface.StartLoadFromUri(new Uri("ms-appx:///Assets/Images/Reply.png"));
                surface.LoadCompleted += (s, e) =>
                {
                    sprite.Brush = _visual.Compositor.CreateSurfaceBrush(s);
                };

                var ellipse = _visual.Compositor.CreateEllipseGeometry();
                ellipse.Radius = new Vector2(15);

                var ellipseShape = _visual.Compositor.CreateSpriteShape(ellipse);
                ellipseShape.FillBrush = _visual.Compositor.CreateColorBrush((Windows.UI.Color)App.Current.Resources["MessageServiceBackgroundColor"]);
                ellipseShape.Offset = new Vector2(15);

                var shape = _visual.Compositor.CreateShapeVisual();
                shape.Shapes.Add(ellipseShape);
                shape.Size = new Vector2(30, 30);

                _indicator = _visual.Compositor.CreateContainerVisual();
                _indicator.Children.InsertAtBottom(shape);
                _indicator.Children.InsertAtTop(sprite);
                _indicator.Size = new Vector2(30, 30);
                _indicator.CenterPoint = new Vector3(15);
                _indicator.Scale = new Vector3();

                _container.Children.InsertAtTop(_indicator);

                //ElementCompositionPreview.SetElementChildVisual(this, _indicator);
                //ElementCompositionPreview.SetElementChildVisual(this, _container);
            }

            var offset = (_tracker.Position.X > 0 && !_reply) || (_tracker.Position.X <= 0 && !_forward) ? 0 : Math.Max(0, Math.Min(72, Math.Abs(_tracker.Position.X)));

            var abs = Math.Abs(offset);
            var percent = abs / 72f;

            var width = (float)ActualWidth;
            var height = (float)ActualHeight;

            if (_indicator != null)
            {
                _indicator.Offset = new Vector3(_tracker.Position.X > 0 ? width - percent * 60 : -30 + percent * 55, (height - 30) / 2, 0);
                _indicator.Scale = new Vector3(_tracker.Position.X > 0 ? 0.8f + percent * 0.2f : -(0.8f + percent * 0.2f), 0.8f + percent * 0.2f, 1);
                _indicator.Opacity = percent;
            }
        }

        public void InertiaStateEntered(InteractionTracker sender, InteractionTrackerInertiaStateEnteredArgs args)
        {
            if (ContentTemplateRoot is FrameworkElement element && element.Tag is MessageViewModel message)
            {
                if (_tracker.Position.X >= 72 && _reply)
                {
                    _parent.ViewModel.MessageReplyCommand.Execute(message);
                }
                else if (_tracker.Position.X <= -72 && _forward)
                {
                    _parent.ViewModel.MessageForwardCommand.Execute(message);
                }
            }
        }

        public void CustomAnimationStateEntered(InteractionTracker sender, InteractionTrackerCustomAnimationStateEnteredArgs args)
        {

        }

        public void IdleStateEntered(InteractionTracker sender, InteractionTrackerIdleStateEnteredArgs args)
        {

        }

        public void InteractingStateEntered(InteractionTracker sender, InteractionTrackerInteractingStateEnteredArgs args)
        {
            ConfigureAnimations(_visual, null);
        }

        public void RequestIgnored(InteractionTracker sender, InteractionTrackerRequestIgnoredArgs args)
        {

        }
    }

    public class AccessibleChatListViewItem : ListViewItem
    {
        private readonly IProtoService _protoService;

        public AccessibleChatListViewItem()
        {

        }

        public AccessibleChatListViewItem(IProtoService protoService)
        {
            _protoService = protoService;
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ChatListViewAutomationPeer(this, _protoService);
        }
    }

    public class ChatListViewAutomationPeer : ListViewItemAutomationPeer
    {
        private readonly ListViewItem _owner;
        private readonly IProtoService _protoService;

        public ChatListViewAutomationPeer(ListViewItem owner)
            : base(owner)
        {
            _owner = owner;
        }

        public ChatListViewAutomationPeer(ListViewItem owner, IProtoService protoService)
            : base(owner)
        {
            _owner = owner;
            _protoService = protoService;
        }

        protected override string GetNameCore()
        {
            if (_owner.ContentTemplateRoot is FrameworkElement content && content is MessageBubble == false)
            {
                var bubble = content.FindName("Bubble") as MessageBubble;
                if (bubble != null)
                {
                    return bubble.GetAutomationName() ?? base.GetNameCore();
                }
            }
            else if (_owner.ContentTemplateRoot is MessageBubble child)
            {
                return child.GetAutomationName() ?? base.GetNameCore();
            }
            else if (_owner.Content is Message message && _protoService != null)
            {
                return Automation.GetDescription(_protoService, message);
            }

            return base.GetNameCore();
        }
    }

    public class ChatGridViewItem : GridViewItem
    {
        private readonly IProtoService _protoService;

        public ChatGridViewItem()
        {

        }

        public ChatGridViewItem(IProtoService protoService)
        {
            _protoService = protoService;
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ChatGridViewAutomationPeer(this, _protoService);
        }
    }

    public class ChatGridViewAutomationPeer : GridViewItemAutomationPeer
    {
        private readonly ChatGridViewItem _owner;
        private readonly IProtoService _protoService;

        public ChatGridViewAutomationPeer(ChatGridViewItem owner)
            : base(owner)
        {
            _owner = owner;
        }

        public ChatGridViewAutomationPeer(ChatGridViewItem owner, IProtoService protoService)
            : base(owner)
        {
            _owner = owner;
            _protoService = protoService;
        }

        protected override string GetNameCore()
        {
            if (_owner.ContentTemplateRoot is FrameworkElement content && content is MessageBubble == false)
            {
                var bubble = content.FindName("Bubble") as MessageBubble;
                if (bubble != null)
                {
                    return bubble.GetAutomationName() ?? base.GetNameCore();
                }
            }
            else if (_owner.ContentTemplateRoot is MessageBubble child)
            {
                return child.GetAutomationName() ?? base.GetNameCore();
            }
            else if (_owner.Content is Message message && _protoService != null)
            {
                return Automation.GetDescription(_protoService, message);
            }

            return base.GetNameCore();
        }
    }
}
