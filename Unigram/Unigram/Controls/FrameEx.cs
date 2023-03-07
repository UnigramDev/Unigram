//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.Interactions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Numerics;
using Unigram.Navigation;
using Windows.UI;

namespace Unigram.Controls
{
    public class FrameEx : Frame, IInteractionTrackerOwner
    {
        private SpriteVisual _hitTest;
        private ContainerVisual _container;
        private Visual _visual;
        private ContainerVisual _indicator;

        private bool _hasInitialLoadedEventFired;
        private InteractionTracker _tracker;
        private VisualInteractionSource _interactionSource;

        public FrameEx()
        {
            Loaded += OnLoaded;
            Navigated += OnNavigated;

            AddHandler(PointerPressedEvent, new PointerEventHandler(OnPointerPressed), true);
            RegisterPropertyChangedCallback(CanGoBackProperty, OnCanGoBackChanged);
        }

        private void OnNavigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            if (e.Content is UIElement element)
            {
                _visual = ElementCompositionPreview.GetElementVisual(element);
                ConfigureAnimations(_visual);
            }
        }

        private void OnCanGoBackChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (_tracker != null)
            {
                _tracker.Properties.InsertBoolean("CanGoBack", CanGoBack);

                _tracker.MaxPosition = new Vector3(0);
                _tracker.MinPosition = new Vector3(CanGoBack ? -72 : 0);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!_hasInitialLoadedEventFired)
            {
                var compositor = BootStrapper.Current.Compositor;

                if (Content is UIElement element)
                {
                    _visual = ElementCompositionPreview.GetElementVisual(element);
                }

                _hitTest = compositor.CreateSpriteVisual();
                _hitTest.Brush = compositor.CreateColorBrush(Microsoft.UI.Colors.Transparent);
                _hitTest.RelativeSizeAdjustment = Vector2.One;

                _container = compositor.CreateContainerVisual();
                _container.Children.InsertAtBottom(_hitTest);
                _container.RelativeSizeAdjustment = Vector2.One;

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
            _tracker = InteractionTracker.CreateWithOwner(_hitTest.Compositor, this);
            _tracker.InteractionSources.Add(_interactionSource);

            _tracker.Properties.InsertBoolean("CanGoBack", CanGoBack);

            _tracker.MaxPosition = new Vector3(0);
            _tracker.MinPosition = new Vector3(CanGoBack ? -72 : 0);

            ConfigureAnimations(_visual);
            ConfigureRestingPoints();
        }

        private void ConfigureRestingPoints()
        {
            var neutralX = InteractionTrackerInertiaRestingValue.Create(_hitTest.Compositor);
            neutralX.Condition = _hitTest.Compositor.CreateExpressionAnimation("true");
            neutralX.RestingValue = _hitTest.Compositor.CreateExpressionAnimation("0");

            _tracker.ConfigurePositionXInertiaModifiers(new InteractionTrackerInertiaModifier[] { neutralX });
        }

        private void ConfigureAnimations(Visual visual)
        {
            if (visual == null || _tracker == null)
            {
                return;
            }

            _tracker.Properties.InsertBoolean("CanGoBack", CanGoBack);

            _tracker.MaxPosition = new Vector3(0);
            _tracker.MinPosition = new Vector3(CanGoBack ? -72 : 0);

            var offsetExp = visual.Compositor.CreateExpressionAnimation("tracker.Position.X <= 0 && !tracker.CanGoBack ? 0 : -tracker.Position.X");
            offsetExp.SetReferenceParameter("tracker", _tracker);
            visual.StartAnimation("Offset.X", offsetExp);
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Mouse)
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
                var sprite = _hitTest.Compositor.CreateSpriteVisual();
                sprite.Size = new Vector2(30, 30);
                sprite.CenterPoint = new Vector3(15);

                var surface = LoadedImageSurface.StartLoadFromUri(new Uri("ms-appx:///Assets/Images/Reply.png"));
                surface.LoadCompleted += (s, e) =>
                {
                    sprite.Brush = _hitTest.Compositor.CreateSurfaceBrush(s);
                };

                var ellipse = _hitTest.Compositor.CreateEllipseGeometry();
                ellipse.Radius = new Vector2(15);

                var ellipseShape = _hitTest.Compositor.CreateSpriteShape(ellipse);
                ellipseShape.FillBrush = _hitTest.Compositor.CreateColorBrush((Color)Navigation.BootStrapper.Current.Resources["MessageServiceBackgroundColor"]);
                ellipseShape.Offset = new Vector2(15);

                var shape = _hitTest.Compositor.CreateShapeVisual();
                shape.Shapes.Add(ellipseShape);
                shape.Size = new Vector2(30, 30);

                _indicator = _hitTest.Compositor.CreateContainerVisual();
                _indicator.Children.InsertAtBottom(shape);
                _indicator.Children.InsertAtTop(sprite);
                _indicator.Size = new Vector2(30, 30);
                _indicator.CenterPoint = new Vector3(15);
                _indicator.Scale = new Vector3();

                _container.Children.InsertAtTop(_indicator);
            }

            var offset = _tracker.Position.X <= 0 && !CanGoBack ? 0 : Math.Max(0, Math.Min(72, Math.Abs(_tracker.Position.X)));

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
            if (position.X <= -72 && CanGoBack)
            {
                var offset = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
                var opacity = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();

                if (position.X <= -72 && CanGoBack)
                {
                    offset.InsertKeyFrame(0, new Vector3(72, 0, 0));
                    offset.InsertKeyFrame(1, new Vector3(0));

                    GoBack();
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
