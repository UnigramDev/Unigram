using System;
using Unigram.Services;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

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

        private bool _isPrimary = true;
        public bool IsPrimary
        {
            get => _isPrimary;
            set
            {
                if (_isPrimary != value)
                {
                    _isPrimary = value;

                    if (value)
                    {
                        gripWidthRatio = SettingsService.Current.DialogsWidthRatio;
                        dialogsWidthRatio = SettingsService.Current.DialogsWidthRatio;
                    }
                    else
                    {
                        gripWidthRatio = SettingsService.Current.ProfileWidthRatio;
                        dialogsWidthRatio = SettingsService.Current.ProfileWidthRatio;
                    }

                    InvalidateMeasure();
                    InvalidateArrange();
                }
            }
        }

        private bool _allowCompact = true;
        public bool AllowCompact
        {
            get => _allowCompact;
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
            get => (bool)GetValue(IsBlankProperty);
            set => SetValue(IsBlankProperty, value);
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
            var background = FindName("PART_Background") as UIElement; //Children[0];
            var masterHeader = FindName("PART_MasterHeader") as UIElement; //Children[1];
            var detailHeader = FindName("PART_DetailHeader") as UIElement; //Children[2];
            var corpusHeader = FindName("PART_CorpusHeader") as UIElement; //Children[2];
            var banner = FindName("PART_Banner") as UIElement; //Children[3];
            var detail = FindName("PART_DetailPresenter") as UIElement; //Children[4];
            var master = FindName("MasterFrame") as UIElement; //Children[5];
            var grip = FindName("PART_Grip") as FrameworkElement; //Children[6] as FrameworkElement;

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
            if (availableSize.Width < columnMinimalWidthLeft + columnMinimalWidthMain || _isBlank)
            {
                background?.Measure(availableSize);
                masterHeader?.Measure(availableSize);
                detailHeader?.Measure(availableSize);
                corpusHeader?.Measure(availableSize);
                banner?.Measure(availableSize);

                var bannerDesiredHeight = banner?.DesiredSize.Height ?? 0;
                var masterHeaderDesiredHeight = masterHeader?.DesiredSize.Height ?? 0;
                var detailHeaderDesiredHeight = detailHeader?.DesiredSize.Height ?? 0;

                master.Measure(new Size(availableSize.Width, Math.Max(0, availableSize.Height - bannerDesiredHeight - masterHeaderDesiredHeight)));
                detail.Measure(new Size(availableSize.Width, Math.Max(0, availableSize.Height - bannerDesiredHeight - detailHeaderDesiredHeight)));

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
                    result = dialogsWidthRatio > 0 ? CountDialogsWidthFromRatio(availableSize.Width, dialogsWidthRatio, _isPrimary) : columnMinimalWidthLeft;
                }

                var corpus = SettingsService.Current.ProfileWidthRatio > 0 ? CountDialogsWidthFromRatio(availableSize.Width - result, SettingsService.Current.ProfileWidthRatio, false) : columnMinimalWidthLeft;

                masterHeader?.Measure(new Size(result, availableSize.Height));
                corpusHeader?.Measure(new Size(availableSize.Width - result - corpus, availableSize.Height));
                detailHeader?.Measure(new Size(corpusHeader?.DesiredSize.Height > 0 ? corpus : availableSize.Width - result, availableSize.Height));
                banner?.Measure(new Size(availableSize.Width - result, availableSize.Height));
                background?.Measure(new Size(availableSize.Width - result, availableSize.Height));

                var bannerDesiredHeight = banner?.DesiredSize.Height ?? 0;
                var masterHeaderDesiredHeight = masterHeader?.DesiredSize.Height ?? 0;
                var detailHeaderDesiredHeight = detailHeader?.DesiredSize.Height ?? 0;

                master.Measure(new Size(result, availableSize.Height - masterHeaderDesiredHeight));
                detail.Measure(new Size(availableSize.Width - result, availableSize.Height - bannerDesiredHeight - detailHeaderDesiredHeight));

                grip.Measure(new Size(8, availableSize.Height));
            }

            return availableSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var background = FindName("PART_Background") as UIElement; //Children[0];
            var masterHeader = FindName("PART_MasterHeader") as UIElement; //Children[1];
            var detailHeader = FindName("PART_DetailHeader") as UIElement; //Children[2];
            var corpusHeader = FindName("PART_CorpusHeader") as UIElement; //Children[2];
            var banner = FindName("PART_Banner") as UIElement; //Children[3];
            var detail = FindName("PART_DetailPresenter") as UIElement; //Children[4];
            var master = FindName("MasterFrame") as UIElement; //Children[5];
            var grip = FindName("PART_Grip") as FrameworkElement; //Children[6] as FrameworkElement;

            // Single column mode
            if (finalSize.Width < columnMinimalWidthLeft + columnMinimalWidthMain || _isBlank)
            {
                CurrentState = MasterDetailState.Minimal;

                background?.Arrange(new Rect(new Point(0, 0), finalSize));
                masterHeader?.Arrange(new Rect(new Point(0, 0), finalSize));
                detailHeader?.Arrange(new Rect(new Point(0, 0), finalSize));
                corpusHeader?.Arrange(new Rect(new Point(0, 0), finalSize));

                var bannerDesiredHeight = banner?.DesiredSize.Height ?? 0;
                var masterHeaderDesiredHeight = masterHeader?.DesiredSize.Height ?? 0;
                var detailHeaderDesiredHeight = detailHeader?.DesiredSize.Height ?? 0;

                banner?.Arrange(new Rect(new Point(0, Math.Max(detailHeaderDesiredHeight, masterHeaderDesiredHeight)), finalSize));

                master.Arrange(new Rect(0, bannerDesiredHeight + masterHeaderDesiredHeight, finalSize.Width, finalSize.Height - bannerDesiredHeight - masterHeaderDesiredHeight));
                detail.Arrange(new Rect(0, bannerDesiredHeight + detailHeaderDesiredHeight, finalSize.Width, finalSize.Height - bannerDesiredHeight - detailHeaderDesiredHeight));

                grip.Arrange(new Rect(0, 0, 0, 0));
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
                    result = dialogsWidthRatio > 0 ? CountDialogsWidthFromRatio(finalSize.Width, dialogsWidthRatio, _isPrimary) : columnMinimalWidthLeft;
                    CurrentState = MasterDetailState.Expanded;
                }

                var corpus = SettingsService.Current.ProfileWidthRatio > 0 ? CountDialogsWidthFromRatio(finalSize.Width - result, SettingsService.Current.ProfileWidthRatio, false) : columnMinimalWidthLeft;

                background?.Arrange(new Rect(result, 0, finalSize.Width - result, finalSize.Height));
                masterHeader?.Arrange(new Rect(0, 0, result, finalSize.Height));
                detailHeader?.Arrange(new Rect(result, 0, corpusHeader?.DesiredSize.Height > 0 ? corpus : finalSize.Width - result, finalSize.Height));
                corpusHeader?.Arrange(new Rect(result + corpus, 0, finalSize.Width - result - corpus, finalSize.Height));

                var bannerDesiredHeight = banner?.DesiredSize.Height ?? 0;
                var masterHeaderDesiredHeight = masterHeader?.DesiredSize.Height ?? 0;
                var detailHeaderDesiredHeight = detailHeader?.DesiredSize.Height ?? 0;

                banner?.Arrange(new Rect(result, detailHeaderDesiredHeight, finalSize.Width - result, finalSize.Height));

                master.Arrange(new Rect(0, masterHeaderDesiredHeight, result, finalSize.Height - masterHeaderDesiredHeight));
                detail.Arrange(new Rect(result, bannerDesiredHeight + detailHeaderDesiredHeight, finalSize.Width - result, finalSize.Height - bannerDesiredHeight - detailHeaderDesiredHeight));

                grip.Arrange(new Rect(result, 0, 8, finalSize.Height));
            }

            return finalSize;
        }

        private double CountDialogsWidthFromRatio(double width, double ratio, bool primary)
        {
            var result = Math.Round(width * ratio);
            if (primary)
            {
                result = Math.Max(result, columnMinimalWidthLeft);
                result = Math.Min(result, width - columnMinimalWidthMain);
            }
            else
            {
                result = Math.Max(result, columnMinimalWidthMain);
                result = Math.Min(result, width - columnMinimalWidthLeft);
            }

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
            var master = FindName("MasterFrame") as FrameworkElement; //Children[5] as FrameworkElement;
            var grip = FindName("PART_Grip") as UserControl; //Children[6] as UserControl;

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
                var grip = FindName("PART_Grip") as UserControl; //Children[6] as UserControl;
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
                    newWidth = CountDialogsWidthFromRatio(ActualWidth, newRatio, _isPrimary);
                }

                grip.Arrange(new Rect(newWidth, 0, 8, ActualHeight));
                gripWidthRatio = newRatio;
            }
        }

        private void Grip_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            var grip = FindName("PART_Grip") as UserControl; //Children[6] as UserControl;
            var point = e.GetCurrentPoint(grip);

            _pointerPressed = false;
            VisualStateManager.GoToState(grip, "Normal", false);

            dialogsWidthRatio = gripWidthRatio;

            if (_isPrimary)
            {
                SettingsService.Current.DialogsWidthRatio = gripWidthRatio;
            }
            else
            {
                SettingsService.Current.ProfileWidthRatio = gripWidthRatio;
            }

            if (_isPrimary)
            {
                InvalidateMeasure();
                InvalidateArrange();
            }
            else if (VisualTreeHelper.GetParent(VisualTreeHelper.GetParent(this)) is UIElement element)
            {
                element.InvalidateMeasure();
                element.InvalidateArrange();

                InvalidateMeasure();
                InvalidateArrange();
            }

            grip.ReleasePointerCapture(e.Pointer);
            e.Handled = true;

            if (point.Position.X is < 0 or > 8)
            {
                Window.Current.CoreWindow.PointerCursor = _defaultCursor;
            }
        }

        public event EventHandler ViewStateChanged;
    }
}
