using LinqToVisualTree;
using System.Linq;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Services;
using Windows.Foundation;
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

            if (Window.Current.Content is FrameworkElement element)
            {
                var app = App.Current.RequestedTheme == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
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
            InputPane.GetForCurrentView().Showing += InputPane_Showing;
            InputPane.GetForCurrentView().Hiding += InputPane_Hiding;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            InputPane.GetForCurrentView().Showing -= InputPane_Showing;
            InputPane.GetForCurrentView().Hiding -= InputPane_Hiding;
        }

        private void InputPane_Showing(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            var element = FocusManager.GetFocusedElement() as Control;
            if (element is TextBox || element is RichEditBox)
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

        public bool FocusPrimaryButton { get; set; } = true;
        public bool IsLightDismissEnabled { get; set; } = true;

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            var button = GetTemplateChild("PrimaryButton") as Button;
            if (button != null && FocusPrimaryButton)
            {
                button.Loaded += PrimaryButton_Loaded;
            }

            var canvas = this.Ancestors().FirstOrDefault() as Canvas;
            if (canvas == null)
            {
                return;
            }

            var rectangle = canvas.Children[0] as Rectangle;
            if (rectangle == null)
            {
                return;
            }

            rectangle.RequestedTheme = RequestedTheme;
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
    }
}
