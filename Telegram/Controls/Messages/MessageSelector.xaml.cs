//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Numerics;
using Telegram.Assets.Icons;
using Telegram.Common;
using Telegram.Composition;
using Telegram.Controls.Chats;
using Telegram.Controls.Messages.Content;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Composition.Interactions;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Messages
{
    public sealed partial class MessageSelector : ToggleButtonEx
    {
        private Border Icon;
        private ContentPresenter Presenter;

        private bool _templateApplied;

        private bool _isSelected;

        private MessageViewModel _message;
        private ChatHistoryView _owner;

        public MessageSelector()
        {
            DefaultStyleKey = typeof(MessageSelector);

            Connected += OnLoaded;
            Disconnected += OnUnloaded;

            AddHandler(PointerPressedEvent, new PointerEventHandler(OnPointerPressed), true);
        }

        public MessageSelector(MessageViewModel message, UIElement child)
            : this()
        {
            _message = message;
            Content = child;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!_hasInitialLoadedEventFired && RootGrid != null && (SettingsService.Current.SwipeToReply || SettingsService.Current.SwipeToShare))
            {
                _hasInitialLoadedEventFired = true;

                _hitTest = ElementComposition.GetElementVisual(this);
                _visual = ElementComposition.GetElementVisual(RootGrid);

                _compositor = _hitTest.Compositor;
                _container ??= _compositor.CreateContainerVisual();

                if (_requiresArrange)
                {
                    _container.Size = ActualSize;
                }
                else
                {
                    _container.RelativeSizeAdjustment = Vector2.One;
                }

                ElementCompositionPreview.SetElementChildVisual(this, _container);
                ConfigureInteractionTracker();
            }

            if (_trackerOwner != null)
            {
                _trackerOwner.ValuesChanged += OnValuesChanged;
                _trackerOwner.InertiaStateEntered += OnInertiaStateEntered;
                _trackerOwner.InteractingStateEntered += OnInteractingStateEntered;
                _trackerOwner.IdleStateEntered += OnIdleStateEntered;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_trackerOwner != null)
            {
                _trackerOwner.ValuesChanged -= OnValuesChanged;
                _trackerOwner.InertiaStateEntered -= OnInertiaStateEntered;
                _trackerOwner.InteractingStateEntered -= OnInteractingStateEntered;
                _trackerOwner.IdleStateEntered -= OnIdleStateEntered;
            }

            if (_message != null)
            {
                Recycle();
            }
        }

        public MessageViewModel Message => _message;

        public void Recycle()
        {
            if (Content is MessageBubble bubble)
            {
                bubble.Recycle();
            }
            else if (Content is IContent content)
            {
                content.Recycle();
            }

            _message?.UpdateSelectionCallback(null);
            _message = null;

            _owner = null;
        }

        private void CreateIcon()
        {
            if (Icon != null || !_isSelectionEnabled)
            {
                return;
            }

            var visual = GetVisual(Window.Current.Compositor, out var source, out _props);

            _source = source;
            _previous = visual;

            Icon = GetTemplateChild(nameof(Icon)) as Border;
            ElementCompositionPreview.SetIsTranslationEnabled(Icon, true);
            ElementCompositionPreview.SetElementChildVisual(Icon, visual?.RootVisual);

            RegisterPropertyChangedCallback(BackgroundProperty, OnBackgroundChanged);
            OnBackgroundChanged(this, BackgroundProperty);

            if (IsAlbumChild)
            {
                if (_message.Content is MessagePhoto or MessageVideo)
                {
                    Icon.VerticalAlignment = VerticalAlignment.Top;
                    Icon.HorizontalAlignment = HorizontalAlignment.Right;
                    Icon.Margin = new Thickness(0, 4, 6, 0);
                }
                else
                {
                    Icon.VerticalAlignment = VerticalAlignment.Center;
                    Icon.HorizontalAlignment = HorizontalAlignment.Left;
                    Icon.Margin = new Thickness(28, 0, 0, 4);
                }

                Grid.SetColumn(Icon, 1);
            }
        }

        private void OnBackgroundChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (_source != null && Background is SolidColorBrush background)
            {
                _source.SetColorProperty("Color_FF0000", background.Color);
            }
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            RootGrid = GetTemplateChild(nameof(RootGrid)) as Grid;
            ElementCompositionPreview.SetIsTranslationEnabled(RootGrid, true);

            Presenter = GetTemplateChild(nameof(Presenter)) as ContentPresenter;
            ElementCompositionPreview.SetIsTranslationEnabled(Presenter, true);

            _templateApplied = true;

            if (_message != null)
            {
                UpdateMessage(_message, _owner);
            }
        }

        protected override void OnToggle()
        {
            if (_isSelectionEnabled && _message is MessageViewModel message)
            {
                base.OnToggle();

                CreateIcon();
                UpdateIcon(IsChecked is true, true);

                if (IsChecked is true)
                {
                    message.Delegate.Select(message);
                }
                else
                {
                    message.Delegate.Unselect(message);
                }
            }
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (e.OriginalSource == RootGrid)
            {
                _owner?.OnPointerPressed(this, e);
            }
        }

        protected override void OnPointerEntered(PointerRoutedEventArgs e)
        {
            base.OnPointerEntered(e);

            if (e.OriginalSource == RootGrid)
            {
                _owner?.OnPointerEntered(this, e);
            }
        }

        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            base.OnPointerMoved(e);

            if (e.OriginalSource == RootGrid)
            {
                _owner?.OnPointerMoved(this, e);
            }
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            base.OnPointerReleased(e);

            if (e.OriginalSource == RootGrid)
            {
                _owner?.OnPointerReleased(this, e);
            }
        }

        protected override void OnPointerCanceled(PointerRoutedEventArgs e)
        {
            base.OnPointerCanceled(e);

            if (e.OriginalSource == RootGrid)
            {
                _owner?.OnPointerCanceled(this, e);
            }
        }

        public void UpdateMessage(MessageViewModel message, ChatHistoryView owner)
        {
            _message = message;
            _owner = owner;

            if (message == null || !_templateApplied)
            {
                return;
            }

            message.UpdateSelectionCallback(UpdateSelection);

            var selected = _isSelectionEnabled && message.Delegate.SelectedItems.ContainsKey(message.Id);
            if (selected == _isSelected)
            {
                return;
            }

            IsChecked = _isSelected = selected;
            Presenter.IsHitTestVisible = !_isSelectionEnabled || IsAlbum;

            CreateIcon();
            UpdateIcon(IsChecked is true, false);

            if (Icon != null)
            {
                var icon = ElementComposition.GetElementVisual(Icon);
                icon.Properties.InsertVector3("Translation", new Vector3(_isSelectionEnabled ? 0 : -36, 0, 0));
            }

            if (IsAlbumChild)
            {
                if (Icon != null)
                {
                    if (_message.Content is MessagePhoto or MessageVideo)
                    {
                        Icon.VerticalAlignment = VerticalAlignment.Top;
                        Icon.HorizontalAlignment = HorizontalAlignment.Right;
                        Icon.Margin = new Thickness(0, 4, 6, 0);
                    }
                    else
                    {
                        Icon.VerticalAlignment = VerticalAlignment.Bottom;
                        Icon.HorizontalAlignment = HorizontalAlignment.Left;
                        Icon.Margin = new Thickness(28, 0, 0, 4);
                    }

                    Grid.SetColumn(Icon, 1);
                }
            }
            else
            {
                var presenter = ElementComposition.GetElementVisual(Presenter);
                presenter.Offset = new Vector3(_isSelectionEnabled && (message.IsChannelPost || !message.IsOutgoing) ? 36 : 0, 0, 0);
            }
        }

        private bool _isSelectionEnabled;

        public void UpdateSelectionEnabled(bool value, bool animate)
        {
            if (_isSelectionEnabled == value)
            {
                return;
            }

            _isSelectionEnabled = value;

            if (_message is MessageViewModel message)
            {
                var selected = value && message.Delegate.SelectedItems.ContainsKey(message.Id);

                IsChecked = _isSelected = selected;
                Presenter.IsHitTestVisible = !value || IsAlbum;

                CreateIcon();

                var presenter = ElementComposition.GetElementVisual(Presenter);
                var outgoing = (message.IsOutgoing && !message.IsChannelPost && message.SenderId is MessageSenderUser) || (message.IsSaved && message.ForwardInfo?.Source is { IsOutgoing: true });

                if (animate)
                {
                    var offset = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
                    offset.InsertKeyFrame(0, value ? -36 : 0);
                    offset.InsertKeyFrame(1, value ? 0 : -36);

                    var scale = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
                    scale.InsertKeyFrame(0, value ? Vector3.Zero : Vector3.One);
                    scale.InsertKeyFrame(1, value ? Vector3.One : Vector3.Zero);

                    if (Icon != null)
                    {
                        UpdateIcon(IsChecked is true, true);

                        var icon = ElementComposition.GetElementVisual(Icon);
                        icon.CenterPoint = new Vector3(12, 12, 0);
                        icon.StartAnimation("Scale", scale);

                        if (!IsAlbumChild)
                        {
                            icon.StartAnimation("Translation.X", offset);
                        }
                    }

                    if (!outgoing && !IsAlbumChild)
                    {
                        offset.InsertKeyFrame(0, value ? 0 : 36);
                        offset.InsertKeyFrame(1, value ? 36 : 0);

                        presenter.StartAnimation("Offset.X", offset);
                    }
                    else
                    {
                        presenter.Offset = Vector3.Zero;
                    }
                }
                else
                {
                    if (Icon != null)
                    {
                        UpdateIcon(IsChecked is true, false);

                        var icon = ElementComposition.GetElementVisual(Icon);
                        icon.Properties.InsertVector3("Translation", new Vector3(value && !IsAlbumChild ? 0 : -36, 0, 0));
                        icon.Scale = value ? Vector3.One : Vector3.Zero;
                    }

                    if (!IsAlbumChild)
                    {
                        presenter.Offset = new Vector3(value && outgoing ? 0 : 36, 0, 0);
                    }
                }
            }

            if (Content is MessageBubble bubble && bubble.MediaTemplateRoot is AlbumContent album)
            {
                album.UpdateSelectionEnabled(value, animate);
            }
        }

        public void UpdateSelection()
        {
            var message = _message;
            if (message != null && _templateApplied)
            {
                bool selected;
                if (message.Content is MessageAlbum album)
                {
                    selected = album.Messages.All(x => message.Delegate.SelectedItems.ContainsKey(x.Id));
                }
                else
                {
                    selected = message.Delegate.SelectedItems.ContainsKey(message.Id);
                }

                selected = _isSelectionEnabled && selected;

                if (selected != _isSelected)
                {
                    IsChecked = _isSelected = selected;
                    Presenter.IsHitTestVisible = !_isSelectionEnabled || IsAlbum;

                    CreateIcon();
                    UpdateIcon(IsChecked is true, true);

                    var peer = FrameworkElementAutomationPeer.CreatePeerForElement(this);
                    peer?.RaiseAutomationEvent(AutomationEvents.SelectionItemPatternOnElementSelected);
                }
            }
        }


        // This should be held in memory, or animation will stop
        private CompositionPropertySet _props;

        private IAnimatedVisual _previous;
        private IAnimatedVisualSource2 _source;

        private IAnimatedVisual GetVisual(Compositor compositor, out IAnimatedVisualSource2 source, out CompositionPropertySet properties)
        {
            source = new Select();

            if (source == null)
            {
                properties = null;
                return null;
            }

            var visual = source.TryCreateAnimatedVisual(compositor, out _);
            if (visual == null)
            {
                properties = null;
                return null;
            }

            properties = compositor.CreatePropertySet();
            properties.InsertScalar("Progress", 1.0F);

            var progressAnimation = compositor.CreateExpressionAnimation("_.Progress");
            progressAnimation.SetReferenceParameter("_", properties);
            visual.RootVisual.Properties.InsertScalar("Progress", 1.0F);
            visual.RootVisual.Properties.StartAnimation("Progress", progressAnimation);

            return visual;
        }

        private void UpdateIcon(bool selected, bool animate)
        {
            if (_props != null && _previous != null)
            {
                if (animate)
                {
                    var linearEasing = _props.Compositor.CreateLinearEasingFunction();
                    var animation = _props.Compositor.CreateScalarKeyFrameAnimation();
                    animation.Duration = _previous.Duration;
                    animation.InsertKeyFrame(1, selected ? 1 : 0, linearEasing);

                    _props.StartAnimation("Progress", animation);
                }
                else
                {
                    _props.InsertScalar("Progress", selected ? 1.0F : 0.0F);
                }
            }
        }

        private bool IsAlbum => _message?.Content is MessageAlbum;

        private bool IsAlbumChild => _message?.Content is not MessageAlbum && _message.MediaAlbumId != 0;

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new MessageSelectorAutomationPeer(this);
        }

        #region Moved from ChatHistoryViewItem

        private Visual _hitTest;
        private Visual _visual;
        private Compositor _compositor;
        private ContainerVisual _container;
        private ContainerVisual _indicator;

        private bool _hasInitialLoadedEventFired;
        private WeakInteractionTrackerOwner _trackerOwner;
        private InteractionTracker _tracker;
        private VisualInteractionSource _interactionSource;
        private bool _interacting;

        private readonly bool _requiresArrange = !ApiInfo.IsWindows11;

        private bool _share;
        private bool _reply;

        private Grid RootGrid;

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (_container != null && _requiresArrange)
            {
                _container.Size = finalSize.ToVector2();
            }

            return base.ArrangeOverride(finalSize);
        }

        private void ConfigureInteractionTracker()
        {
            _interactionSource = VisualInteractionSource.Create(_hitTest);

            //Configure for x-direction panning
            _interactionSource.ManipulationRedirectionMode = VisualInteractionSourceRedirectionMode.CapableTouchpadOnly;
            _interactionSource.PositionXSourceMode = InteractionSourceMode.EnabledWithInertia;
            _interactionSource.PositionXChainingMode = InteractionChainingMode.Never;
            _interactionSource.IsPositionXRailsEnabled = true;

            _trackerOwner = new WeakInteractionTrackerOwner();

            //Create tracker and associate interaction source
            _tracker = InteractionTracker.CreateWithOwner(_compositor, _trackerOwner);
            _tracker.InteractionSources.Add(_interactionSource);

            _tracker.MaxPosition = new Vector3(_reply ? 72 : 0);
            _tracker.MinPosition = new Vector3(_share ? -72 : 0);

            _tracker.Properties.InsertBoolean("CanReply", _reply);
            _tracker.Properties.InsertBoolean("CanShare", _share);

            //ConfigureAnimations(_visual, null);
            ConfigureRestingPoints();

            if (_interacting)
            {
                _interacting = false;
                _visual.Properties.InsertVector3("Translation", Vector3.Zero);
            }
        }

        private void ConfigureRestingPoints()
        {
            var neutralX = InteractionTrackerInertiaRestingValue.Create(_compositor);
            neutralX.Condition = _compositor.CreateExpressionAnimation("true");
            neutralX.RestingValue = _compositor.CreateExpressionAnimation("0");

            _tracker.ConfigurePositionXInertiaModifiers(new InteractionTrackerInertiaModifier[] { neutralX });
        }

        private void ConfigureAnimations(Visual visual, Visual indicator)
        {
            // Create an animation that changes the offset of the photoVisual and shadowVisual based on the manipulation progress
            var offsetExp = _compositor.CreateExpressionAnimation("(tracker.Position.X > 0 && !tracker.CanReply) || (tracker.Position.X <= 0 && !tracker.CanShare) ? 0 : -tracker.Position.X");
            //var photoOffsetExp = _visual.Compositor.CreateExpressionAnimation("tracker.Position.X > 0 && !tracker.CanReply || tracker.Position.X <= 0 && !tracker.CanShare ? 0 : Max(-72, Min(72, -tracker.Position.X))");
            //var photoOffsetExp = _visual.Compositor.CreateExpressionAnimation("-tracker.Position.X");
            offsetExp.SetReferenceParameter("tracker", _tracker);
            visual.StartAnimation("Translation.X", offsetExp);
        }

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

        public async void PrepareForItemOverride(MessageViewModel message, bool canReply)
        {
            var properties = await message.ClientService.SendAsync(new GetMessageProperties(message.ChatId, message.Id)) as MessageProperties;
            if (properties == null)
            {
                return;
            }

            var share = SettingsService.Current.SwipeToShare && properties.CanBeForwarded;
            var reply = SettingsService.Current.SwipeToReply && (canReply || properties.CanBeRepliedInAnotherChat);

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

                try
                {
                    if (_visual != null && _visual.Offset.X != 0)
                    {
                        _visual.Offset = new Vector3();
                    }
                }
                catch
                {
                    // ???
                }
            }

            _share = share;
            _reply = reply;
        }

        private void OnValuesChanged(InteractionTracker sender, InteractionTrackerValuesChangedArgs args)
        {
            if (_indicator == null && (sender.Position.X > 0.0001f || sender.Position.X < -0.0001f) /*&& Math.Abs(e.Cumulative.Translation.X) >= 45*/)
            {
                var sprite = _compositor.CreateSpriteVisual();
                sprite.Size = new Vector2(30, 30);
                sprite.CenterPoint = new Vector3(15);

                var surface = LoadedImageSurface.StartLoadFromUri(new Uri("ms-appx:///Assets/Images/Reply.png"));
                void handler(LoadedImageSurface s, LoadedImageSourceLoadCompletedEventArgs args)
                {
                    s.LoadCompleted -= handler;
                    sprite.Brush = _compositor.CreateSurfaceBrush(s);
                }

                surface.LoadCompleted += handler;

                var ellipse = _compositor.CreateEllipseGeometry();
                ellipse.Radius = new Vector2(15);

                var ellipseShape = _compositor.CreateSpriteShape(ellipse);
                ellipseShape.FillBrush = _compositor.CreateColorBrush((Windows.UI.Color)Navigation.BootStrapper.Current.Resources["MessageServiceBackgroundColor"]);
                ellipseShape.Offset = new Vector2(15);

                var shape = _compositor.CreateShapeVisual();
                shape.Shapes.Add(ellipseShape);
                shape.Size = new Vector2(30, 30);

                _indicator = _compositor.CreateContainerVisual();
                _indicator.Children.InsertAtBottom(shape);
                _indicator.Children.InsertAtTop(sprite);
                _indicator.Size = new Vector2(30, 30);
                _indicator.CenterPoint = new Vector3(15);
                _indicator.Scale = new Vector3();

                _container.Children.InsertAtTop(_indicator);

                //ElementCompositionPreview.SetElementChildVisual(this, _indicator);
                //ElementCompositionPreview.SetElementChildVisual(this, _container);
            }

            var offset = (sender.Position.X > 0 && !_reply) || (sender.Position.X <= 0 && !_share) ? 0 : Math.Max(0, Math.Min(72, Math.Abs(sender.Position.X)));

            var abs = Math.Abs(offset);
            var percent = abs / 72f;

            var width = (float)ActualWidth;
            var height = (float)ActualHeight;

            if (_indicator != null)
            {
                _indicator.Offset = new Vector3(sender.Position.X > 0 ? width - percent * 60 : -30 + percent * 55, (height - 30) / 2, 0);
                _indicator.Scale = new Vector3(sender.Position.X > 0 ? 0.8f + percent * 0.2f : -(0.8f + percent * 0.2f), 0.8f + percent * 0.2f, 1);
                _indicator.Opacity = percent;
            }
        }

        private void OnInertiaStateEntered(InteractionTracker sender, InteractionTrackerInertiaStateEnteredArgs args)
        {
            if (Message != null)
            {
                if (sender.Position.X >= 72 && _reply)
                {
                    _owner.ViewModel.ReplyToMessage(Message);
                }
                else if (sender.Position.X <= -72 && _share)
                {
                    _owner.ViewModel.ForwardMessage(Message);
                }
            }
        }

        private void OnIdleStateEntered(InteractionTracker sender, InteractionTrackerIdleStateEnteredArgs args)
        {
            _interacting = false;

            if (IsDisconnected)
            {
                OnUnloaded(null, null);
            }
            else
            {
                ConfigureAnimations(_visual, null);
            }
        }

        private void OnInteractingStateEntered(InteractionTracker sender, InteractionTrackerInteractingStateEnteredArgs args)
        {
            _interacting = true;
            ConfigureAnimations(_visual, null);
        }

        #endregion

        public class MessageSelectorAutomationPeer : ToggleButtonAutomationPeer, ISelectionItemProvider
        {
            private readonly MessageSelector _owner;

            public MessageSelectorAutomationPeer(MessageSelector owner)
                : base(owner)
            {
                _owner = owner;
            }

            protected override string GetNameCore()
            {
                if (_owner.Content is MessageBubble bubble)
                {
                    return bubble.GetAutomationName() ?? base.GetNameCore();
                }
                else if (_owner.ContentTemplateRoot is MessageBubble child)
                {
                    return child.GetAutomationName() ?? base.GetNameCore();
                }
                else if (_owner.Message != null)
                {
                    return Automation.GetSummary(_owner.Message, true);
                }

                return base.GetNameCore();
            }

            protected override object GetPatternCore(PatternInterface patternInterface)
            {
                if (patternInterface == PatternInterface.SelectionItem)
                {
                    return this;
                }
                else if (patternInterface == PatternInterface.Toggle)
                {
                    return null;
                }

                return base.GetPatternCore(patternInterface);
            }

            protected override AutomationControlType GetAutomationControlTypeCore()
            {
                return AutomationControlType.ListItem;
            }

            protected override int GetPositionInSetCore()
            {
                if (_owner._owner != null)
                {
                    return 1 + _owner._owner.Items.IndexOf(_owner.Message);
                }

                return base.GetPositionInSetCore();
            }

            protected override int GetSizeOfSetCore()
            {
                if (_owner._owner != null)
                {
                    return _owner._owner.Items.Count;
                }

                return base.GetSizeOfSetCore();
            }

            public void AddToSelection()
            {
                _owner._owner.SelectedItems.Add(_owner.Message);
            }

            public void RemoveFromSelection()
            {
                _owner._owner.SelectedItems.Remove(_owner.Message);
            }

            public void Select()
            {
                _owner._owner.SelectedItems.Add(_owner.Message);
            }

            public bool IsSelected => _owner._isSelected;

            public IRawElementProviderSimple SelectionContainer
            {
                get
                {
                    if (_owner._owner != null)
                    {
                        var peer = FrameworkElementAutomationPeer.CreatePeerForElement(_owner._owner);
                        return ProviderFromPeer(peer);
                    }

                    return null;
                }
            }
        }
    }
}
