using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Unigram.Common
{
    public static class TextBlockHelper
    {
        #region SentCodeType

        public static TLAuthSentCodeTypeBase GetSentCodeType(DependencyObject obj)
        {
            return (TLAuthSentCodeTypeBase)obj.GetValue(SentCodeTypeProperty);
        }

        public static void SetSentCodeType(DependencyObject obj, TLAuthSentCodeTypeBase value)
        {
            obj.SetValue(SentCodeTypeProperty, value);
        }

        public static readonly DependencyProperty SentCodeTypeProperty =
            DependencyProperty.RegisterAttached("SentCodeType", typeof(TLAuthSentCodeTypeBase), typeof(TextBlockHelper), new PropertyMetadata(null, OnSentCodeTypeChanged));

        private static void OnSentCodeTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as TextBlock;
            var type = e.NewValue as TLAuthSentCodeTypeBase;

            sender.Inlines.Clear();

            switch (type)
            {
                case TLAuthSentCodeTypeApp appType:
                    sender.Inlines.Add(new Run { Text = "We've sent the code the " });
                    sender.Inlines.Add(new Run { Text = "Telegram", FontWeight = FontWeights.SemiBold });
                    sender.Inlines.Add(new Run { Text = " app on your other device." });
                    break;
                case TLAuthSentCodeTypeSms smsType:
                    sender.Inlines.Add(new Run { Text = "We've sent you an SMS with the code." });
                    break;
            }
        }

        #endregion

        #region WebPage

        public static TLWebPageBase GetWebPage(DependencyObject obj)
        {
            return (TLWebPageBase)obj.GetValue(WebPageProperty);
        }

        public static void SetWebPage(DependencyObject obj, TLWebPageBase value)
        {
            obj.SetValue(WebPageProperty, value);
        }

        public static readonly DependencyProperty WebPageProperty =
            DependencyProperty.RegisterAttached("WebPage", typeof(TLWebPageBase), typeof(TextBlockHelper), new PropertyMetadata(null, OnWebPageChanged));

        private static void OnWebPageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as RichTextBlock;
            var newValue = e.NewValue as TLWebPageBase;

            OnWebPageChanged(sender, newValue);
        }

        private static void OnWebPageChanged(RichTextBlock sender, TLWebPageBase newValue)
        {
            sender.IsTextSelectionEnabled = false;

            var webPage = newValue as TLWebPage;
            if (webPage != null)
            {
                var paragraph = new Paragraph();

                if (webPage.HasSiteName && !string.IsNullOrWhiteSpace(webPage.SiteName))
                {
                    var foreground = sender.Resources["MessageHeaderForegroundBrush"] as SolidColorBrush;

                    paragraph.Inlines.Add(new Run { Text = webPage.SiteName, FontWeight = FontWeights.SemiBold, Foreground = foreground });
                    paragraph.Inlines.Add(new LineBreak());
                }

                if (webPage.HasTitle && !string.IsNullOrWhiteSpace(webPage.Title))
                {
                    paragraph.Inlines.Add(new Run { Text = webPage.Title, FontWeight = FontWeights.SemiBold });
                    paragraph.Inlines.Add(new LineBreak());
                }
                else if (webPage.HasAuthor && !string.IsNullOrWhiteSpace(webPage.Author))
                {
                    paragraph.Inlines.Add(new Run { Text = webPage.Author, FontWeight = FontWeights.SemiBold });
                    paragraph.Inlines.Add(new LineBreak());
                }

                if (webPage.HasDescription && !string.IsNullOrWhiteSpace(webPage.Description))
                {
                    paragraph.Inlines.Add(new Run { Text = webPage.Description });
                }

                sender.Blocks.Clear();
                sender.Blocks.Add(paragraph);

                if (paragraph.Inlines.Count > 0)
                {
                    sender.Visibility = Visibility.Visible;
                }
                else
                {
                    sender.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                sender.Blocks.Clear();
            }
        }

        #endregion
    }
}
