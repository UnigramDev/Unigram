//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Runtime.CompilerServices;
using Telegram.Services;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls
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

        private MasterDetailState _currentState = MasterDetailState.Unknown;
        public MasterDetailState CurrentState
        {
            get => _currentState;
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
            get => _allowCompact;
            set
            {
                if (ActualWidth >= columnMinimalWidthLeft + columnMinimalWidthMain && dialogsWidthRatio == 0 && _allowCompact != value)
                {
                    _allowCompact = value;
                    InvalidateMeasure();
                }
                else
                {
                    _allowCompact = value;
                }
            }
        }

        public bool HasMaster { get; set; } = true;

        public double ActualMasterWidth => ((FrameworkElement)Children[2]).ActualWidth;

        public double ActualDetailWidth => ((FrameworkElement)Children[1]).ActualWidth;

        private bool _registerEvents = true;

        protected override Size MeasureOverride(Size availableSize)
        {
            var banner = Children[0];
            var detail = Children[1];
            var master = Children[2];
            var grip = Children[3] as FrameworkElement;

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
            if (availableSize.Width < columnMinimalWidthLeft + columnMinimalWidthMain || !HasMaster)
            {
                banner.Measure(CreateSize(availableSize.Width - 16, availableSize.Height));

                master.Measure(CreateSize(availableSize.Width, Math.Max(0, availableSize.Height - banner.DesiredSize.Height)));
                detail.Measure(CreateSize(availableSize.Width, Math.Max(0, availableSize.Height - banner.DesiredSize.Height)));

                grip.Measure(CreateSize(0, 0));
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

                banner.Measure(CreateSize(availableSize.Width - result, availableSize.Height));

                master.Measure(CreateSize(result, availableSize.Height));
                detail.Measure(CreateSize(availableSize.Width - result, availableSize.Height - banner.DesiredSize.Height));

                grip.Measure(CreateSize(8, availableSize.Height));
            }

            return availableSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var banner = Children[0];
            var detail = Children[1];
            var master = Children[2];
            var grip = Children[3] as FrameworkElement;

            // Single column mode
            if (finalSize.Width < columnMinimalWidthLeft + columnMinimalWidthMain || !HasMaster)
            {
                CurrentState = MasterDetailState.Minimal;

                banner.Arrange(CreateRect(12, 0, finalSize.Width - 16, banner.DesiredSize.Height));

                master.Arrange(CreateRect(0, banner.DesiredSize.Height, finalSize.Width, finalSize.Height - banner.DesiredSize.Height));
                detail.Arrange(CreateRect(0, banner.DesiredSize.Height, finalSize.Width, finalSize.Height - banner.DesiredSize.Height));

                grip.Arrange(CreateRect(0, 0, 0, 0));
            }
            else
            {
                double result;
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

                banner.Arrange(CreateRect(result, 0, finalSize.Width - result, banner.DesiredSize.Height));

                master.Arrange(CreateRect(0, 0, result, finalSize.Height));
                detail.Arrange(CreateRect(result, banner.DesiredSize.Height, finalSize.Width - result, finalSize.Height - banner.DesiredSize.Height));

                grip.Arrange(CreateRect(result, 0, 8, finalSize.Height));
            }

            return finalSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Size CreateSize(double width, double height)
        {
            return new Size(Math.Max(0, width), Math.Max(0, height));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Rect CreateRect(double x, double y, double width, double height)
        {
            return new Rect(x, y, Math.Max(0, width), Math.Max(0, height));
        }

        public static double CountDialogsWidthFromRatio(double width, double ratio)
        {
            var result = Math.Round(width * ratio);
            result = Math.Max(result, columnMinimalWidthLeft);
            result = Math.Min(result, columnMaximalWidthLeft);

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
            var master = Children[2] as FrameworkElement;
            var grip = Children[3] as UserControl;

            _pointerPressed = true;
            _pointerDelta = e.GetCurrentPoint(this).Position.X - master.ActualWidth;

            VisualStateManager.GoToState(grip, "Pressed", false);

            grip.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void Grip_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_pointerPressed)
            {
                var master = Children[2] as FrameworkElement;
                var grip = Children[3] as UserControl;

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
            var master = Children[2] as FrameworkElement;
            var grip = Children[3] as UserControl;

            _pointerPressed = false;
            VisualStateManager.GoToState(grip, "Normal", false);

            dialogsWidthRatio = gripWidthRatio;
            SettingsService.Current.DialogsWidthRatio = gripWidthRatio;

            InvalidateMeasure();

            grip.ReleasePointerCapture(e.Pointer);
            e.Handled = true;

            var point = e.GetCurrentPoint(grip);
            if (point.Position.X is < 0 or > 8)
            {
                Window.Current.CoreWindow.PointerCursor = _defaultCursor;
            }
        }

        public event EventHandler ViewStateChanged;
    }
}
