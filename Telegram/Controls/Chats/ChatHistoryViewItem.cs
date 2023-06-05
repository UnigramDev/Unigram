//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls.Messages;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Composition.Interactions;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Chats
{
    public class ChatHistoryViewItem : ListViewItem, IInteractionTrackerOwner
    {
        private readonly ChatHistoryView _owner;
        private readonly string _typeName;

        private SpriteVisual _hitTest;
        private ContainerVisual _container;
        private Visual _visual;
        private ContainerVisual _indicator;

        private bool _hasInitialLoadedEventFired;
        private InteractionTracker _tracker;
        private VisualInteractionSource _interactionSource;

        private bool _requiresArrange;

        private bool _share;
        private bool _reply;

        private FrameworkElement _presenter;

        public ChatHistoryViewItem(ChatHistoryView owner, string typeName)
        {
            _owner = owner;
            _typeName = typeName;

            AddHandler(PointerPressedEvent, new PointerEventHandler(OnPointerPressed), true);
        }

        public string TypeName => _typeName;

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ChatListViewAutomationPeer(this);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (ApiInfo.IsWindows11)
            {
                _requiresArrange = false;
                _presenter = VisualTreeHelper.GetChild(this, 0) as FrameworkElement;
            }
            else
            {
                _requiresArrange = true;
                _presenter = GetTemplateChild("ContentBorder") as FrameworkElement;
            }

            DetachEventHandlers();
            AttachEventHandlers();
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            Logger.Debug();

            if (_hitTest != null && _requiresArrange)
            {
                _hitTest.Size = finalSize.ToVector2();
                _container.Size = finalSize.ToVector2();
            }

            return base.ArrangeOverride(finalSize);
        }

        private void AttachEventHandlers()
        {
            Loaded += OnLoaded;
        }

        private void DetachEventHandlers()
        {
            Loaded -= OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!_hasInitialLoadedEventFired && _presenter != null && (SettingsService.Current.SwipeToReply || SettingsService.Current.SwipeToShare))
            {
                _visual = ElementCompositionPreview.GetElementVisual(_presenter);

                _hitTest = _visual.Compositor.CreateSpriteVisual();
                _hitTest.Brush = _visual.Compositor.CreateColorBrush(Windows.UI.Colors.Transparent);

                _container = _visual.Compositor.CreateContainerVisual();
                _container.Children.InsertAtBottom(_hitTest);

                if (_requiresArrange)
                {
                    _hitTest.Size = ActualSize;
                    _container.Size = ActualSize;
                }
                else
                {
                    _hitTest.RelativeSizeAdjustment = Vector2.One;
                    _container.RelativeSizeAdjustment = Vector2.One;
                }

                ElementCompositionPreview.SetElementChildVisual(this, _container);

                ConfigureInteractionTracker();
            }

            _hasInitialLoadedEventFired = true;
        }

        private void ConfigureInteractionTracker()
        {
            _interactionSource = VisualInteractionSource.Create(_hitTest);

            //Configure for x-direction panning
            _interactionSource.ManipulationRedirectionMode = VisualInteractionSourceRedirectionMode.CapableTouchpadOnly;
            _interactionSource.PositionXSourceMode = InteractionSourceMode.EnabledWithInertia;
            _interactionSource.PositionXChainingMode = InteractionChainingMode.Never;
            _interactionSource.IsPositionXRailsEnabled = true;

            //Create tracker and associate interaction source
            _tracker = InteractionTracker.CreateWithOwner(_visual.Compositor, this);
            _tracker.InteractionSources.Add(_interactionSource);

            _tracker.MaxPosition = new Vector3(_reply ? 72 : 0);
            _tracker.MinPosition = new Vector3(_share ? -72 : 0);

            _tracker.Properties.InsertBoolean("CanReply", _reply);
            _tracker.Properties.InsertBoolean("CanShare", _share);

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
            var offsetExp = _visual.Compositor.CreateExpressionAnimation("(tracker.Position.X > 0 && !tracker.CanReply) || (tracker.Position.X <= 0 && !tracker.CanShare) ? 0 : -tracker.Position.X");
            //var photoOffsetExp = _visual.Compositor.CreateExpressionAnimation("tracker.Position.X > 0 && !tracker.CanReply || tracker.Position.X <= 0 && !tracker.CanShare ? 0 : Max(-72, Min(72, -tracker.Position.X))");
            //var photoOffsetExp = _visual.Compositor.CreateExpressionAnimation("-tracker.Position.X");
            offsetExp.SetReferenceParameter("tracker", _tracker);
            visual.StartAnimation("Offset.X", offsetExp);
        }

        #region ContentMargin

        public Thickness ContentMargin
        {
            get => (Thickness)GetValue(ContentMarginProperty);
            set => SetValue(ContentMarginProperty, value);
        }

        public static readonly DependencyProperty ContentMarginProperty =
            DependencyProperty.Register("ContentMargin", typeof(Thickness), typeof(ChatHistoryViewItem), new PropertyMetadata(default(Thickness)));

        #endregion

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse)
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
            if (_owner.SelectionMode == ListViewSelectionMode.Multiple && !IsSelected)
            {
                e.Handled = ContentTemplateRoot is not MessageSelector;
            }

            base.OnPointerPressed(e);
        }

        public void PrepareForItemOverride(MessageViewModel message, bool canReply)
        {
            var share = SettingsService.Current.SwipeToShare && message.CanBeForwarded;
            var reply = SettingsService.Current.SwipeToReply && canReply && ContentTemplateRoot is MessageSelector;

            if (_tracker != null)
            {
                if (_share != share)
                {
                    _tracker.Properties.InsertBoolean("CanShare", share);
                    _tracker.MinPosition = new Vector3(share ? -72 : 0);
                }

                if (_reply != reply)
                {
                    _tracker.Properties.InsertBoolean("CanReply", reply);
                    _tracker.MaxPosition = new Vector3(reply ? 72 : 0);
                }

                if (_tracker.Position.X != 0)
                {
                    _tracker.TryUpdatePosition(new Vector3());
                }

                if (_visual != null && _visual.Offset.X != 0)
                {
                    _visual.Offset = new Vector3();
                }
            }

            _share = share;
            _reply = reply;
        }

        public void ValuesChanged(InteractionTracker sender, InteractionTrackerValuesChangedArgs args)
        {
            if (_indicator == null && (_tracker.Position.X > 0.0001f || _tracker.Position.X < -0.0001f) /*&& Math.Abs(e.Cumulative.Translation.X) >= 45*/)
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
                ellipseShape.FillBrush = _visual.Compositor.CreateColorBrush((Windows.UI.Color)Navigation.BootStrapper.Current.Resources["MessageServiceBackgroundColor"]);
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

            var offset = (_tracker.Position.X > 0 && !_reply) || (_tracker.Position.X <= 0 && !_share) ? 0 : Math.Max(0, Math.Min(72, Math.Abs(_tracker.Position.X)));

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
            if (ContentTemplateRoot is MessageSelector selector && selector.Message != null)
            {
                if (_tracker.Position.X >= 72 && _reply)
                {
                    _owner.ViewModel.ReplyToMessage(selector.Message);
                }
                else if (_tracker.Position.X <= -72 && _share)
                {
                    _owner.ViewModel.ForwardMessage(selector.Message);
                }
            }
        }

        public void CustomAnimationStateEntered(InteractionTracker sender, InteractionTrackerCustomAnimationStateEnteredArgs args)
        {

        }

        public void IdleStateEntered(InteractionTracker sender, InteractionTrackerIdleStateEnteredArgs args)
        {
            ConfigureAnimations(_visual, null);
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
        private readonly IClientService _clientService;

        public AccessibleChatListViewItem()
        {

        }

        public AccessibleChatListViewItem(IClientService clientService)
        {
            _clientService = clientService;
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ChatListViewAutomationPeer(this, _clientService);
        }
    }

    public class TableAccessibleChatListViewItem : TableListViewItem
    {
        private readonly IClientService _clientService;

        public TableAccessibleChatListViewItem()
        {

        }

        public TableAccessibleChatListViewItem(IClientService clientService)
        {
            _clientService = clientService;
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ChatListViewAutomationPeer(this, _clientService);
        }
    }

    public class ChatListViewAutomationPeer : ListViewItemAutomationPeer
    {
        private readonly ListViewItem _owner;
        private readonly IClientService _clientService;

        public ChatListViewAutomationPeer(ListViewItem owner)
            : base(owner)
        {
            _owner = owner;
        }

        public ChatListViewAutomationPeer(ListViewItem owner, IClientService clientService)
            : base(owner)
        {
            _owner = owner;
            _clientService = clientService;
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

            return base.GetNameCore();
        }
    }

    public class ChatGridViewItem : GridViewItem
    {
        private readonly IClientService _clientService;

        public ChatGridViewItem()
        {

        }

        public ChatGridViewItem(IClientService clientService)
        {
            _clientService = clientService;
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ChatGridViewAutomationPeer(this, _clientService);
        }
    }

    public class ChatGridViewAutomationPeer : GridViewItemAutomationPeer
    {
        private readonly ChatGridViewItem _owner;
        private readonly IClientService _clientService;

        public ChatGridViewAutomationPeer(ChatGridViewItem owner)
            : base(owner)
        {
            _owner = owner;
        }

        public ChatGridViewAutomationPeer(ChatGridViewItem owner, IClientService clientService)
            : base(owner)
        {
            _owner = owner;
            _clientService = clientService;
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
            else if (_owner.Content is Message message && _clientService != null)
            {
                return Automation.GetDescription(_clientService, message);
            }

            return base.GetNameCore();
        }
    }
}
