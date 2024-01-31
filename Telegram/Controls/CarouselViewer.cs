//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Composition;
using Telegram.Navigation;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.System;
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

    public class CarouselViewer : Grid
    {
        private bool _requiresArrange;
        private ulong _scrolling;

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
                _hasInitialLoadedEventFired = true;

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

            if (_trackerOwner != null)
            {
                _trackerOwner.InertiaStateEntered += OnInertiaStateEntered;
                _trackerOwner.InteractingStateEntered += OnInteractingStateEntered;
                _trackerOwner.IdleStateEntered += OnIdleStateEntered;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_trackerOwner != null)
            {
                _trackerOwner.InertiaStateEntered -= OnInertiaStateEntered;
                _trackerOwner.InteractingStateEntered -= OnInteractingStateEntered;
                _trackerOwner.IdleStateEntered -= OnIdleStateEntered;
            }
        }

        private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            // TODO: I don't understand how to use VisualInteractionSource
            // for vertical pointer wheel + horizontal position source

            if (_interactionSource?.PositionXSourceMode != InteractionSourceMode.EnabledWithInertia || IsScrolling)
            {
                return;
            }

            var ctrl = WindowContext.IsKeyDown(VirtualKey.Control);

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

            if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
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
                _visuals[i] = ElementComposition.GetElementVisual(_elements[i]);
                ElementCompositionPreview.SetIsTranslationEnabled(_elements[i], true);
            }

            _hasConfiguredElements = true;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
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

        public bool IsScrolling => Logger.TickCount - _scrolling < 100;

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
            _scrolling = Logger.TickCount;

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

        private WeakInteractionTrackerOwner _trackerOwner;
        private InteractionTracker _tracker;
        private VisualInteractionSource _interactionSource;

        private CompositionPropertySet _progress;

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

            _trackerOwner = new WeakInteractionTrackerOwner();

            //Create tracker and associate interaction source
            _tracker = InteractionTracker.CreateWithOwner(Window.Current.Compositor, _trackerOwner);
            _tracker.InteractionSources.Add(_interactionSource);

            _tracker.Properties.InsertScalar("RestingValue", _restingValue);
            _tracker.Properties.InsertBoolean("CanGoNext", _canGoNext);
            _tracker.Properties.InsertBoolean("CanGoPrev", _canGoPrev);

            _progress = _tracker.Compositor.CreatePropertySet();
            _progress.InsertScalar("Progress", 0);

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

            static Vector2 GetSize(FrameworkElement element)
            {
                if (element is AspectView { RotationAngle: RotationAngle.Angle90 or RotationAngle.Angle270 })
                {
                    return new Vector2(element.ActualSize.Y, element.ActualSize.X);
                }

                return element.ActualSize;
            }

            (Visual Visual, Vector2 Size)
                visual2 = (_visuals[0], GetSize(_elements[0]));

            (Visual Visual, Vector2 Size)
                visual0 = (_visuals[1], GetSize(_elements[1]));

            (Visual Visual, Vector2 Size)
                visual1 = (_visuals[2], GetSize(_elements[2]));

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

            _progress.InsertScalar("Progress", 0);
            _progress.StartAnimation("Progress", progress);

            var offset2 = _tracker.Compositor.CreateExpressionAnimation("Floor(value + (_.Progress < 0 ? _.Progress * (maxValue - value) * -1 : 0)) - 2");
            offset2.SetReferenceParameter("_", _progress);
            offset2.SetScalarParameter("value", current2);
            offset2.SetScalarParameter("maxValue", future20);

            var offset0 = _tracker.Compositor.CreateExpressionAnimation("_.Progress > 0 " +
                "? _.Progress * maxValue * -1" +
                ": _.Progress * minValue");
            offset0.SetReferenceParameter("_", _progress);
            offset0.SetScalarParameter("minValue", future02);
            offset0.SetScalarParameter("maxValue", future01);

            var offset1 = _tracker.Compositor.CreateExpressionAnimation("Ceil(value + (_.Progress > 0 ? _.Progress * (value - maxValue) * -1 : 0)) + 2");
            offset1.SetReferenceParameter("_", _progress);
            offset1.SetScalarParameter("value", current1);
            offset1.SetScalarParameter("maxValue", future10);

            visual2.Visual.StartAnimation("Translation.X", offset2);
            visual0.Visual.StartAnimation("Translation.X", offset0);
            visual1.Visual.StartAnimation("Translation.X", offset1);
        }

        private void OnInertiaStateEntered(InteractionTracker sender, InteractionTrackerInertiaStateEnteredArgs args)
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

        private void OnIdleStateEntered(InteractionTracker sender, InteractionTrackerIdleStateEnteredArgs args)
        {
            //_tracker.TryUpdatePosition(Vector3.Zero);
            //ConfigureAnimations(0);

            if (_viewChanged != CarouselDirection.None)
            {
                ViewChanged?.Invoke(this, new CarouselViewChangedEventArgs(_viewChanged));
                _viewChanged = CarouselDirection.None;
            }
        }

        private void OnInteractingStateEntered(InteractionTracker sender, InteractionTrackerInteractingStateEnteredArgs args)
        {
            ConfigureAnimations(_restingValue);
        }

        #endregion
    }
}
