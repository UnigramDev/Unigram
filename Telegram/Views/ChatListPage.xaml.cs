//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using LinqToVisualTree;
using System;
using System.Linq;
using System.Numerics;
using Telegram.Controls;
using Telegram.ViewModels;
using Windows.UI.Composition;
using Windows.UI.Composition.Interactions;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Views
{
    public sealed partial class ChatListPage : ChatListListView, IInteractionTrackerOwner
    {
        public MainViewModel Main { get; set; }

        private bool _canGoNext;
        private bool _canGoPrev;

        public ChatListPage()
        {
            InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            AddHandler(PointerPressedEvent, new PointerEventHandler(OnPointerPressed), true);
        }

        private SpriteVisual _hitTest;
        private ContainerVisual _container;
        private Visual _visual;
        private ContainerVisual _indicator;

        private bool _hasInitialLoadedEventFired;
        private InteractionTracker _tracker;
        private VisualInteractionSource _interactionSource;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!_hasInitialLoadedEventFired)
            {
                var root = VisualTreeHelper.GetChild(this, 0) as UIElement;
                if (root == null)
                {
                    return;
                }

                _visual = ElementCompositionPreview.GetElementVisual(root);

                _hitTest = _visual.Compositor.CreateSpriteVisual();
                _hitTest.Brush = _visual.Compositor.CreateColorBrush(Windows.UI.Colors.Transparent);
                _hitTest.RelativeSizeAdjustment = Vector2.One;

                _container = _visual.Compositor.CreateContainerVisual();
                _container.Children.InsertAtBottom(_hitTest);
                _container.RelativeSizeAdjustment = Vector2.One;

                ElementCompositionPreview.SetElementChildVisual(this, _container);

                ConfigureInteractionTracker();
            }

            _hasInitialLoadedEventFired = true;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_hasInitialLoadedEventFired)
            {
                _tracker.Dispose();
                _tracker = null;

                _interactionSource.Dispose();
                _interactionSource = null;
            }

            _hasInitialLoadedEventFired = false;
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

            _tracker.Properties.InsertBoolean("CanGoNext", _canGoNext);
            _tracker.Properties.InsertBoolean("CanGoPrev", _canGoPrev);

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

        private void ConfigureAnimations(Visual visual)
        {
            var viewModel = Main;
            if (viewModel != null)
            {
                var already = viewModel.SelectedFolder;
                if (already == null)
                {
                    return;
                }

                var index = viewModel.Folders.IndexOf(already);

                _canGoNext = index < viewModel.Folders.Count - 1;
                _canGoPrev = index > 0;

                _tracker.Properties.InsertBoolean("CanGoNext", _canGoNext);
                _tracker.Properties.InsertBoolean("CanGoPrev", _canGoPrev);
                _tracker.MaxPosition = new Vector3(_canGoNext ? 72 : 0);
                _tracker.MinPosition = new Vector3(_canGoPrev ? -72 : 0);
            }

            var offsetExp = _visual.Compositor.CreateExpressionAnimation("(tracker.Position.X > 0 && !tracker.CanGoNext) || (tracker.Position.X <= 0 && !tracker.CanGoPrev) ? 0 : -tracker.Position.X");
            offsetExp.SetReferenceParameter("tracker", _tracker);
            visual.StartAnimation("Offset.X", offsetExp);
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
            }

            var offset = (_tracker.Position.X > 0 && !_canGoNext) || (_tracker.Position.X <= 0 && !_canGoPrev) ? 0 : Math.Max(0, Math.Min(72, Math.Abs(_tracker.Position.X)));

            var abs = Math.Abs(offset);
            var percent = abs / 72f;

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
            if (position.X >= 72 && _canGoNext || position.X <= -72 && _canGoPrev)
            {
                var main = this.Ancestors<MainPage>().FirstOrDefault();
                if (main == null)
                {
                    return;
                }

                var offset = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
                var opacity = Window.Current.Compositor.CreateScalarKeyFrameAnimation();

                if (position.X >= 72 && _canGoNext)
                {
                    offset.InsertKeyFrame(0, new Vector3(-72, 0, 0));
                    offset.InsertKeyFrame(1, new Vector3(0));

                    main.ScrollFolder(+1, true);
                }
                else if (position.X <= -72 && _canGoPrev)
                {
                    offset.InsertKeyFrame(0, new Vector3(72, 0, 0));
                    offset.InsertKeyFrame(1, new Vector3(0));

                    main.ScrollFolder(-1, true);
                }

                offset.Duration = TimeSpan.FromMilliseconds(250);
                sender.TryUpdatePositionWithAnimation(offset);

                opacity.InsertKeyFrame(0, 0);
                opacity.InsertKeyFrame(1, 1);
                opacity.Duration = TimeSpan.FromMilliseconds(250);

                _visual.StartAnimation("Opacity", opacity);
            }
        }

        public void IdleStateEntered(InteractionTracker sender, InteractionTrackerIdleStateEnteredArgs args)
        {
            ConfigureAnimations(_visual);
        }

        public void InteractingStateEntered(InteractionTracker sender, InteractionTrackerInteractingStateEnteredArgs args)
        {
            ConfigureAnimations(_visual);
        }

        public void RequestIgnored(InteractionTracker sender, InteractionTrackerRequestIgnoredArgs args)
        {

        }

        public void CustomAnimationStateEntered(InteractionTracker sender, InteractionTrackerCustomAnimationStateEnteredArgs args)
        {

        }
    }
}
