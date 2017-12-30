using System;
using Telegram.Intro;
using Unigram.Common;
using Unigram.Views.SignIn;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Unigram.Views
{
    public sealed partial class IntroPage : Page
    {
        private TLIntroRenderer _renderer;

        private Visual _layoutVisual;
        private int _selectedIndex;
        private bool _selecting;

        private DispatcherTimer _timer;
        private bool _timedOut;

        public IntroPage()
        {
            InitializeComponent();

            _layoutVisual = ElementCompositionPreview.GetElementVisual(LayoutRoot);

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(3);
            _timer.Tick += Interact_Tick;

            LayoutRoot.ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.TranslateRailsX | ManipulationModes.TranslateInertia;
            LayoutRoot.ManipulationStarted += LayoutRoot_ManipulationStarted;
            LayoutRoot.ManipulationDelta += LayoutRoot_ManipulationDelta;
            LayoutRoot.ManipulationCompleted += LayoutRoot_ManipulationCompleted;

            SetIndex(_selectedIndex = 0);
        }

        private void SwapChain_Loaded(object sender, RoutedEventArgs e)
        {
            if (_renderer == null)
            {
                try
                {
                    _renderer = new TLIntroRenderer(SwapChain, ApplicationSettings.Current.CurrentTheme);
                    _renderer.Loaded();
                }
                catch { }
            }
        }

        private void SwapChain_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_renderer != null)
            {
                try
                {
                    _renderer.Dispose();
                    _renderer = null;
                }
                catch { }
            }
        }

        private void Interact_Tick(object sender, object e)
        {
            _timer.Stop();
            _timedOut = true;

            SetIndex(_selectedIndex);
        }

        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            base.OnPointerMoved(e);
            Interact(e.Pointer.PointerDeviceType != PointerDeviceType.Touch);
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            base.OnPointerPressed(e);
            Interact(e.Pointer.PointerDeviceType != PointerDeviceType.Touch);
        }

        protected override void OnPointerWheelChanged(PointerRoutedEventArgs e)
        {
            base.OnPointerWheelChanged(e);
            Interact(e.Pointer.PointerDeviceType != PointerDeviceType.Touch);

            var point = e.GetCurrentPoint(LayoutRoot);
            var delta = -point.Properties.MouseWheelDelta;

            Scroll(delta);
        }

        private void Interact(bool start)
        {
            _timedOut = !start;

            if (start)
            {
                _timer.Stop();
                _timer.Start();
            }

            SetIndex(_selectedIndex);
        }

        private void SetIndex(int index)
        {
            Carousel.SelectedIndex = index;
            BackButton.Visibility = index > 0 && !_timedOut ? Visibility.Visible : Visibility.Collapsed;
            NextButton.Visibility = index < 5 && !_timedOut ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            Interact(true);
            Scroll(-1);
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            Interact(true);
            Scroll(+1);
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SignInPage));
        }

        private void Scroll(double delta)
        {
            if (_selecting)
            {
                return;
            }

            _selecting = true;

            var width = (float)ActualWidth;
            var current = -(_selectedIndex * width);
            var previous = current + width;
            var next = current - width;

            var maximum = next;
            var minimum = previous;

            if (_selectedIndex == 0)
            {
                minimum = current;
                delta = delta > 0 ? delta : 0;
            }
            else if (_selectedIndex == 5)
            {
                maximum = current;
                delta = delta < 0 ? delta : 0;
            }

            var offset = _layoutVisual.Offset;

            var batch = _layoutVisual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

            var animation = _layoutVisual.Compositor.CreateScalarKeyFrameAnimation();
            animation.InsertKeyFrame(0, offset.X);

            if (delta < 0)
            {
                // previous
                _selectedIndex--;
                _renderer?.SetPage(_selectedIndex);
                animation.InsertKeyFrame(1, minimum);
            }
            else if (delta > 0)
            {
                // next
                _selectedIndex++;
                _renderer?.SetPage(_selectedIndex);
                animation.InsertKeyFrame(1, maximum);
            }
            else
            {
                // back
                animation.InsertKeyFrame(1, current);
            }

            _layoutVisual.StartAnimation("Offset.X", animation);

            SetIndex(_selectedIndex);

            batch.Completed += (s, args) =>
            {
                _selecting = false;
            };
            batch.End();
        }

        private void LayoutRoot_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            _selecting = true;
        }

        private void LayoutRoot_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            if (e.IsInertial)
            {
                e.Complete();
                return;
            }

            var delta = (float)e.Delta.Translation.X;
            var width = (float)ActualWidth;

            var current = -(_selectedIndex * width);
            var previous = current + width;
            var next = current - width;

            var maximum = next;
            var minimum = previous;

            if (_selectedIndex == 0)
            {
                minimum = 0;
            }
            else if (_selectedIndex == 5)
            {
                maximum = current;
            }

            var offset = _layoutVisual.Offset;
            offset.X = Math.Max(maximum, Math.Min(minimum, offset.X + delta));

            var position = Math.Max(maximum, Math.Min(minimum, offset.X)) / width;
            position += _selectedIndex;

            _layoutVisual.Offset = offset;
            _renderer?.SetScroll(-position);
        }

        private void LayoutRoot_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            var width = (float)ActualWidth;

            var current = -(_selectedIndex * width);

            var maximum = current - width;
            var minimum = current + width;

            var offset = _layoutVisual.Offset;
            var position = Math.Max(maximum, Math.Min(minimum, offset.X)) / width;

            position += _selectedIndex;

            _renderer?.SetScroll(0);

            var batch = _layoutVisual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

            var animation = _layoutVisual.Compositor.CreateScalarKeyFrameAnimation();

            animation.InsertKeyFrame(0, offset.X);

            if (position != 0 && e.Velocities.Linear.X > 1.5f)
            {
                if (_selectedIndex > 0)
                {
                    // previous
                    _selectedIndex--;
                    _renderer?.SetPage(_selectedIndex);
                    animation.InsertKeyFrame(1, minimum);
                }
                else
                {
                    animation.InsertKeyFrame(1, current);
                }
            }
            else if (position != 0 && e.Velocities.Linear.X < -1.5f)
            {
                if (_selectedIndex < 5)
                {
                    // next
                    _selectedIndex++;
                    _renderer?.SetPage(_selectedIndex);
                    animation.InsertKeyFrame(1, maximum);
                }
                else
                {
                    animation.InsertKeyFrame(1, current);
                }
            }
            else
            {
                // back
                animation.InsertKeyFrame(1, current);
            }

            _layoutVisual.StartAnimation("Offset.X", animation);

            SetIndex(_selectedIndex);

            batch.Completed += (s, args) =>
            {
                _selecting = false;
            };
            batch.End();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var size = base.MeasureOverride(availableSize);
            var current = -(_selectedIndex * availableSize.Width);

            LayoutRoot.Width = availableSize.Width * 6;

            if (_layoutVisual != null)
            {
                _layoutVisual.Offset = new System.Numerics.Vector3((float)current, 0, 0);
            }

            return size;
        }
    }
}
