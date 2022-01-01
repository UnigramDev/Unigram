using System;
using System.Text;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Navigation;
using Unigram.Services;
using Windows.Foundation;
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

namespace Unigram.Controls
{
    public class ContentPopup : ContentDialog
    {
        private ContentDialogResult _result;

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
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplicationView.GetForCurrentView().VisibleBoundsChanged += ApplicationView_VisibleBoundsChanged;

            InputPane.GetForCurrentView().Showing += InputPane_Showing;
            InputPane.GetForCurrentView().Hiding += InputPane_Hiding;

            Window.Current.CoreWindow.CharacterReceived += OnCharacterReceived;

            ApplicationView_VisibleBoundsChanged(ApplicationView.GetForCurrentView());
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ApplicationView.GetForCurrentView().VisibleBoundsChanged -= ApplicationView_VisibleBoundsChanged;

            InputPane.GetForCurrentView().Showing -= InputPane_Showing;
            InputPane.GetForCurrentView().Hiding -= InputPane_Hiding;

            Window.Current.CoreWindow.CharacterReceived -= OnCharacterReceived;
        }

        private void OnCharacterReceived(CoreWindow sender, CharacterReceivedEventArgs args)
        {
            var character = Encoding.UTF32.GetString(BitConverter.GetBytes(args.KeyCode));
            if (character != "\r" || DefaultButton == ContentDialogButton.Primary)
            {
                return;
            }

            var focused = FocusManager.GetFocusedElement();
            if (focused is null or (not TextBox and not RichEditBox))
            {
                Hide(ContentDialogResult.Primary);
                args.Handled = true;
            }
        }

        private void ApplicationView_VisibleBoundsChanged(ApplicationView sender, object args = null)
        {
            if (Content is FrameworkElement element && !IsFullWindow)
            {
                element.MaxHeight = sender.VisibleBounds.Height - 32 - 32 - 48 - 48 - 48 - 48;
            }
        }

        private void InputPane_Showing(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            var element = FocusManager.GetFocusedElement() as Control;
            if (element is TextBox or RichEditBox)
            {
                var transform = element.TransformToVisual(Window.Current.Content);
                var point = transform.TransformPoint(new Point());

                var offset = point.Y + element.ActualHeight + 8;
                if (offset > args.OccludedRect.Y)
                {
                    RenderTransform = new TranslateTransform { Y = args.OccludedRect.Y - offset };
                }
            }
        }

        private void InputPane_Hiding(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            RenderTransform = null;
        }

        public bool IsFullWindow { get; set; } = false;

        public bool FocusPrimaryButton { get; set; } = true;
        public bool IsLightDismissEnabled { get; set; } = true;

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

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
                    var invoke = new ButtonAutomationPeer(button) as IInvokeProvider;
                    if (invoke != null)
                    {
                        invoke.Invoke();
                        return;
                    }
                }
            }
            else if (result == ContentDialogResult.Secondary)
            {
                var button = GetTemplateChild("SecondaryButton") as Button;
                if (button != null)
                {
                    var invoke = new ButtonAutomationPeer(button) as IInvokeProvider;
                    if (invoke != null)
                    {
                        invoke.Invoke();
                        return;
                    }
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

    }
}
