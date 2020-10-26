using System;
using Unigram.Services;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Unigram.Controls
{
    public class MasterDetailPanel : Panel
    {
        private const double columnCompactWidthLeft = 72;
        private const double columnMinimalWidthLeft = 260;
        private const double columnMaximalWidthLeft = 540;
        private const double columnMinimalWidthMain = 380;
        private const double kDefaultDialogsWidthRatio = 5d / 14d;

        private double gripWidthRatio = SettingsService.Current.DialogsWidthRatio;
        private double dialogsWidthRatio = SettingsService.Current.DialogsWidthRatio;

        private MasterDetailState _currentState;
        public MasterDetailState CurrentState
        {
            get
            {
                return _currentState;
            }
            set
            {
                if (_currentState != value)
                {
                    _currentState = value;
                    ViewStateChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private bool _allowCompact = true;
        public bool AllowCompact
        {
            get
            {
                return _allowCompact;
            }
            set
            {
                if (_allowCompact != value)
                {
                    _allowCompact = value;
                    InvalidateMeasure();
                    InvalidateArrange();
                }
            }
        }

        private bool _isBlank;
        public bool IsBlank
        {
            get { return (bool)GetValue(IsBlankProperty); }
            set { SetValue(IsBlankProperty, value); }
        }

        public static readonly DependencyProperty IsBlankProperty =
            DependencyProperty.Register("IsBlank", typeof(bool), typeof(MasterDetailPanel), new PropertyMetadata(true, OnBlankChanged));

        private static void OnBlankChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var newValue = (bool)e.NewValue;
            var oldValue = (bool)e.OldValue;

            ((MasterDetailPanel)d)._isBlank = newValue;

            if (newValue != oldValue)
            {
                ((MasterDetailPanel)d).InvalidateMeasure();
                ((MasterDetailPanel)d).InvalidateArrange();
            }
        }

        private bool _registerEvents = true;

        protected override Size MeasureOverride(Size availableSize)
        {
            var background = Children[0];
            var masterHeader = Children[1];
            var detailHeader = Children[2];
            var banner = Children[3];
            var detail = Children[4];
            var master = Children[5];
            var grip = Children[6] as FrameworkElement;

            if (_registerEvents)
            {
                _registerEvents = false;

                grip.PointerEntered += Grip_PointerEntered;
                grip.PointerExited += Grip_PointerExited;
                grip.PointerPressed += Grip_PointerPressed;
                grip.PointerMoved += Grip_PointerMoved;
                grip.PointerReleased += Grip_PointerReleased;
                grip.PointerCanceled += Grip_PointerReleased;
                grip.PointerCaptureLost += Grip_PointerReleased;
                grip.Unloaded += Grip_Unloaded;
            }

            // Single column mode
            if (availableSize.Width < columnMinimalWidthLeft + columnMinimalWidthMain)
            {
                background.Measure(availableSize);
                masterHeader.Measure(availableSize);
                detailHeader.Measure(availableSize);
                banner.Measure(availableSize);

                master.Measure(new Size(availableSize.Width, Math.Max(0, availableSize.Height - banner.DesiredSize.Height - masterHeader.DesiredSize.Height)));
                detail.Measure(new Size(availableSize.Width, Math.Max(0, availableSize.Height - banner.DesiredSize.Height - detailHeader.DesiredSize.Height)));

                grip.Measure(new Size(0, 0));
            }
            else
            {
                var result = 0d;
                if (dialogsWidthRatio == 0 && _allowCompact)
                {
                    result = columnCompactWidthLeft;
                }
                else
                {
                    result = dialogsWidthRatio > 0 ? CountDialogsWidthFromRatio(availableSize.Width, dialogsWidthRatio) : columnMinimalWidthLeft;
                }

                masterHeader.Measure(new Size(result, availableSize.Height));
                detailHeader.Measure(new Size(availableSize.Width - result, availableSize.Height));
                banner.Measure(new Size(availableSize.Width - result, availableSize.Height));
                background.Measure(new Size(availableSize.Width - result, availableSize.Height));

                master.Measure(new Size(result, availableSize.Height - masterHeader.DesiredSize.Height));
                detail.Measure(new Size(availableSize.Width - result, availableSize.Height - banner.DesiredSize.Height - detailHeader.DesiredSize.Height));

                grip.Measure(new Size(8, availableSize.Height));
            }

            return availableSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var background = Children[0];
            var masterHeader = Children[1];
            var detailHeader = Children[2];
            var banner = Children[3];
            var detail = Children[4];
            var master = Children[5];
            var grip = Children[6];

            // Single column mode
            if (finalSize.Width < columnMinimalWidthLeft + columnMinimalWidthMain)
            {
                CurrentState = MasterDetailState.Minimal;

                background.Arrange(new Rect(new Point(0, 0), finalSize));
                masterHeader.Arrange(new Rect(new Point(0, 0), finalSize));
                detailHeader.Arrange(new Rect(new Point(0, 0), finalSize));
                banner.Arrange(new Rect(new Point(0, Math.Max(detailHeader.DesiredSize.Height, masterHeader.DesiredSize.Height)), finalSize));

                master.Arrange(new Rect(0, banner.DesiredSize.Height + masterHeader.DesiredSize.Height, finalSize.Width, finalSize.Height - banner.DesiredSize.Height - masterHeader.DesiredSize.Height));
                detail.Arrange(new Rect(0, banner.DesiredSize.Height + detailHeader.DesiredSize.Height, finalSize.Width, finalSize.Height - banner.DesiredSize.Height - detailHeader.DesiredSize.Height));

                grip.Arrange(new Rect(0, 0, 0, 0));
            }
            else
            {
                var result = 0d;
                if (dialogsWidthRatio == 0 && _allowCompact)
                {
                    result = columnCompactWidthLeft;
                    CurrentState = MasterDetailState.Compact;
                }
                else
                {
                    result = dialogsWidthRatio > 0 ? CountDialogsWidthFromRatio(finalSize.Width, dialogsWidthRatio) : columnMinimalWidthLeft;
                    CurrentState = MasterDetailState.Expanded;
                }

                background.Arrange(new Rect(result, 0, finalSize.Width - result, finalSize.Height));
                masterHeader.Arrange(new Rect(0, 0, result, finalSize.Height));
                detailHeader.Arrange(new Rect(result, 0, finalSize.Width - result, finalSize.Height));
                banner.Arrange(new Rect(result, detailHeader.DesiredSize.Height, finalSize.Width - result, finalSize.Height));

                master.Arrange(new Rect(0, masterHeader.DesiredSize.Height, result, finalSize.Height - masterHeader.DesiredSize.Height));
                detail.Arrange(new Rect(result, banner.DesiredSize.Height + detailHeader.DesiredSize.Height, finalSize.Width - result, finalSize.Height - banner.DesiredSize.Height - detailHeader.DesiredSize.Height));

                grip.Arrange(new Rect(result, 0, 8, finalSize.Height));
            }

            return finalSize;
        }

        private double CountDialogsWidthFromRatio(double width, double ratio)
        {
            var result = Math.Round(width * ratio);
            result = Math.Max(result, columnMinimalWidthLeft);
            result = Math.Min(result, width - columnMinimalWidthMain);

            return result;
        }

        private static readonly CoreCursor _defaultCursor = new CoreCursor(CoreCursorType.Arrow, 1);
        private static readonly CoreCursor _resizeCursor = new CoreCursor(CoreCursorType.SizeWestEast, 1);

        private bool _pointerPressed;
        private double _pointerDelta;

        private void Grip_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = _resizeCursor;
        }

        private void Grip_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (!_pointerPressed)
            {
                Window.Current.CoreWindow.PointerCursor = _defaultCursor;
            }
        }

        private void Grip_Unloaded(object sender, RoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = _defaultCursor;
        }

        private void Grip_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var master = Children[5] as FrameworkElement;
            var grip = Children[6] as UserControl;

            _pointerPressed = true;
            _pointerDelta = e.GetCurrentPoint(this).Position.X - master.ActualWidth;

            VisualStateManager.GoToState(grip as UserControl, "Pressed", false);

            grip.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void Grip_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_pointerPressed)
            {
                var master = Children[5] as FrameworkElement;
                var grip = Children[6] as UserControl;

                var point = e.GetCurrentPoint(this);

                var newWidth = point.Position.X - _pointerDelta;
                var newRatio = (newWidth < columnMinimalWidthLeft / 2)
                    ? 0
                    : newWidth / ActualWidth;

                if (newRatio == 0 && _allowCompact)
                {
                    newWidth = columnCompactWidthLeft;
                }
                else
                {
                    newWidth = CountDialogsWidthFromRatio(ActualWidth, newRatio);
                }

                grip.Arrange(new Rect(newWidth, 0, 8, ActualHeight));
                gripWidthRatio = newRatio;
            }
        }

        private void Grip_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            var master = Children[5] as FrameworkElement;
            var grip = Children[6] as UserControl;

            _pointerPressed = false;
            VisualStateManager.GoToState(grip, "Normal", false);

            dialogsWidthRatio = gripWidthRatio;
            SettingsService.Current.DialogsWidthRatio = gripWidthRatio;

            InvalidateMeasure();
            InvalidateArrange();

            grip.ReleasePointerCapture(e.Pointer);
            e.Handled = true;

            var point = e.GetCurrentPoint(Children[6]);
            if (point.Position.X < 0 || point.Position.X > 8)
            {
                Window.Current.CoreWindow.PointerCursor = _defaultCursor;
            }
        }

        public event EventHandler ViewStateChanged;
    }
}
