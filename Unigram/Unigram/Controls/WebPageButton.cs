using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
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
            OnWebPageChanged(WebPage, null);

            base.OnApplyTemplate();
        }

        #region WebPage

        public TLWebPageBase WebPage
        {
            get { return (TLWebPageBase)GetValue(WebPageProperty); }
            set { SetValue(WebPageProperty, value); }
        }

        public static readonly DependencyProperty WebPageProperty =
            DependencyProperty.Register("WebPage", typeof(TLWebPageBase), typeof(WebPageButton), new PropertyMetadata(null, OnWebPageChanged));

        private static void OnWebPageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((WebPageButton)d).OnWebPageChanged((TLWebPageBase)e.NewValue, (TLWebPageBase)e.OldValue);
        }

        private void OnWebPageChanged(TLWebPageBase newValue, TLWebPageBase oldValue)
        {
            if (newValue is TLWebPage webPage)
            {
                var run1 = ContentPresenter?.Inlines[0] as Run;
                var run2 = ContentPresenter?.Inlines[1] as Run;
                var run3 = ContentPresenter?.Inlines[2] as Run;

                if (webPage.HasCachedPage)
                {
                    if (webPage.IsInstantGallery())
                    {
                        Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        if (run1 != null)
                        {
                            run1.Text = run3.Text = "\uE611";
                            run2.Text = $"  {Strings.Android.InstantView}  ";
                            run3.Foreground = null;
                        }

                        Visibility = Visibility.Visible;
                    }
                }
                else if (webPage.HasType && webPage.Type.Equals("telegram_megagroup", StringComparison.OrdinalIgnoreCase))
                {
                    if (run1 != null)
                    {
                        run1.Text = run3.Text = string.Empty;
                        run2.Text = Strings.Android.OpenGroup;
                        run3.Foreground = null;
                    }

                    Visibility = Visibility.Visible;
                }
                else if (webPage.HasType && webPage.Type.Equals("telegram_channel", StringComparison.OrdinalIgnoreCase))
                {
                    if (run1 != null)
                    {
                        run1.Text = run3.Text = string.Empty;
                        run2.Text = Strings.Android.OpenChannel;
                        run3.Foreground = null;
                    }

                    Visibility = Visibility.Visible;
                }
                else if (webPage.HasType && webPage.Type.Equals("telegram_message", StringComparison.OrdinalIgnoreCase))
                {
                    if (run1 != null)
                    {
                        run1.Text = run3.Text = string.Empty;
                        run2.Text = Strings.Android.OpenMessage;
                        run3.Foreground = null;
                    }

                    Visibility = Visibility.Visible;
                }
                else
                {
                    Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                Visibility = Visibility.Collapsed;
            }
        }

        #endregion
    }
}
