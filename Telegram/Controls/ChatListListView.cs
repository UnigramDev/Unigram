//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Numerics;
using Telegram.Common;
using Telegram.Composition;
using Telegram.Controls.Cells;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Composition.Interactions;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    public class ChatListSwipedEventArgs : EventArgs
    {
        public CarouselDirection Direction { get; }

        public ChatListSwipedEventArgs(CarouselDirection direction)
        {
            Direction = direction;
        }
    }

    public class ChatListListView : TopNavView, IInteractionTrackerOwner
    {
        public ChatListViewModel ViewModel => DataContext as ChatListViewModel;

        public MasterDetailState _viewState;

        private readonly Dictionary<long, SelectorItem> _itemToSelector = new();

        private ScrollViewer ScrollViewer;
        private Border Ghost;

        public ChatListListView()
        {
            DefaultStyleKey = typeof(ChatListListView);

            Connected += OnLoaded;
            Disconnected += OnUnloaded;
            ContainerContentChanging += OnContainerContentChanging;

            AddHandler(PointerPressedEvent, new PointerEventHandler(OnPointerPressed), true);
            RegisterPropertyChangedCallback(SelectionModeProperty, OnSelectionModeChanged);
        }

        protected override void OnApplyTemplate()
        {
            ScrollViewer = GetTemplateChild(nameof(ScrollViewer)) as ScrollViewer;
            Ghost = GetTemplateChild(nameof(Ghost)) as Border;

            base.OnApplyTemplate();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _itemToSelector.Clear();

            if (_hasInitialLoadedEventFired)
            {
                _hasInitialLoadedEventFired = false;

                _interactionSource.Dispose();
                _interactionSource = null;

                _tracker.Dispose();
                _tracker = null;
            }
        }

        public bool TryGetChatAndCell(long chatId, out Chat chat, out ChatCell cell)
        {
            if (_itemToSelector.TryGetValue(chatId, out SelectorItem container))
            {
                chat = container.Tag as Chat;
                cell = container.ContentTemplateRoot as ChatCell;
                return chat != null && cell != null;
            }

            chat = null;
            cell = null;
            return false;
        }

        public bool TryGetContainer(long chatId, out SelectorItem container)
        {
            return _itemToSelector.TryGetValue(chatId, out container);
        }

        public bool TryGetCell(Chat chat, out ChatCell cell)
        {
            if (_itemToSelector.TryGetValue(chat.Id, out SelectorItem container))
            {
                cell = container.ContentTemplateRoot as ChatCell;
                return cell != null;
            }

            cell = null;
            return false;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.Item is not Chat chat)
            {
                return;
            }

            if (args.InRecycleQueue)
            {
                _itemToSelector.Remove(chat.Id);
                return;
            }

            _itemToSelector[chat.Id] = args.ItemContainer;
            args.Handled = true;

            if (args.Phase == 0)
            {
                args.ItemContainer.Tag = args.Item;
                VisualStateManager.GoToState(args.ItemContainer, "DataPlaceholder", false);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(OnContainerContentChanging);
                args.ItemContainer.ContentTemplateRoot.Opacity = 0;
                return;
            }

            if (args.ItemContainer.ContentTemplateRoot is ChatCell content)
            {
                content.UpdateViewState(chat, _viewState == MasterDetailState.Compact, false);
                content.UpdateChat(ViewModel.ClientService, chat, ViewModel.Items.ChatList);
                content.Opacity = 1;
            }

            VisualStateManager.GoToState(args.ItemContainer, "DataAvailable", false);
        }

        private void OnSelectionModeChanged(DependencyObject sender, DependencyProperty dp)
        {
            UpdateVisibleChats();
        }

        public void UpdateViewState(MasterDetailState state)
        {
            _viewState = state;
            UpdateVisibleChats();
        }

        public void UpdateVisibleChats()
        {
            // TODO: supposedly, _itemToSelector should only contain cached items
            foreach (var item in _itemToSelector)
            {
                if (item.Value.ContentTemplateRoot is ChatCell chatView && ViewModel.ClientService.TryGetChat(item.Key, out Chat chat))
                {
                    chatView.UpdateViewState(chat, _viewState == MasterDetailState.Compact, true);
                }
            }
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new ChatListListViewItem(this);
        }

        private bool _changingView;
        private bool _fromAnimation;

        public async void ChangeView(CarouselDirection direction, Action continuation)
        {
            void Continue(bool restore)
            {
                if (restore)
                {
                    _tracker.TryUpdatePosition(Vector3.Zero);
                }

                if (continuation != null)
                {
                    continuation();
                }
                else
                {
                    Swiped?.Invoke(this, new ChatListSwipedEventArgs(direction));
                }
            }

            if (_changingView || !PowerSavingPolicy.AreSmoothTransitionsEnabled)
            {
                Continue(true);
                return;
            }

            _changingView = true;

            var child = VisualTreeHelper.GetChild(ScrollViewer, 0) as UIElement;
            var visual = Window.Current.Compositor.CreateRedirectBrush(child, Vector2.Zero, child.ActualSize, true);

            await VisualUtilities.WaitForCompositionRenderedAsync();

            Continue(false);
            ConfigureAnimations(false);

            var position = ActualSize.X * (ActualSize.X / (ActualSize.X - 72));

            var w = continuation != null ? position : ActualSize.X;
            var x = direction == CarouselDirection.Previous ? w : -w;

            var translate = _tracker.Compositor.CreateVector3KeyFrameAnimation();
            translate.InsertKeyFrame(0, new Vector3(x, 0, 0));
            translate.InsertKeyFrame(1, new Vector3(0));

            _tracker.Properties.InsertBoolean("FromAnimation", _fromAnimation = true);
            _tracker.TryUpdatePositionWithAnimation(translate);

            _changingView = false;
            _redirect.Brush = visual;
        }

        #region Swipe

        public bool CanGoNext { get; set; }
        public bool CanGoPrev { get; set; }

        public event EventHandler<ChatListSwipedEventArgs> Swiped;

        private SpriteVisual _hitTest;
        private ContainerVisual _container;
        private Visual _visual;
        private SpriteVisual _redirect;
        private ContainerVisual _indicator;

        private bool _hasInitialLoadedEventFired;
        private InteractionTracker _tracker;
        private VisualInteractionSource _interactionSource;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!_hasInitialLoadedEventFired)
            {
                _hasInitialLoadedEventFired = true;

                _visual = ElementCompositionPreview.GetElementVisual(ScrollViewer);

                _redirect = _visual.Compositor.CreateSpriteVisual();
                _redirect.RelativeSizeAdjustment = Vector2.One;

                _hitTest = _visual.Compositor.CreateSpriteVisual();
                _hitTest.Brush = _visual.Compositor.CreateColorBrush(Colors.Transparent);
                _hitTest.RelativeSizeAdjustment = Vector2.One;

                _container = _visual.Compositor.CreateContainerVisual();
                _container.Children.InsertAtBottom(_hitTest);
                _container.RelativeSizeAdjustment = Vector2.One;

                ElementCompositionPreview.SetElementChildVisual(Ghost, _redirect);
                ElementCompositionPreview.SetElementChildVisual(this, _container);
                ConfigureInteractionTracker();
            }
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

            _tracker.MaxPosition = new Vector3(72);
            _tracker.MinPosition = new Vector3(-72);

            _tracker.Properties.InsertBoolean("FromAnimation", false);
            _tracker.Properties.InsertBoolean("CanGoNext", CanGoNext);
            _tracker.Properties.InsertBoolean("CanGoPrev", CanGoPrev);

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

        private void ConfigureAnimations(bool interacting)
        {
            if (interacting)
            {
                _redirect.Brush = _tracker.Compositor.CreateColorBrush(Colors.Transparent);
                _tracker.Properties.InsertBoolean("FromAnimation", _fromAnimation = false);
            }

            _tracker.Properties.InsertBoolean("CanGoNext", CanGoNext);
            _tracker.Properties.InsertBoolean("CanGoPrev", CanGoPrev);
            _tracker.MaxPosition = new Vector3(CanGoNext ? 72 : 0);
            _tracker.MinPosition = new Vector3(CanGoPrev ? -72 : 0);

            // This should be enough: tracker.FromAnimation ? -tracker.Position.X : Clamp(-tracker.Position.X, -72, 72)
            var offsetExp = _visual.Compositor.CreateExpressionAnimation("tracker.FromAnimation || Abs(tracker.Position.X) >= this.Target.Size.X ? -tracker.Position.X : Clamp(-tracker.Position.X, -72, 72)");
            offsetExp.SetReferenceParameter("tracker", _tracker);

            var offsetExp2 = _visual.Compositor.CreateExpressionAnimation("tracker.Position.X < 0 ? Min(-tracker.Position.X / (source.Size.X) * (source.Size.X - 72), source.Size.X) - source.Size.X : tracker.Position.X >= 0 ? Min(-tracker.Position.X / (source.Size.X) * (source.Size.X - 72), source.Size.X) + source.Size.X : 0");
            offsetExp2.SetReferenceParameter("tracker", _tracker);
            offsetExp2.SetReferenceParameter("source", _visual);

            _visual.StartAnimation("Offset.X", offsetExp);
            _redirect.StartAnimation("Offset.X", offsetExp2);
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                try
                {
                    _interactionSource.TryRedirectForManipulation(e.GetCurrentPoint(this));
                }
                catch { }
            }
        }

        public void ValuesChanged(InteractionTracker sender, InteractionTrackerValuesChangedArgs args)
        {
            if (_indicator == null && (_tracker.Position.X > 0.0001f || _tracker.Position.X < -0.0001f) /*&& Math.Abs(e.Cumulative.Translation.X) >= 45*/)
            {
                var sprite = _visual.Compositor.CreateSpriteVisual();
                sprite.Size = new Vector2(30, 30);
                sprite.CenterPoint = new Vector3(15);

                var surface = LoadedImageSurface.StartLoadFromUri(new Uri("ms-appx:///Assets/Images/ArrowLeft.png"));
                void handler(LoadedImageSurface s, LoadedImageSourceLoadCompletedEventArgs args)
                {
                    s.LoadCompleted -= handler;
                    sprite.Brush = _visual.Compositor.CreateSurfaceBrush(s);
                }

                surface.LoadCompleted += handler;

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
            }

            var offset = (_tracker.Position.X > 0 && !CanGoNext) || (_tracker.Position.X <= 0 && !CanGoPrev) ? 0 : Math.Clamp(Math.Abs(_tracker.Position.X), 0, 72);

            var abs = Math.Abs(offset);
            var percent = _fromAnimation ? 0 : abs / 72f;

            var width = ActualSize.X;
            var height = ActualSize.Y;

            if (_indicator != null)
            {
                _indicator.Offset = new Vector3(_tracker.Position.X > 0 ? width - percent * 60 : -30 + percent * 55, (height - 30) / 2, 0);
                _indicator.Scale = new Vector3(_tracker.Position.X > 0 ? 0.8f + percent * 0.2f : -(0.8f + percent * 0.2f), 0.8f + percent * 0.2f, 1);
                _indicator.Opacity = percent;
            }
        }

        public void InertiaStateEntered(InteractionTracker sender, InteractionTrackerInertiaStateEnteredArgs args)
        {
            var position = _tracker.Position;
            if (position.X >= 72 && CanGoNext || position.X <= -72 && CanGoPrev)
            {
                sender.TryUpdatePosition(sender.Position);

                var direction = position.X <= -72 && CanGoPrev
                    ? CarouselDirection.Previous
                    : CarouselDirection.Next;

                ChangeView(direction, null);
            }
        }

        public void IdleStateEntered(InteractionTracker sender, InteractionTrackerIdleStateEnteredArgs args)
        {
            ConfigureAnimations(false);
        }

        public void InteractingStateEntered(InteractionTracker sender, InteractionTrackerInteractingStateEnteredArgs args)
        {
            ConfigureAnimations(true);
        }

        public void RequestIgnored(InteractionTracker sender, InteractionTrackerRequestIgnoredArgs args)
        {

        }

        public void CustomAnimationStateEntered(InteractionTracker sender, InteractionTrackerCustomAnimationStateEnteredArgs args)
        {
            _tracker.Properties.InsertBoolean("FromAnimation", _fromAnimation = true);
        }

        #endregion
    }

    public class ChatListListViewItem : TopNavViewItem
    {
        private readonly ChatListListView _list;

        private readonly bool _multi;
        private bool _selected;

        public ChatListListViewItem()
        {
            _multi = true;
            DefaultStyleKey = typeof(ChatListListViewItem);
        }

        public bool IsSingle => !_multi;

        public void UpdateState(bool selected)
        {
            if (_selected == selected)
            {
                return;
            }

            if (ContentTemplateRoot is IMultipleElement test)
            {
                _selected = selected;
                test.UpdateState(selected, true, _list.SelectionMode == ListViewSelectionMode.Multiple);
            }
        }


        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ChatListListViewItemAutomationPeer(this);
        }

        public ChatListListViewItem(ChatListListView list)
        {
            DefaultStyleKey = typeof(ChatListListViewItem);

            _multi = true;
            _list = list;
            //RegisterPropertyChangedCallback(IsSelectedProperty, OnSelectedChanged);
        }

        private void OnSelectedChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (ContentTemplateRoot is ChatCell content)
            {
                content?.UpdateViewState(_list.ItemFromContainer(this) as Chat, _list._viewState == MasterDetailState.Compact, false);
            }
        }
    }

    public class ChatListVisualStateManager : VisualStateManager
    {
        private bool _multi;

        protected override bool GoToStateCore(Control control, FrameworkElement templateRoot, string stateName, VisualStateGroup group, VisualState state, bool useTransitions)
        {
            var selector = control as ChatListListViewItem;
            if (selector == null)
            {
                return false;
            }

            if (group.Name == "MultiSelectStates")
            {
                _multi = stateName == "MultiSelectEnabled";
                selector.UpdateState((_multi || selector.IsSingle) && selector.IsSelected);
            }
            else if ((_multi || selector.IsSingle) && stateName.EndsWith("Selected"))
            {
                stateName = stateName.Replace("Selected", string.Empty);

                if (string.IsNullOrEmpty(stateName))
                {
                    stateName = "Normal";
                }
            }

            return base.GoToStateCore(control, templateRoot, stateName, group, state, useTransitions);
        }
    }

    public class ChatListListViewItemAutomationPeer : ListViewItemAutomationPeer
    {
        private readonly ChatListListViewItem _owner;

        public ChatListListViewItemAutomationPeer(ChatListListViewItem owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override string GetNameCore()
        {
            var name = _owner.ContentTemplateRoot switch
            {
                ChatCell chat => chat.GetAutomationName(),
                ForumTopicCell topic => topic.GetAutomationName(),
                _ => null
            };

            return name ?? base.GetNameCore();
        }
    }
}
