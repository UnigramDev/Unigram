//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Text;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Views.Host;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls
{
    public class ContentPopup : ContentDialog
    {
        private ContentDialogResult _result;

        private Border BackgroundElement;

        public ContentPopup()
        {
            DefaultStyleKey = typeof(ContentPopup);
            DefaultButton = ContentDialogButton.Primary;

            if (Window.Current.Content is FrameworkElement element)
            {
                var app = BootStrapper.Current.RequestedTheme == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
                var frame = element.RequestedTheme;

                if (app != frame)
                {
                    RequestedTheme = SettingsService.Current.Appearance.GetCalculatedElementTheme();
                }
            }

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            Opened += OnOpened;
            Closing += OnClosing;
            Closed += OnClosed;
        }

        public virtual void OnNavigatedTo()
        {

        }

        private void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            if (Window.Current.Content is RootPage root)
            {
                root.PopupOpened();
            }
        }

        private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            var canvas = VisualTreeHelper.GetParent(this) as Canvas;
            if (canvas != null)
            {
                foreach (var child in canvas.Children)
                {
                    if (child is Rectangle rectangle)
                    {
                        rectangle.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }

        private void OnClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            if (Window.Current.Content is RootPage root)
            {
                root.PopupClosed();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplicationView.GetForCurrentView().VisibleBoundsChanged += ApplicationView_VisibleBoundsChanged;

            Window.Current.CoreWindow.CharacterReceived += OnCharacterReceived;

            ApplicationView_VisibleBoundsChanged(ApplicationView.GetForCurrentView());

            var canvas = VisualTreeHelper.GetParent(this) as Canvas;
            if (canvas != null)
            {
                foreach (var child in canvas.Children)
                {
                    if (child is Rectangle rectangle)
                    {
                        rectangle.Visibility = Visibility.Visible;
                        rectangle.Fill = new SolidColorBrush(ActualTheme == ElementTheme.Light
                            ? Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)
                            : Color.FromArgb(0x99, 0x00, 0x00, 0x00));
                    }
                }
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ApplicationView.GetForCurrentView().VisibleBoundsChanged -= ApplicationView_VisibleBoundsChanged;

            Window.Current.CoreWindow.CharacterReceived -= OnCharacterReceived;
        }

        private void OnCharacterReceived(CoreWindow sender, CharacterReceivedEventArgs args)
        {
            var character = Encoding.UTF32.GetString(BitConverter.GetBytes(args.KeyCode));
            if (character != "\r" || DefaultButton == ContentDialogButton.Primary)
            {
                return;
            }

            // TODO: should the if be simplified to focused is null or not Control?

            var focused = FocusManager.GetFocusedElement();
            if (focused is null or (not TextBox and not RichEditBox and not Button and not MenuFlyoutItem))
            {
                Hide(ContentDialogResult.Primary);
                args.Handled = true;
            }
        }

        private void ApplicationView_VisibleBoundsChanged(ApplicationView sender, object args = null)
        {
            if (Content is FrameworkElement && !IsFullWindow)
            {
                if (VerticalContentAlignment == VerticalAlignment.Center)
                {
                    BackgroundElement.MaxHeight = Math.Min(sender.VisibleBounds.Height - 40 - 40, 640);
                }
                else
                {
                    BackgroundElement.MaxHeight = Math.Min(sender.VisibleBounds.Height - 40 - 40, ContentMaxHeight);
                }
            }
        }

        public bool IsFullWindow { get; set; } = false;

        public bool FocusPrimaryButton { get; set; } = true;
        public bool IsLightDismissEnabled { get; set; } = true;

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            BackgroundElement = GetTemplateChild(nameof(BackgroundElement)) as Border;

            VisualStateManager.GoToState(this, IsPrimaryButtonSplit ? "PrimaryAsSplitButton" : "NoSplitButton", false);

            var button = GetTemplateChild("PrimaryButton") as Button;
            if (button != null && FocusPrimaryButton)
            {
                button.Loaded += PrimaryButton_Loaded;
            }

            var rectangle = GetTemplateChild("LightDismiss") as Rectangle;
            if (rectangle == null)
            {
                return;
            }

            //if (ActualTheme == ElementTheme.Dark)
            //{
            //    rectangle.Fill = new SolidColorBrush(Color.FromArgb(0x99, 0x00, 0x00, 0x00));
            //}
            //else
            //{
            //    rectangle.Fill = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF));
            //}

            rectangle.PointerReleased += Rectangle_PointerReleased;
        }

        private void PrimaryButton_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && FocusPrimaryButton)
            {
                button.Focus(FocusState.Keyboard);
            }
        }

        private void Rectangle_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            var pointer = e.GetCurrentPoint(this);
            if (pointer.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased && IsLightDismissEnabled)
            {
                Hide();
            }
        }

        public async Task<ContentDialogResult> OpenAsync()
        {
            await this.ShowQueuedAsync();
            return _result;
        }

        protected void SetResult(ContentDialogResult result)
        {
            _result = result;
        }

        public void Hide(ContentDialogResult result)
        {
            _result = result;

            if (result == ContentDialogResult.Primary)
            {
                var button = GetTemplateChild("PrimaryButton") as Button;
                if (button != null)
                {
                    if (button.IsEnabled)
                    {
                        var invoke = new ButtonAutomationPeer(button) as IInvokeProvider;
                        invoke?.Invoke();
                    }

                    return;
                }
            }
            else if (result == ContentDialogResult.Secondary)
            {
                var button = GetTemplateChild("SecondaryButton") as Button;
                if (button != null)
                {
                    if (button.IsEnabled)
                    {
                        var invoke = new ButtonAutomationPeer(button) as IInvokeProvider;
                        invoke?.Invoke();
                    }

                    return;
                }
            }

            Hide();
        }

        #region IsPrimaryButtonSplit

        public bool IsPrimaryButtonSplit
        {
            get => (bool)GetValue(IsPrimaryButtonSplitProperty);
            set => SetValue(IsPrimaryButtonSplitProperty, value);
        }

        public static readonly DependencyProperty IsPrimaryButtonSplitProperty =
            DependencyProperty.Register("IsPrimaryButtonSplit", typeof(bool), typeof(ContentPopup), new PropertyMetadata(false));

        #endregion

        #region SecondaryBackground

        public Brush SecondaryBackground
        {
            get { return (Brush)GetValue(SecondaryBackgroundProperty); }
            set { SetValue(SecondaryBackgroundProperty, value); }
        }

        public static readonly DependencyProperty SecondaryBackgroundProperty =
            DependencyProperty.Register("SecondaryBackground", typeof(Brush), typeof(ContentPopup), new PropertyMetadata(null));

        #endregion

        #region ContentMaxWidth

        public double ContentMaxWidth
        {
            get { return (double)GetValue(ContentMaxWidthProperty); }
            set { SetValue(ContentMaxWidthProperty, value); }
        }

        public static readonly DependencyProperty ContentMaxWidthProperty =
            DependencyProperty.Register("ContentMaxWidth", typeof(double), typeof(ContentPopup), new PropertyMetadata(320d));

        #endregion

        #region ContentMaxHeight

        public double ContentMaxHeight
        {
            get { return (double)GetValue(ContentMaxHeightProperty); }
            set { SetValue(ContentMaxHeightProperty, value); }
        }

        public static readonly DependencyProperty ContentMaxHeightProperty =
            DependencyProperty.Register("ContentMaxHeight", typeof(double), typeof(ContentPopup), new PropertyMetadata(568d));

        #endregion
    }
}
