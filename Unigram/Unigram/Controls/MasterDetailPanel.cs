using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Common;
using Windows.Foundation;
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

        private double gripWidthRatio = ApplicationSettings.Current.DialogsWidthRatio;
        private double dialogsWidthRatio = ApplicationSettings.Current.DialogsWidthRatio;

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

        protected override Size MeasureOverride(Size availableSize)
        {
            var detail = Children[0];
            var master = Children[1];
            var grip = Children[2];

            if (grip.ManipulationMode == ManipulationModes.System)
            {
                grip.ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.TranslateRailsY;
                grip.ManipulationStarted += Grip_ManipulationStarted;
                grip.ManipulationDelta += Grip_ManipulationDelta;
                grip.ManipulationCompleted += Grip_ManipulationCompleted;
            }

            if (availableSize.Width < columnMinimalWidthLeft + columnMinimalWidthMain)
            {
                master.Measure(availableSize);
                detail.Measure(availableSize);

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

                master.Measure(new Size(result, availableSize.Height));
                detail.Measure(new Size(availableSize.Width - result, availableSize.Height));

                grip.Measure(new Size(12, availableSize.Height));

                return availableSize;
            }

            return base.MeasureOverride(availableSize);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var detail = Children[0];
            var master = Children[1];
            var grip = Children[2];

            var old = _currentState;

            if (finalSize.Width < columnMinimalWidthLeft + columnMinimalWidthMain)
            {
                CurrentState = MasterDetailState.Minimal;

                master.Arrange(new Rect(new Point(0, 0), finalSize));
                detail.Arrange(new Rect(new Point(0, 0), finalSize));

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

                master.Arrange(new Rect(0, 0, result, finalSize.Height));
                detail.Arrange(new Rect(result, 0, finalSize.Width - result, finalSize.Height));

                grip.Arrange(new Rect(result, 0, 12, finalSize.Height));

                return finalSize;
            }

            return base.ArrangeOverride(finalSize);
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
            VisualStateManager.GoToState(Children[2] as UserControl, "Pressed", false);
        }

        private void Grip_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            var detail = Children[0];
            var master = Children[1] as FrameworkElement;
            var grip = Children[2];

            var newWidth = master.ActualWidth + e.Cumulative.Translation.X;
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
            VisualStateManager.GoToState(Children[2] as UserControl, "Normal", false);

            dialogsWidthRatio = gripWidthRatio;
            ApplicationSettings.Current.DialogsWidthRatio = gripWidthRatio;

            InvalidateMeasure();
            InvalidateArrange();
        }

        public event EventHandler ViewStateChanged;
    }
}
