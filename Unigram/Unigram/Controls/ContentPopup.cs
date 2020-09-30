using LinqToVisualTree;
using System.Linq;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Services;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
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
