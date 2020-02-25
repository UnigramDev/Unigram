using LinqToVisualTree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Services;
using Unigram.Services.Settings;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Shapes;

namespace Unigram.Controls
{
    public class TLContentDialog : ContentDialog
    {
        private ContentDialogResult _result;

        public TLContentDialog()
        {
            DefaultStyleKey = typeof(TLContentDialog);

            if (Window.Current.Content is FrameworkElement element)
            {
                var app = App.Current.RequestedTheme == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
                var frame = element.RequestedTheme;

                if (app != frame && SettingsService.Current.Appearance.RequestedTheme != ElementTheme.Default)
                {
                    RequestedTheme = SettingsService.Current.Appearance.GetCalculatedElementTheme();
                }
            }

            Opened += TLContentDialog_Opened;
        }

        private void TLContentDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            var button = GetTemplateChild("PrimaryButton") as Button;
            if (button != null)
            {
                button.Focus(FocusState.Keyboard);
            }
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

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

        private void Rectangle_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            var pointer = e.GetCurrentPoint(this);
            if (pointer.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased)
            {
                Hide();
            }
        }

        public async Task<ContentDialogResult> OpenAsync()
        {
            await this.ShowQueuedAsync();
            return _result;
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
