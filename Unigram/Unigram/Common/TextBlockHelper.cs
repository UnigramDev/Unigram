﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Telegram.Api.TL.Auth;
using Unigram.Strings;
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
                    sender.Inlines.Add(new Run { Text = "We've sent the code to the " });
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
                }

                if (webPage.HasTitle && !string.IsNullOrWhiteSpace(webPage.Title))
                {
                    if (paragraph.Inlines.Count > 0)
                    {
                        paragraph.Inlines.Add(new LineBreak());
                    }

                    paragraph.Inlines.Add(new Run { Text = webPage.Title, FontWeight = FontWeights.SemiBold });
                }
                else if (webPage.HasAuthor && !string.IsNullOrWhiteSpace(webPage.Author))
                {
                    if (paragraph.Inlines.Count > 0)
                    {
                        paragraph.Inlines.Add(new LineBreak());
                    }

                    paragraph.Inlines.Add(new Run { Text = webPage.Author, FontWeight = FontWeights.SemiBold });
                }

                if (webPage.HasDescription && !string.IsNullOrWhiteSpace(webPage.Description))
                {
                    if (paragraph.Inlines.Count > 0)
                    {
                        paragraph.Inlines.Add(new LineBreak());
                    }

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

        #region Markdown
        public static string GetMarkdown(DependencyObject obj)
        {
            return (string)obj.GetValue(MarkdownProperty);
        }

        public static void SetMarkdown(DependencyObject obj, string value)
        {
            obj.SetValue(MarkdownProperty, value);
        }

        public static readonly DependencyProperty MarkdownProperty =
            DependencyProperty.RegisterAttached("Markdown", typeof(string), typeof(TextBlockHelper), new PropertyMetadata(null, OnTextChanged));

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as TextBlock;
            var markdown = e.NewValue as string;

            sender.Inlines.Clear();

            var previous = 0;
            var index = markdown.IndexOf("**");
            var next = index > -1 ? markdown.IndexOf("**", index + 2) : -1;

            while (index > -1 && next > -1)
            {
                if (index - previous > 0)
                {
                    sender.Inlines.Add(new Run { Text = markdown.Substring(previous, index - previous) });
                }

                sender.Inlines.Add(new Run { Text = markdown.Substring(index + 2, next - index - 2), FontWeight = FontWeights.SemiBold });

                previous = next + 2;
                index = markdown.IndexOf("**", next + 2);
                next = index > -1 ? markdown.IndexOf("**", index + 2) : -1;
            }

            if (markdown.Length - previous > 0)
            {
                sender.Inlines.Add(new Run { Text = markdown.Substring(previous, markdown.Length - previous) });
            }
        }
        #endregion

        #region Edited

        public static TLMessage GetEdited(DependencyObject obj)
        {
            return (TLMessage)obj.GetValue(EditedProperty);
        }

        public static void SetEdited(DependencyObject obj, TLMessage value)
        {
            obj.SetValue(EditedProperty, value);
        }

        public static readonly DependencyProperty EditedProperty =
            DependencyProperty.RegisterAttached("Edited", typeof(TLMessage), typeof(TextBlockHelper), new PropertyMetadata(null, OnEditedChanged));

        private static void OnEditedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as TextBlock;
            var newMessage = e.NewValue as TLMessage;
            var oldMessage = newMessage.Reply as TLMessage;

            var siteName = sender.Inlines[0] as Run;
            var description = sender.Inlines[2] as Run;

            if (newMessage.Media == null || (newMessage.Media is TLMessageMediaEmpty) || (newMessage.Media is TLMessageMediaWebPage) || !string.IsNullOrEmpty(newMessage.Message))
            {
                siteName.Text = Strings.Resources.EventLogOriginalMessages;
                description.Text = oldMessage.Message;
            }
            else if (oldMessage.Media is ITLMessageMediaCaption captionMedia)
            {
                siteName.Text = Strings.Resources.EventLogOriginalCaption;

                if (string.IsNullOrEmpty(captionMedia.Caption))
                {
                    description.Text = Strings.Resources.EventLogOriginalCaptionEmpty;
                }
                else
                {
                    description.Text = captionMedia.Caption;
                }
            }
        }

        #endregion
    }
}
