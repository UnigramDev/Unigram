using System;
using Telegram.Common;
using Windows.Foundation;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls
{
    public sealed partial class TabbedPageHeader : UserControl
    {
        private readonly GestureRecognizer _recognizer;
        private object _sender;

        public TabbedPageHeader()
        {
            InitializeComponent();

            _recognizer = new GestureRecognizer();
            _recognizer.GestureSettings = GestureSettings.HoldWithMouse;
            _recognizer.Holding += OnHolding;

            InitializeHolding(BackButton);
            InitializeHolding(ForwardButton);
        }

        private void OnHolding(GestureRecognizer sender, HoldingEventArgs args)
        {
            if (args.HoldingState == HoldingState.Started)
            {
                if (_sender == BackButton)
                {
                    BackButton.ReleasePointerCaptures();
                    GoBackRequested?.Invoke(BackButton, args);
                }
                else if (_sender == ForwardButton)
                {
                    ForwardButton.ReleasePointerCaptures();
                    GoForwardRequested?.Invoke(ForwardButton, args);
                }

                _sender = null;
            }
        }

        private void InitializeHolding(UIElement element)
        {
            element.AddHandler(PointerPressedEvent, new PointerEventHandler(OnPointerPressed), true);
            element.AddHandler(PointerMovedEvent, new PointerEventHandler(OnPointerMoved), true);
            element.AddHandler(PointerCanceledEvent, new PointerEventHandler(OnPointerCanceled), true);
            element.AddHandler(PointerReleasedEvent, new PointerEventHandler(OnPointerReleased), true);
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _sender = sender;
            _recognizer.TryProcessDownEvent(e.GetCurrentPoint(this));
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            _sender = sender;
            _recognizer.TryProcessMoveEvents(e.GetIntermediatePoints(this));
        }

        private void OnPointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            _sender = sender;
            _recognizer.TryCompleteGesture();
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _sender = sender;
            _recognizer.TryProcessUpEvent(e.GetCurrentPoint(this));
        }

        public bool CanGoBack
        {
            get => BackButton.IsEnabled;
            set => BackButton.IsEnabled = value;
        }

        public bool CanGoForward
        {
            get => ForwardButton.IsEnabled;
            set => ForwardButton.IsEnabled = value;
        }

        public event RoutedEventHandler GoBackClick
        {
            add => BackButton.Click += value;
            remove => BackButton.Click -= value;
        }

        public event TypedEventHandler<UIElement, object> GoBackRequested;

        public event RoutedEventHandler GoForwardClick
        {
            add => ForwardButton.Click += value;
            remove => ForwardButton.Click -= value;
        }

        public event TypedEventHandler<UIElement, object> GoForwardRequested;

        public Uri Source
        {
            set
            {
                if (value != null)
                {
                    SourceText.Text = value.Host + value.PathAndQuery + value.Fragment;

                    DomainText.Text = value.Host;
                    PathText.Text = value.PathAndQuery + value.Fragment;
                }
            }
        }

        public object Options
        {
            get => OptionsPresenter.Content;
            set => OptionsPresenter.Content = value;
        }

        private void BackButton_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            GoBackRequested?.Invoke(sender, args);
        }

        private void ForwardButton_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            GoForwardRequested?.Invoke(sender, args);
        }
    }
}
