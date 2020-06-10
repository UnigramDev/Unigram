using System;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;

namespace Unigram.Controls
{
    public class WebPageButton : Button
    {
        private TextBlock ContentPresenter;

        protected override void OnApplyTemplate()
        {
            ContentPresenter = (TextBlock)GetTemplateChild("ContentPresenter");

            base.OnApplyTemplate();
        }

        #region WebPage

        public WebPage WebPage
        {
            get { return (WebPage)GetValue(WebPageProperty); }
            set { SetValue(WebPageProperty, value); }
        }

        public static readonly DependencyProperty WebPageProperty =
            DependencyProperty.Register("WebPage", typeof(WebPage), typeof(WebPageButton), new PropertyMetadata(null, OnWebPageChanged));

        private static void OnWebPageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {

        }

        public void UpdateWebPage(WebPage webPage)
        {
            if (webPage == null)
            {
                Visibility = Visibility.Collapsed;
                return;
            }

            var run1 = ContentPresenter?.Inlines[0] as Run;
            var run2 = ContentPresenter?.Inlines[1] as Run;
            var run3 = ContentPresenter?.Inlines[2] as Run;

            if (webPage.InstantViewVersion != 0)
            {
                //if (webPage.IsInstantGallery())
                //{
                //    Visibility = Visibility.Collapsed;
                //}
                //else
                {
                    if (run1 != null)
                    {
                        run1.Text = run3.Text = "\uE611";
                        run2.Text = $"  {Strings.Resources.InstantView}  ";
                        run3.Foreground = null;
                    }

                    Visibility = Visibility.Visible;
                }
            }
            else if (string.Equals(webPage.Type, "telegram_megagroup", StringComparison.OrdinalIgnoreCase))
            {
                if (run1 != null)
                {
                    run1.Text = run3.Text = string.Empty;
                    run2.Text = Strings.Resources.OpenGroup;
                    run3.Foreground = null;
                }

                Visibility = Visibility.Visible;
            }
            else if (string.Equals(webPage.Type, "telegram_channel", StringComparison.OrdinalIgnoreCase))
            {
                if (run1 != null)
                {
                    run1.Text = run3.Text = string.Empty;
                    run2.Text = Strings.Resources.OpenChannel;
                    run3.Foreground = null;
                }

                Visibility = Visibility.Visible;
            }
            else if (string.Equals(webPage.Type, "telegram_message", StringComparison.OrdinalIgnoreCase))
            {
                if (run1 != null)
                {
                    run1.Text = run3.Text = string.Empty;
                    run2.Text = Strings.Resources.OpenMessage;
                    run3.Foreground = null;
                }

                Visibility = Visibility.Visible;
            }
            else
            {
                Visibility = Visibility.Collapsed;
            }
        }
        #endregion
    }
}
