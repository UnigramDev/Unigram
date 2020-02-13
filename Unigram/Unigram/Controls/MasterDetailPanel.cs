using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Services;
using Unigram.Views.Host;
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

        protected override Size MeasureOverride(Size availableSize)
        {
            var background = Children[0];
            var title = Children[1];
            var header = Children[2];
            var detail = Children[3];
            var master = Children[4];
            var grip = Children[5] as FrameworkElement;

            if (grip.ManipulationMode == ManipulationModes.System)
            {
                grip.ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.TranslateRailsY;
                grip.ManipulationStarted += Grip_ManipulationStarted;
                grip.ManipulationDelta += Grip_ManipulationDelta;
                grip.ManipulationCompleted += Grip_ManipulationCompleted;

                grip.PointerEntered += Grip_PointerEntered;
                grip.PointerPressed += Grip_PointerPressed;
                grip.PointerReleased += Grip_PointerReleased;
                grip.PointerExited += Grip_PointerExited;
                grip.PointerCanceled += Grip_PointerExited;
                grip.PointerCaptureLost += Grip_PointerExited;
                grip.Unloaded += Grip_Unloaded;
            }

            if (availableSize.Width < columnMinimalWidthLeft + columnMinimalWidthMain)
            {
                title.Measure(availableSize);
                background.Measure(availableSize);
                header.Measure(availableSize);

                master.Measure(new Size(availableSize.Width, availableSize.Height - title.DesiredSize.Height - header.DesiredSize.Height));
                detail.Measure(new Size(availableSize.Width, availableSize.Height - title.DesiredSize.Height - header.DesiredSize.Height));

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

                header.Measure(new Size(availableSize.Width - result, availableSize.Height));
                title.Measure(new Size(availableSize.Width, availableSize.Height));
                background.Measure(new Size(availableSize.Width - result, availableSize.Height));

                master.Measure(new Size(result, availableSize.Height - title.DesiredSize.Height));
                detail.Measure(new Size(availableSize.Width - result, availableSize.Height - title.DesiredSize.Height - header.DesiredSize.Height));

                grip.Measure(new Size(12, availableSize.Height));
            }

            return availableSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var background = Children[0];
            var title = Children[1];
            var header = Children[2];
            var detail = Children[3];
            var master = Children[4];
            var grip = Children[5];

            if (finalSize.Width < columnMinimalWidthLeft + columnMinimalWidthMain)
            {
                CurrentState = MasterDetailState.Minimal;

                title.Arrange(new Rect(new Point(0, 0), finalSize));
                background.Arrange(new Rect(new Point(0, 0), finalSize));
                header.Arrange(new Rect(new Point(0, title.DesiredSize.Height), finalSize));

                master.Arrange(new Rect(0, title.DesiredSize.Height + header.DesiredSize.Height, finalSize.Width, finalSize.Height - title.DesiredSize.Height - header.DesiredSize.Height));
                detail.Arrange(new Rect(0, title.DesiredSize.Height + header.DesiredSize.Height, finalSize.Width, finalSize.Height - title.DesiredSize.Height - header.DesiredSize.Height));

                grip.Arrange(new Rect(0, 0, 0, 0));

                if (Window.Current.Content is RootPage root)
                {
                    root.TopPadding = new Thickness(0, header.DesiredSize.Height, 0, 0);
                }
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

                title.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
                background.Arrange(new Rect(result, 0, finalSize.Width - result, finalSize.Height));
                header.Arrange(new Rect(result, title.DesiredSize.Height, finalSize.Width - result, finalSize.Height));

                master.Arrange(new Rect(0, title.DesiredSize.Height, result, finalSize.Height - title.DesiredSize.Height));
                detail.Arrange(new Rect(result, title.DesiredSize.Height + header.DesiredSize.Height, finalSize.Width - result, finalSize.Height - title.DesiredSize.Height - header.DesiredSize.Height));

                grip.Arrange(new Rect(result, 0, 12, finalSize.Height));

                if (Window.Current.Content is RootPage root)
                {
                    root.TopPadding = new Thickness(0, 0, 0, 0);
                }
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

        private void Grip_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            VisualStateManager.GoToState(Children[5] as UserControl, "Pressed", false);
        }

        private void Grip_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            var master = Children[4] as FrameworkElement;
            var grip = Children[5];

            var newWidth = master.ActualWidth + e.Cumulative.Translation.X + 12;
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

            grip.Arrange(new Rect(newWidth, 0, 12, ActualHeight));
            gripWidthRatio = newRatio;
        }

        private void Grip_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            VisualStateManager.GoToState(Children[5] as UserControl, "Normal", false);

            dialogsWidthRatio = gripWidthRatio;
            SettingsService.Current.DialogsWidthRatio = gripWidthRatio;

            InvalidateMeasure();
            InvalidateArrange();

            if (e.Position.X < 0 || e.Position.X > 12)
            {
                Window.Current.CoreWindow.PointerCursor = _defaultCursor;
            }

            foreach (var pointer in Children[5].PointerCaptures)
            {
                Children[5].ReleasePointerCapture(pointer);
            }
        }

        private static readonly CoreCursor _defaultCursor = new CoreCursor(CoreCursorType.Arrow, 1);
        private static readonly CoreCursor _resizeCursor = new CoreCursor(CoreCursorType.SizeWestEast, 1);

        private void Grip_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = _resizeCursor;
        }

        private void Grip_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = _defaultCursor;
        }

        private void Grip_Unloaded(object sender, RoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = _defaultCursor;
        }

        private void Grip_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            VisualStateManager.GoToState(Children[5] as UserControl, "Pressed", false);

            Children[5].CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void Grip_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            VisualStateManager.GoToState(Children[5] as UserControl, "Normal", false);

            var point = e.GetCurrentPoint(Children[5]);
            if (point.Position.X < 0 || point.Position.X > 12)
            {
                Window.Current.CoreWindow.PointerCursor = _defaultCursor;
            }

            Children[2].ReleasePointerCapture(e.Pointer);
            e.Handled = true;
        }

        public event EventHandler ViewStateChanged;
    }
}
