using LinqToVisualTree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Services;
using Unigram.Services.Settings;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Shapes;

namespace Unigram.Controls
{
    public class TLContentDialog : ContentDialog
    {
        public TLContentDialog()
        {
            if (Window.Current.Content is FrameworkElement element)
            {
                var app = App.Current.RequestedTheme == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
                var frame = element.RequestedTheme;

                if (app != frame && SettingsService.Current.Appearance.RequestedTheme != ElementTheme.Default)
                {
                    RequestedTheme = SettingsService.Current.Appearance.GetCalculatedElementTheme();
                }
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
    }
}
