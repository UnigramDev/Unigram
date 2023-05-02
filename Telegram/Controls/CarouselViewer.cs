//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Numerics;
using Telegram.Common;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Composition.Interactions;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls
{
    public enum CarouselDirection
    {
        None = 0,
        Previous = -1,
        Next = 1
    }

    public class CarouselViewChangingEventArgs : EventArgs
    {
        public CarouselViewChangingEventArgs(CarouselDirection direction)
        {
            Direction = direction;
        }

        public CarouselDirection Direction { get; }
    }

    public class CarouselViewChangedEventArgs : EventArgs
    {
        public CarouselViewChangedEventArgs(CarouselDirection direction)
        {
            Direction = direction;
        }

        public CarouselDirection Direction { get; }
    }

    public delegate void CarouselViewChangedEventHandler(object sender, CarouselViewChangedEventArgs e);
    public delegate void CarouselViewChangingEventHandler(object sender, CarouselViewChangingEventArgs e);

    public class CarouselViewer : Grid, IInteractionTrackerOwner
    {
        private bool _requiresArrange;
        private long _scrolling;

        public CarouselViewer()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            AddHandler(PointerWheelChangedEvent, new PointerEventHandler(OnPointerWheelChanged), true);
            AddHandler(PointerPressedEvent, new PointerEventHandler(OnPointerPressed), true);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!_hasInitialLoadedEventFired)
            {
                _hitTest = Window.Current.Compositor.CreateSpriteVisual();
                _hitTest.Brush = Window.Current.Compositor.CreateColorBrush(Windows.UI.Colors.Transparent);

                if (ApiInfo.IsWindows11)
                {
                    _requiresArrange = false;
                    _hitTest.RelativeSizeAdjustment = Vector2.One;
                }
                else
                {
                    _requiresArrange = true;
                    _hitTest.Size = ActualSize;
                }

                ElementCompositionPreview.SetElementChildVisual(this, _hitTest);
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

        private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            // TODO: I don't understand how to use VisualInteractionSource
            // for vertical pointer wheel + horizontal position source

            if (_interactionSource?.PositionXSourceMode != InteractionSourceMode.EnabledWithInertia || IsScrolling)
            {
                return;
            }

            var ctrl = Window.Current.CoreWindow.IsKeyDown(Windows.System.VirtualKey.Control);

            var point = e.GetCurrentPoint(this);
            if (point.Properties.IsHorizontalMouseWheel || ctrl)
            {
                return;
            }

            var direction = point.Properties.MouseWheelDelta > 0
                ? CarouselDirection.Previous
                : CarouselDirection.Next;

            ViewChanging?.Invoke(this, new CarouselViewChangingEventArgs(direction));
            ChangeView(direction);

            e.Handled = true;
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_interactionSource?.PositionXSourceMode != InteractionSourceMode.EnabledWithInertia)
            {
                return;
            }

            if (e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                try
                {
                    _interactionSource.TryRedirectForManipulation(e.GetCurrentPoint(this));
                    e.Handled = true;
                }
                catch
                {
                    // We don't care
                }
            }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            Telegram.App.Track();

            ConfigureElements();
            ConfigureAnimations(_restingValue);

            if (_hitTest != null && _requiresArrange)
            {
                _hitTest.Size = finalSize.ToVector2();
            }

            return base.ArrangeOverride(finalSize);
        }

        private void ConfigureElements()
        {
            if (_hasConfiguredElements)
            {
                return;
            }

            for (int i = 0; i < Math.Min(_elements.Length, Children.Count); i++)
            {
                var element = Children[i] as FrameworkElement;
                element.SizeChanged += OnSizeChanged;

                _elements[i] = element;
                _visuals[i] = ElementCompositionPreview.GetElementVisual(_elements[i]);
                ElementCompositionPreview.SetIsTranslationEnabled(_elements[i], true);
            }

            _hasConfiguredElements = true;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            Telegram.App.Track();

            if (e.NewSize.Width != e.PreviousSize.Width)
            {
                ConfigureAnimations(_restingValue);
            }
        }

        private float GetElementPosition(Vector2 size, int column)
        {
            var halfWidth = ActualSize.X / 2;

            return column switch
            {
                0 => MathF.Floor(-halfWidth - size.X / 2),
                2 => MathF.Floor(halfWidth + size.X / 2),
                _ => 0
            };
        }

        public bool HasPrevious
        {
            get => _canGoPrev;
            set => _canGoPrev = value;
        }

        public bool HasNext
        {
            get => _canGoNext;
            set => _canGoNext = value;
        }

        public bool IsManipulationEnabled
        {
            get => _interactionSource?.PositionXSourceMode == InteractionSourceMode.EnabledWithInertia;
            set
            {
                if (_interactionSource != null)
                {
                    _interactionSource.PositionXSourceMode = value
                        ? InteractionSourceMode.EnabledWithInertia
                        : InteractionSourceMode.Disabled;
                }
            }
        }

        public bool IsScrolling => Environment.TickCount - _scrolling < 100;

        public FrameworkElement CurrentElement => _elements[1];

        public FrameworkElement GetElement(CarouselDirection direction)
        {
            var index = (int)direction;
            return _elements[1 + index];
        }

        public void PrepareElements<T>(CarouselDirection direction, out T previous, out T target, out T next) where T : FrameworkElement
        {
            ConfigureElements();

            var index = (int)direction;
            if (index != 0)
            {
                _elements.Shiftino(index);
                _visuals.Shiftino(index);
            }

            previous = _elements[0] as T;
            target = _elements[1] as T;
            next = _elements[2] as T;

            Canvas.SetZIndex(target, 1);
            Canvas.SetZIndex(previous, 0);
            Canvas.SetZIndex(next, 0);
        }

        public void ChangeView(CarouselDirection direction)
        {
            _scrolling = Environment.TickCount;

            var position = direction == CarouselDirection.Previous
                ? _tracker.MinPosition
                : _tracker.MaxPosition;

            var anim = _tracker.Compositor.CreateVector3KeyFrameAnimation();
            anim.InsertKeyFrame(0, new Vector3(_restingValue, 0, 0));
            anim.InsertKeyFrame(1, position);

            _viewChanged = direction;
            _tracker.TryUpdatePositionWithAnimation(anim);

            ConfigureAnimations(position.X);
        }

        public event EventHandler<CarouselViewChangingEventArgs> ViewChanging;
        public event EventHandler<CarouselViewChangedEventArgs> ViewChanged;

        #region IInteractionTrackerOwner

        private SpriteVisual _hitTest;

        private readonly FrameworkElement[] _elements = new FrameworkElement[3];
        private readonly Visual[] _visuals = new Visual[3];

        private bool _hasInitialLoadedEventFired;
        private bool _hasConfiguredElements;

        private InteractionTracker _tracker;
        private VisualInteractionSource _interactionSource;

        private float _restingValue;

        private bool _canGoPrev;
        private bool _canGoNext;

        private CarouselDirection _viewChanged;

        private void ConfigureInteractionTracker()
        {
            _interactionSource = VisualInteractionSource.Create(_hitTest);

            //Configure for x-direction panning
            _interactionSource.ManipulationRedirectionMode = VisualInteractionSourceRedirectionMode.CapableTouchpadAndPointerWheel;
            _interactionSource.PositionXSourceMode = InteractionSourceMode.EnabledWithInertia;
            _interactionSource.PositionXChainingMode = InteractionChainingMode.Never;
            _interactionSource.IsPositionXRailsEnabled = true;

            //Create tracker and associate interaction source
            _tracker = InteractionTracker.CreateWithOwner(Window.Current.Compositor, this);
            _tracker.InteractionSources.Add(_interactionSource);

            _tracker.Properties.InsertScalar("RestingValue", _restingValue);
            _tracker.Properties.InsertBoolean("CanGoNext", _canGoNext);
            _tracker.Properties.InsertBoolean("CanGoPrev", _canGoPrev);

            ConfigureAnimations();
            ConfigureRestingPoints();
        }

        private void ConfigureRestingPoints()
        {
            var snapPrevModifier = InteractionTrackerInertiaRestingValue.Create(_tracker.Compositor);
            var snapNextModifier = InteractionTrackerInertiaRestingValue.Create(_tracker.Compositor);

            // Is NaturalRestingPosition less than the halfway point between Snap Points?
            snapPrevModifier.Condition = _tracker.Compositor.CreateExpressionAnimation(
                "this.Target.NaturalRestingPosition.X <= (this.Target.RestingValue - " +
                "(this.Target.RestingValue - this.Target.MaxPosition.X) * 0.25)");
            // Is NaturalRestingPosition greater than the halfway point between Snap Points?
            snapNextModifier.Condition = _tracker.Compositor.CreateExpressionAnimation(
                "this.Target.NaturalRestingPosition.X > (this.Target.RestingValue - " +
                "(this.Target.RestingValue - this.Target.MaxPosition.X) * 0.25)");

            snapPrevModifier.RestingValue = _tracker.Compositor.CreateExpressionAnimation(
                "this.Target.NaturalRestingPosition.X < (this.Target.RestingValue - " +
                "(this.Target.RestingValue - this.Target.MinPosition.X) * 0.25) " +
                "? this.Target.MinPosition.X : this.Target.RestingValue");
            snapNextModifier.RestingValue = _tracker.Compositor.CreateExpressionAnimation("this.Target.MaxPosition.X");

            _tracker.ConfigurePositionXInertiaModifiers(new[] { snapPrevModifier, snapNextModifier });
        }

        private void ConfigureAnimations(float restingValue = 0, int direction = 0)
        {
            if (_tracker == null)
            {
                return;
            }

            _restingValue = restingValue;

            (Visual Visual, Vector2 Size)
                visual2 = (_visuals[0], _elements[0].ActualSize);

            (Visual Visual, Vector2 Size)
                visual0 = (_visuals[1], _elements[1].ActualSize);

            (Visual Visual, Vector2 Size)
                visual1 = (_visuals[2], _elements[2].ActualSize);

            var current2 = GetElementPosition(visual2.Size, 0);
            var future20 = GetElementPosition(visual2.Size, 1);

            var future02 = GetElementPosition(visual0.Size, 0);
            var future01 = GetElementPosition(visual0.Size, 2);

            var current1 = GetElementPosition(visual1.Size, 2);
            var future10 = GetElementPosition(visual1.Size, 1);

            _tracker.Properties.InsertScalar("RestingValue", _restingValue);
            _tracker.Properties.InsertBoolean("CanGoNext", _canGoNext);
            _tracker.Properties.InsertBoolean("CanGoPrev", _canGoPrev);

            _tracker.MaxPosition = new Vector3(restingValue + (_canGoNext ? ActualSize.X : 0), 0, 0);
            _tracker.MinPosition = new Vector3(restingValue - (_canGoPrev ? ActualSize.X : 0), 0, 0);

            var progress = _tracker.Compositor.CreateExpressionAnimation(
                "tracker.Position.X < restingValue " +
                "? (1 - (tracker.Position.X - tracker.MinPosition.X) / (restingValue - tracker.MinPosition.X)) * -1 " +
                ": tracker.Position.X > restingValue " +
                "? (1 - (tracker.Position.X - tracker.MaxPosition.X) / (restingValue - tracker.MaxPosition.X)) : 0");
            progress.SetReferenceParameter("tracker", _tracker);
            progress.SetScalarParameter("restingValue", restingValue);

            var properties = _tracker.Compositor.CreatePropertySet();
            properties.InsertScalar("Progress", 0);
            properties.StartAnimation("Progress", progress);

            var offset2 = _tracker.Compositor.CreateExpressionAnimation("Floor(value + (_.Progress < 0 ? _.Progress * (maxValue - value) * -1 : 0)) - 2");
            offset2.SetReferenceParameter("_", properties);
            offset2.SetScalarParameter("value", current2);
            offset2.SetScalarParameter("maxValue", future20);

            var offset0 = _tracker.Compositor.CreateExpressionAnimation("_.Progress > 0 " +
                "? _.Progress * maxValue * -1" +
                ": _.Progress * minValue");
            offset0.SetReferenceParameter("_", properties);
            offset0.SetScalarParameter("minValue", future02);
            offset0.SetScalarParameter("maxValue", future01);

            var offset1 = _tracker.Compositor.CreateExpressionAnimation("Ceil(value + (_.Progress > 0 ? _.Progress * (value - maxValue) * -1 : 0)) + 2");
            offset1.SetReferenceParameter("_", properties);
            offset1.SetScalarParameter("value", current1);
            offset1.SetScalarParameter("maxValue", future10);

            visual2.Visual.StartAnimation("Translation.X", offset2);
            visual0.Visual.StartAnimation("Translation.X", offset0);
            visual1.Visual.StartAnimation("Translation.X", offset1);
        }

        public void ValuesChanged(InteractionTracker sender, InteractionTrackerValuesChangedArgs args)
        {

        }

        public void InertiaStateEntered(InteractionTracker sender, InteractionTrackerInertiaStateEnteredArgs args)
        {
            if (args.ModifiedRestingPosition?.X is float position)
            {
                if (_canGoNext && position.AlmostEquals(sender.MaxPosition.X))
                {
                    ViewChanging?.Invoke(this, new CarouselViewChangingEventArgs(_viewChanged = CarouselDirection.Next));
                    ConfigureAnimations(_tracker.MaxPosition.X);
                }
                else if (_canGoPrev && position.AlmostEquals(sender.MinPosition.X))
                {
                    ViewChanging?.Invoke(this, new CarouselViewChangingEventArgs(_viewChanged = CarouselDirection.Previous));
                    ConfigureAnimations(_tracker.MinPosition.X);
                }

                return;
            }

            _viewChanged = CarouselDirection.None;
        }

        public void IdleStateEntered(InteractionTracker sender, InteractionTrackerIdleStateEnteredArgs args)
        {
            //_tracker.TryUpdatePosition(Vector3.Zero);
            //ConfigureAnimations(0);

            if (_viewChanged != CarouselDirection.None)
            {
                ViewChanged?.Invoke(this, new CarouselViewChangedEventArgs(_viewChanged));
                _viewChanged = CarouselDirection.None;
            }
        }

        public void InteractingStateEntered(InteractionTracker sender, InteractionTrackerInteractingStateEnteredArgs args)
        {
            ConfigureAnimations(_restingValue);
        }

        public void RequestIgnored(InteractionTracker sender, InteractionTrackerRequestIgnoredArgs args)
        {

        }

        public void CustomAnimationStateEntered(InteractionTracker sender, InteractionTrackerCustomAnimationStateEnteredArgs args)
        {

        }

        #endregion
    }
}
