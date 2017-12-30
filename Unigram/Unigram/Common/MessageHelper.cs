using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Telegram.Api;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Template10.Common;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.Converters;
using Unigram.Views;
using Unigram.ViewModels;
using Unigram.Views.Users;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Unigram.Views.SignIn;
using Telegram.Api.Aggregator;
using Windows.Foundation;
using Windows.UI.Xaml.Input;
using Unigram.ViewModels.Dialogs;

namespace Unigram.Common
{
    public class MessageHelper
    {
        #region Message

        public static TLMessage GetMessage(DependencyObject obj)
        {
            return (TLMessage)obj.GetValue(MessageProperty);
        }

        public static void SetMessage(DependencyObject obj, TLMessage value)
        {
            // TODO: shitty hack!!!
            var oldValue = obj.GetValue(MessageProperty);
            obj.SetValue(MessageProperty, value);

            if (oldValue == value)
            {
                OnMessageChanged(obj as RichTextBlock, value);
            }
        }

        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.RegisterAttached("Message", typeof(TLMessage), typeof(MessageHelper), new PropertyMetadata(null, OnMessageChanged));

        private static void OnMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as RichTextBlock;
            var newValue = e.NewValue as TLMessage;

            OnMessageChanged(sender, newValue);
        }

        private static void OnMessageChanged(RichTextBlock sender, TLMessage newValue)
        {
            //sender.IsTextSelectionEnabled = false;
            var block = sender.Blocks[0] as Paragraph;
            var span = block.Inlines[0] as Span;

            var message = newValue as TLMessage;
            if (message != null /*&& sender.Visibility == Visibility.Visible*/)
            {
                var text = !string.IsNullOrWhiteSpace(message.Message);

                var caption = false;
                if (message.Media is ITLMessageMediaCaption captionMedia)
                {
                    caption = !string.IsNullOrWhiteSpace(captionMedia.Caption) && !message.IsRoundVideo();
                }
                else if (message.Media is TLMessageMediaVenue)
                {
                    caption = true;
                }

                var game = false;
                var notGame = true;
                if (message.Media is TLMessageMediaGame)
                {
                    game = sender.Tag != null;
                    notGame = false;
                }

                var notLive = true;
                if (message.Media is TLMessageMediaGeoLive)
                {
                    notLive = false;
                }

                var empty = false;
                if (message.Media is TLMessageMediaWebPage webpageMedia)
                {
                    empty = webpageMedia.WebPage is TLWebPageEmpty || webpageMedia.WebPage is TLWebPagePending;
                }

                sender.Visibility = (message.Media == null || /*message.Media is TLMessageMediaEmpty || message.Media is TLMessageMediaWebPage ||*/ game || caption || (text && notGame && notLive) ? Visibility.Visible : Visibility.Collapsed);
                if (sender.Visibility == Visibility.Collapsed)
                {
                    span.Inlines.Clear();
                    return;
                }

                var foreground = sender.Resources["MessageHyperlinkForegroundBrush"] as SolidColorBrush;

                var paragraph = new Span();

                if (message.HasEntities && message.Entities != null)
                {
                    ReplaceEntities(message, paragraph, foreground);
                }
                else
                {
                    if (message.HasEntities && message.Entities == null)
                    {
                        Debug.WriteLine("WARNING: this is weird!");
                    }

                    if (!string.IsNullOrWhiteSpace(message.Message))
                    {
                        paragraph.Inlines.Add(new Run { Text = message.Message });
                    }
                    else if (message.Media is TLMessageMediaVenue venueMedia)
                    {
                        paragraph.Inlines.Add(new Run { Text = venueMedia.Title, FontWeight = FontWeights.SemiBold });
                        paragraph.Inlines.Add(new LineBreak());
                        paragraph.Inlines.Add(new Run { Text = venueMedia.Address });
                    }
                    else if (game)
                    {
                        var gameMedia = message.Media as TLMessageMediaGame;
                        if (gameMedia != null)
                        {
                            Debug.WriteLine("WARNING: Using Regex to process message entities, considering it as a TLMessageMediaGame");
                            ReplaceAll(message, gameMedia.Game.Description, paragraph, sender.Foreground, true);
                        }
                    }
                    else if (caption)
                    {
                        var captionMedia2 = message.Media as ITLMessageMediaCaption;
                        if (captionMedia2 != null)
                        {
                            Debug.WriteLine("WARNING: Using Regex to process message entities, considering it as a ITLMediaCaption");
                            ReplaceAll(message, captionMedia2.Caption, paragraph, sender.Foreground, true);
                        }
                    }

                    //var text = message.Message;
                    //var captionMedia = message.Media as ITLMediaCaption;
                    //if (captionMedia != null && !string.IsNullOrWhiteSpace(captionMedia.Caption))
                    //{
                    //    text = captionMedia.Caption;
                    //}

                    //ReplaceAll(message, text, paragraph, sender.Foreground, true);
                }

                if (message?.Media is TLMessageMediaEmpty || message?.Media is ITLMessageMediaCaption || empty || message?.Media == null)
                {
                    if (IsAnyCharacterRightToLeft(message.Message ?? string.Empty))
                    {
                        paragraph.Inlines.Add(new LineBreak());
                    }
                    //else
                    {
                        //var date = BindConvert.Current.Date(message.Date);
                        //var placeholder = message.IsOut ? $"  {date}  " : $"  {date}";

                        //var bot = false;
                        //if (message.From != null)
                        //{
                        //    bot = message.From.IsBot;
                        //}

                        //if (message.HasEditDate && !message.HasViaBotId && !bot && !(message.ReplyMarkup is TLReplyInlineMarkup))
                        //{
                        //    placeholder = "edited" + placeholder;
                        //}

                        //if (message.HasViews)
                        //{
                        //    placeholder = "WS  " + (message.Views ?? 0) + placeholder;

                        //    if (message.HasFromId && message.From != null)
                        //    {
                        //        placeholder = (message.From.FullName + "  " ?? string.Empty) + placeholder;
                        //    }
                        //    else if (message.IsPost && message.HasPostAuthor && message.PostAuthor != null)
                        //    {
                        //        placeholder = (message.PostAuthor + "  " ?? string.Empty) + placeholder;
                        //    }
                        //    else if (message.HasFwdFrom && message.FwdFrom != null && message.FwdFrom.HasPostAuthor && message.FwdFrom.PostAuthor != null)
                        //    {
                        //        placeholder = (message.FwdFrom.PostAuthor + "  " ?? string.Empty) + placeholder;
                        //    }
                        //}

                        //paragraph.Inlines.Add(new Run { Text = "\u200E" + placeholder, Foreground = null, FontSize = 12 });
                    }
                }
                else
                {
                    paragraph.Inlines.Add(new Run { Text = " " });
                }

                span.Inlines.Clear();
                span.Inlines.Add(paragraph);
            }
            else
            {
                span.Inlines.Clear();
            }
        }

        #endregion

        #region IsFirst

        public static TLMessageBase GetHeader(DependencyObject obj)
        {
            return (TLMessageBase)obj.GetValue(HeaderProperty);
        }

        public static void SetHeader(DependencyObject obj, TLMessageBase value)
        {
            obj.SetValue(HeaderProperty, value);
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.RegisterAttached("Header", typeof(TLMessageBase), typeof(MessageHelper), new PropertyMetadata(false, OnHeaderChanged));

        private static void OnHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var paragraph = d as TextBlock;

            var message = e.NewValue as TLMessage;
            if (message != null)
            {
                //if (message.IsFirst && !message.IsOut && !message.IsPost && (message.ToId is TLPeerChat || message.ToId is TLPeerChannel))
                //{
                //    var hyperlink = new Hyperlink();
                //    hyperlink.Inlines.Add(new Run { Text = message.From?.FullName ?? string.Empty, Foreground = BindConvert.Current.Bubble(message.FromId ?? 0) });
                //    hyperlink.UnderlineStyle = UnderlineStyle.None;
                //    hyperlink.Foreground = paragraph.Foreground;
                //    //hyperlink.Click += (s, args) => From_Click(message);

                //    paragraph.Inlines.Add(hyperlink);
                //}
                //else if (message.IsPost && (message.ToId is TLPeerChat || message.ToId is TLPeerChannel))
                //{
                //    var hyperlink = new Hyperlink();
                //    hyperlink.Inlines.Add(new Run { Text = message.Parent?.DisplayName ?? string.Empty, Foreground = BindConvert.Current.Bubble(message.ToId.Id) });
                //    hyperlink.UnderlineStyle = UnderlineStyle.None;
                //    hyperlink.Foreground = paragraph.Foreground;
                //    //hyperlink.Click += (s, args) => From_Click(message);

                //    paragraph.Inlines.Add(hyperlink);
                //}

                if (message.HasFwdFrom && !message.IsSticker())
                {
                    if (paragraph.Inlines.Count > 0)
                        paragraph.Inlines.Add(new LineBreak());

                    paragraph.Inlines.Add(new Run { Text = "Forwarded from " });

                    var name = string.Empty;

                    var channel = message.FwdFromChannel;
                    if (channel != null)
                    {
                        name = channel.DisplayName;

                        if (message.FwdFrom.HasPostAuthor && message.FwdFrom.PostAuthor != null)
                        {
                            name += $" ({message.FwdFrom.PostAuthor})";
                        }
                    }

                    var user = message.FwdFromUser;
                    if (user != null)
                    {
                        if (name.Length > 0)
                        {
                            name += $" ({user.FullName})";
                        }
                        else
                        {
                            name = user.FullName;
                        }
                    }

                    var hyperlink = new Hyperlink();
                    hyperlink.Inlines.Add(new Run { Text = name });
                    hyperlink.UnderlineStyle = UnderlineStyle.None;
                    hyperlink.Foreground = paragraph.Foreground;
                    //hyperlink.Click += (s, args) => FwdFrom_Click(message);

                    paragraph.Inlines.Add(hyperlink);
                }

                if (message.HasViaBotId && message.ViaBot != null && !message.ViaBot.IsDeleted && message.ViaBot.HasUsername)
                {
                    var hyperlink = new Hyperlink();
                    hyperlink.Inlines.Add(new Run { Text = (paragraph.Inlines.Count > 0 ? " via @" : "via @") + message.ViaBot.Username });
                    hyperlink.UnderlineStyle = UnderlineStyle.None;
                    hyperlink.Foreground = paragraph.Foreground;
                    //hyperlink.Click += (s, args) => ViaBot_Click(message);

                    paragraph.Inlines.Add(hyperlink);
                }

                if (paragraph.Inlines.Count > 0)
                {
                    paragraph.Inlines.Add(new Run { Text = " " });
                    paragraph.Visibility = Visibility.Visible;
                }
                else
                {
                    paragraph.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                paragraph.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        #region Text
        public static string GetText(DependencyObject obj)
        {
            return (string)obj.GetValue(TextProperty);
        }

        public static void SetText(DependencyObject obj, string value)
        {
            obj.SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.RegisterAttached("Text", typeof(string), typeof(MessageHelper), new PropertyMetadata(null, OnTextChanged));

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as FrameworkElement;
            var newValue = e.NewValue as string;
            var oldValue = e.OldValue as string;

            //sender.IsTextSelectionEnabled = false;
            sender.Visibility = string.IsNullOrWhiteSpace(newValue) ? Visibility.Collapsed : Visibility.Visible;

            if (oldValue == newValue) return;

            var foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x0f, 0x7d, 0xc7));
            var paragraph = new Span();
            ReplaceAll(null, newValue, paragraph, foreground, true);

            if (sender is TextBlock textBlock)
            {
                textBlock.Inlines.Clear();
                textBlock.Inlines.Add(paragraph);
            }
            else if (sender is RichTextBlock richBlock)
            {
                var block = new Paragraph();
                block.Inlines.Add(paragraph);
                richBlock.Blocks.Clear();
                richBlock.Blocks.Add(block);
            }
        }
        #endregion

        private static bool IsAnyCharacterRightToLeft(string s)
        {
            //if (s.Length > 2)
            //{
            //    s = s.Substring(s.Length - 2);
            //}

            for (int i = 0; i < s.Length; i += char.IsSurrogatePair(s, i) ? 2 : 1)
            {
                var codepoint = char.ConvertToUtf32(s, i);
                if (IsRandALCat(codepoint))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsRandALCat(int c)
        {
            bool hasRandALCat = false;
            if (c >= 0x5BE && c <= 0x10B7F)
            {
                if (c <= 0x85E)
                {
                    if (c == 0x5BE) hasRandALCat = true;
                    else if (c == 0x5C0) hasRandALCat = true;
                    else if (c == 0x5C3) hasRandALCat = true;
                    else if (c == 0x5C6) hasRandALCat = true;
                    else if (0x5D0 <= c && c <= 0x5EA) hasRandALCat = true;
                    else if (0x5F0 <= c && c <= 0x5F4) hasRandALCat = true;
                    else if (c == 0x608) hasRandALCat = true;
                    else if (c == 0x60B) hasRandALCat = true;
                    else if (c == 0x60D) hasRandALCat = true;
                    else if (c == 0x61B) hasRandALCat = true;
                    else if (0x61E <= c && c <= 0x64A) hasRandALCat = true;
                    else if (0x66D <= c && c <= 0x66F) hasRandALCat = true;
                    else if (0x671 <= c && c <= 0x6D5) hasRandALCat = true;
                    else if (0x6E5 <= c && c <= 0x6E6) hasRandALCat = true;
                    else if (0x6EE <= c && c <= 0x6EF) hasRandALCat = true;
                    else if (0x6FA <= c && c <= 0x70D) hasRandALCat = true;
                    else if (c == 0x710) hasRandALCat = true;
                    else if (0x712 <= c && c <= 0x72F) hasRandALCat = true;
                    else if (0x74D <= c && c <= 0x7A5) hasRandALCat = true;
                    else if (c == 0x7B1) hasRandALCat = true;
                    else if (0x7C0 <= c && c <= 0x7EA) hasRandALCat = true;
                    else if (0x7F4 <= c && c <= 0x7F5) hasRandALCat = true;
                    else if (c == 0x7FA) hasRandALCat = true;
                    else if (0x800 <= c && c <= 0x815) hasRandALCat = true;
                    else if (c == 0x81A) hasRandALCat = true;
                    else if (c == 0x824) hasRandALCat = true;
                    else if (c == 0x828) hasRandALCat = true;
                    else if (0x830 <= c && c <= 0x83E) hasRandALCat = true;
                    else if (0x840 <= c && c <= 0x858) hasRandALCat = true;
                    else if (c == 0x85E) hasRandALCat = true;
                }
                else if (c == 0x200F) hasRandALCat = true;
                else if (c >= 0xFB1D)
                {
                    if (c == 0xFB1D) hasRandALCat = true;
                    else if (0xFB1F <= c && c <= 0xFB28) hasRandALCat = true;
                    else if (0xFB2A <= c && c <= 0xFB36) hasRandALCat = true;
                    else if (0xFB38 <= c && c <= 0xFB3C) hasRandALCat = true;
                    else if (c == 0xFB3E) hasRandALCat = true;
                    else if (0xFB40 <= c && c <= 0xFB41) hasRandALCat = true;
                    else if (0xFB43 <= c && c <= 0xFB44) hasRandALCat = true;
                    else if (0xFB46 <= c && c <= 0xFBC1) hasRandALCat = true;
                    else if (0xFBD3 <= c && c <= 0xFD3D) hasRandALCat = true;
                    else if (0xFD50 <= c && c <= 0xFD8F) hasRandALCat = true;
                    else if (0xFD92 <= c && c <= 0xFDC7) hasRandALCat = true;
                    else if (0xFDF0 <= c && c <= 0xFDFC) hasRandALCat = true;
                    else if (0xFE70 <= c && c <= 0xFE74) hasRandALCat = true;
                    else if (0xFE76 <= c && c <= 0xFEFC) hasRandALCat = true;
                    else if (0x10800 <= c && c <= 0x10805) hasRandALCat = true;
                    else if (c == 0x10808) hasRandALCat = true;
                    else if (0x1080A <= c && c <= 0x10835) hasRandALCat = true;
                    else if (0x10837 <= c && c <= 0x10838) hasRandALCat = true;
                    else if (c == 0x1083C) hasRandALCat = true;
                    else if (0x1083F <= c && c <= 0x10855) hasRandALCat = true;
                    else if (0x10857 <= c && c <= 0x1085F) hasRandALCat = true;
                    else if (0x10900 <= c && c <= 0x1091B) hasRandALCat = true;
                    else if (0x10920 <= c && c <= 0x10939) hasRandALCat = true;
                    else if (c == 0x1093F) hasRandALCat = true;
                    else if (c == 0x10A00) hasRandALCat = true;
                    else if (0x10A10 <= c && c <= 0x10A13) hasRandALCat = true;
                    else if (0x10A15 <= c && c <= 0x10A17) hasRandALCat = true;
                    else if (0x10A19 <= c && c <= 0x10A33) hasRandALCat = true;
                    else if (0x10A40 <= c && c <= 0x10A47) hasRandALCat = true;
                    else if (0x10A50 <= c && c <= 0x10A58) hasRandALCat = true;
                    else if (0x10A60 <= c && c <= 0x10A7F) hasRandALCat = true;
                    else if (0x10B00 <= c && c <= 0x10B35) hasRandALCat = true;
                    else if (0x10B40 <= c && c <= 0x10B55) hasRandALCat = true;
                    else if (0x10B58 <= c && c <= 0x10B72) hasRandALCat = true;
                    else if (0x10B78 <= c && c <= 0x10B7F) hasRandALCat = true;
                }
            }

            return hasRandALCat;
        }

        public static readonly Regex EmojiRegex;
        public static readonly Regex AllRegex;

        static MessageHelper()
        {
            var emoji = "(?:\ud83d\udc68‍\ud83d\udc68‍\ud83d\udc66‍\ud83d\udc66|\ud83d\udc68‍\ud83d\udc68‍\ud83d\udc67‍\ud83d\udc66|\ud83d\udc68‍\ud83d\udc68‍\ud83d\udc67‍\ud83d\udc67|\ud83d\udc68‍\ud83d\udc69‍\ud83d\udc66‍\ud83d\udc66|\ud83d\udc68‍\ud83d\udc69‍\ud83d\udc67‍\ud83d\udc66|\ud83d\udc68‍\ud83d\udc69‍\ud83d\udc67‍\ud83d\udc67|\ud83d\udc69‍\ud83d\udc69‍\ud83d\udc66‍\ud83d\udc66|\ud83d\udc69‍\ud83d\udc69‍\ud83d\udc67‍\ud83d\udc66|\ud83d\udc69‍\ud83d\udc69‍\ud83d\udc67‍\ud83d\udc67|\ud83d\udc68‍❤️‍\ud83d\udc8b‍\ud83d\udc68|\ud83d\udc69‍❤️‍\ud83d\udc8b‍\ud83d\udc69|\ud83d\udc68‍\ud83d\udc68‍\ud83d\udc66|\ud83d\udc68‍\ud83d\udc68‍\ud83d\udc67|\ud83d\udc68‍\ud83d\udc69‍\ud83d\udc67|\ud83d\udc69‍\ud83d\udc69‍\ud83d\udc66|\ud83d\udc69‍\ud83d\udc69‍\ud83d\udc67|\ud83d\udc68‍❤️‍\ud83d\udc68|\ud83d\udc69‍❤️‍\ud83d\udc69|\ud83c\udde6\ud83c\uddff|\ud83c\udde6\ud83c\uddfa|\ud83c\udde6\ud83c\uddfc|\ud83c\udde6\ud83c\uddf4|\ud83c\udde6\ud83c\uddf1|\ud83c\udde6\ud83c\uddf2|\ud83c\udde6\ud83c\uddf8|\ud83c\udde6\ud83c\uddf9|\ud83c\udde6\ud83c\uddea|\ud83c\udde6\ud83c\uddec|\ud83c\udde6\ud83c\uddeb|\ud83c\udde6\ud83c\uddee|\ud83c\udde6\ud83c\udde9|\ud83c\udde6\ud83c\uddf7|\ud83c\udde7\ud83c\uddf4|\ud83c\udde7\ud83c\uddf7|\ud83c\udde7\ud83c\uddf3|\ud83c\udde7\ud83c\uddf2|\ud83c\udde7\ud83c\uddf9|\ud83c\udde7\ud83c\uddf8|\ud83c\udde7\ud83c\uddfe|\ud83c\udde7\ud83c\uddff|\ud83c\udde7\ud83c\uddfc|\ud83c\udde7\ud83c\uddea|\ud83c\udde7\ud83c\uddeb|\ud83c\udde7\ud83c\uddec|\ud83c\udde7\ud83c\udded|\ud83c\udde7\ud83c\uddee|\ud83c\udde7\ud83c\uddef|\ud83c\udde7\ud83c\udde6|\ud83c\udde7\ud83c\udde7|\ud83c\udde7\ud83c\udde9|\ud83c\udde8\ud83c\uddfb|\ud83c\udde8\ud83c\uddfc|\ud83c\udde8\ud83c\uddfa|\ud83c\udde8\ud83c\uddff|\ud83c\udde8\ud83c\uddfe|\ud83c\udde8\ud83c\uddf2|\ud83c\udde8\ud83c\uddf3|\ud83c\udde8\ud83c\uddf0|\ud83c\udde8\ud83c\uddf1|\ud83c\udde8\ud83c\uddf7|\ud83c\udde8\ud83c\uddf4|\ud83c\udde8\ud83c\udde9|\ud83c\udde8\ud83c\udde6|\ud83c\udde8\ud83c\uddec|\ud83c\udde8\ud83c\uddeb|\ud83c\udde8\ud83c\uddee|\ud83c\udde8\ud83c\udded|\ud83c\udde9\ud83c\uddea|\ud83c\udde9\ud83c\uddef|\ud83c\udde9\ud83c\uddf2|\ud83c\udde9\ud83c\uddf0|\ud83c\udde9\ud83c\uddf4|\ud83c\udde9\ud83c\uddff|\ud83c\uddea\ud83c\uddf9|\ud83c\uddea\ud83c\uddf8|\ud83c\uddea\ud83c\uddf7|\ud83c\uddea\ud83c\uddec|\ud83c\uddea\ud83c\uddea|\ud83c\uddea\ud83c\udde8|\ud83c\uddeb\ud83c\uddf4|\ud83c\uddeb\ud83c\uddf7|\ud83c\uddeb\ud83c\uddee|\ud83c\uddeb\ud83c\uddef|\ud83c\uddec\ud83c\uddf9|\ud83c\uddec\ud83c\uddf3|\ud83c\uddec\ud83c\uddf2|\ud83c\uddec\ud83c\uddf5|\ud83c\uddec\ud83c\uddf7|\ud83c\uddec\ud83c\uddf6|\ud83c\uddec\ud83c\uddfa|\ud83c\uddec\ud83c\uddfc|\ud83c\uddec\ud83c\uddfe|\ud83c\uddec\ud83c\uddeb|\ud83c\uddec\ud83c\udded|\ud83c\uddec\ud83c\uddee|\ud83c\uddec\ud83c\uddea|\ud83c\uddec\ud83c\udde9|\ud83c\uddec\ud83c\udde6|\ud83c\uddec\ud83c\udde7|\ud83c\udded\ud83c\uddfa|\ud83c\udded\ud83c\uddf7|\ud83c\udded\ud83c\uddf3|\ud83c\udded\ud83c\uddf0|\ud83c\udded\ud83c\uddf9|\ud83c\uddee\ud83c\uddea|\ud83c\uddee\ud83c\udde9|\ud83c\uddee\ud83c\uddf7|\ud83c\uddee\ud83c\uddf6|\ud83c\uddee\ud83c\uddf3|\ud83c\uddee\ud83c\uddf1|\ud83c\uddee\ud83c\uddf9|\ud83c\uddee\ud83c\uddf8|\ud83c\uddef\ud83c\uddf4|\ud83c\uddef\ud83c\uddf5|\ud83c\uddef\ud83c\uddf2|\ud83c\uddf0\ud83c\uddec|\ud83c\uddf0\ud83c\uddea|\ud83c\uddf0\ud83c\udded|\ud83c\uddf0\ud83c\uddee|\ud83c\uddf0\ud83c\uddfc|\ud83c\uddf0\ud83c\uddff|\ud83c\uddf0\ud83c\uddfe|\ud83c\uddf0\ud83c\uddf3|\ud83c\uddf0\ud83c\uddf2|\ud83c\uddf0\ud83c\uddf7|\ud83c\uddf0\ud83c\uddf5|\ud83c\uddf1\ud83c\uddf0|\ud83c\uddf1\ud83c\uddf7|\ud83c\uddf1\ud83c\uddf8|\ud83c\uddf1\ud83c\uddf9|\ud83c\uddf1\ud83c\uddfb|\ud83c\uddf1\ud83c\uddfa|\ud83c\uddf1\ud83c\uddfe|\ud83c\uddf1\ud83c\uddee|\ud83c\uddf1\ud83c\udde8|\ud83c\uddf1\ud83c\udde7|\ud83c\uddf1\ud83c\udde6|\ud83c\uddf2\ud83c\uddfc|\ud83c\uddf2\ud83c\uddfb|\ud83c\uddf2\ud83c\uddf4|\ud83c\uddf2\ud83c\uddea|\ud83c\uddf2\ud83c\uddec|\ud83c\uddf2\ud83c\udde9|\ud83c\uddf2\ud83c\udde6|\ud83c\uddf2\ud83c\uddf3|\ud83c\uddf2\ud83c\uddf2|\ud83c\uddf2\ud83c\uddf7|\ud83c\uddf2\ud83c\uddf6|\ud83c\uddf2\ud83c\uddf9|\ud83c\uddf2\ud83c\uddf8|\ud83c\uddf2\ud83c\uddfe|\ud83c\uddf2\ud83c\uddfd|\ud83c\uddf2\ud83c\uddff|\ud83c\uddf2\ud83c\uddf1|\ud83c\uddf2\ud83c\uddf0|\ud83c\uddf2\ud83c\uddf5|\ud83c\uddf3\ud83c\uddee|\ud83c\uddf3\ud83c\udde6|\ud83c\uddf3\ud83c\uddf1|\ud83c\uddf3\ud83c\uddf4|\ud83c\uddf3\ud83c\uddf5|\ud83c\uddf3\ud83c\uddff|\ud83c\uddf3\ud83c\uddfa|\ud83c\uddf3\ud83c\uddea|\ud83c\uddf3\ud83c\udde8|\ud83c\uddf3\ud83c\uddec|\ud83c\uddf4\ud83c\uddf2|\ud83c\uddf5\ud83c\uddf8|\ud83c\uddf5\ud83c\uddf0|\ud83c\uddf5\ud83c\uddfe|\ud83c\uddf5\ud83c\uddfc|\ud83c\uddf5\ud83c\udded|\ud83c\uddf5\ud83c\uddec|\ud83c\uddf5\ud83c\uddea|\ud83c\uddf5\ud83c\udde6|\ud83c\uddf5\ud83c\uddf7|\ud83c\uddf5\ud83c\uddf9|\ud83c\uddf5\ud83c\uddf1|\ud83c\uddf6\ud83c\udde6|\ud83c\uddf7\ud83c\uddea|\ud83c\uddf7\ud83c\uddf8|\ud83c\uddf7\ud83c\uddf4|\ud83c\uddf7\ud83c\uddfa|\ud83c\uddf7\ud83c\uddfc|\ud83c\uddf8\ud83c\uddf2|\ud83c\uddf8\ud83c\udde6|\ud83c\uddf8\ud83c\udde7|\ud83c\uddf8\ud83c\udde8|\ud83c\uddf8\ud83c\udde9|\ud83c\uddf8\ud83c\uddec|\ud83c\uddf8\ud83c\uddea|\ud83c\uddf8\ud83c\uddee|\ud83c\uddf8\ud83c\uddfb|\ud83c\uddf8\ud83c\uddff|\ud83c\uddf8\ud83c\uddfe|\ud83c\uddf8\ud83c\uddfd|\ud83c\uddf8\ud83c\uddf9|\ud83c\uddf8\ud83c\uddf8|\ud83c\uddf8\ud83c\uddf3|\ud83c\uddf8\ud83c\uddf1|\ud83c\uddf8\ud83c\uddf0|\ud83c\uddf8\ud83c\uddf7|\ud83c\uddf8\ud83c\uddf4|\ud83c\uddf9\ud83c\uddf4|\ud83c\uddf9\ud83c\uddff|\ud83c\uddf9\ud83c\uddf9|\ud83c\uddf9\ud83c\uddf2|\ud83c\uddf9\ud83c\uddf3|\ud83c\uddf9\ud83c\uddf1|\ud83c\uddf9\ud83c\uddf7|\ud83c\uddf9\ud83c\uddfb|\ud83c\uddf9\ud83c\uddec|\ud83c\uddf9\ud83c\uddeb|\ud83c\uddf9\ud83c\uddef|\ud83c\uddf9\ud83c\udded|\ud83c\uddf9\ud83c\udde8|\ud83c\uddfa\ud83c\uddf8|\ud83c\uddfa\ud83c\uddff|\ud83c\uddfa\ud83c\uddfe|\ud83c\uddfa\ud83c\uddec|\ud83c\uddfa\ud83c\udde6|\ud83c\uddfb\ud83c\uddfa|\ud83c\uddfb\ud83c\uddf3|\ud83c\uddfb\ud83c\udde8|\ud83c\uddfb\ud83c\uddea|\ud83c\uddfb\ud83c\uddec|\ud83c\uddfb\ud83c\uddee|\ud83c\uddfc\ud83c\uddf8|\ud83c\uddfe\ud83c\uddea|\ud83c\uddff\ud83c\udde6|\ud83c\uddff\ud83c\uddfc|\ud83c\uddff\ud83c\uddf2|\ud83c\udf85\ud83c\udffb|\ud83c\udf85\ud83c\udffc|\ud83c\udf85\ud83c\udffd|\ud83c\udf85\ud83c\udffe|\ud83c\udf85\ud83c\udfff|\ud83c\udfc3\ud83c\udffb|\ud83c\udfc3\ud83c\udffc|\ud83c\udfc3\ud83c\udffd|\ud83c\udfc3\ud83c\udffe|\ud83c\udfc3\ud83c\udfff|\ud83c\udfc4\ud83c\udffb|\ud83c\udfc4\ud83c\udffc|\ud83c\udfc4\ud83c\udffd|\ud83c\udfc4\ud83c\udffe|\ud83c\udfc4\ud83c\udfff|\ud83c\udfc7\ud83c\udffb|\ud83c\udfc7\ud83c\udffc|\ud83c\udfc7\ud83c\udffd|\ud83c\udfc7\ud83c\udffe|\ud83c\udfc7\ud83c\udfff|\ud83c\udfca\ud83c\udffb|\ud83c\udfca\ud83c\udffc|\ud83c\udfca\ud83c\udffd|\ud83c\udfca\ud83c\udffe|\ud83c\udfca\ud83c\udfff|\ud83d\udc42\ud83c\udffb|\ud83d\udc42\ud83c\udffc|\ud83d\udc42\ud83c\udffd|\ud83d\udc42\ud83c\udffe|\ud83d\udc42\ud83c\udfff|\ud83d\udc43\ud83c\udffb|\ud83d\udc43\ud83c\udffc|\ud83d\udc43\ud83c\udffd|\ud83d\udc43\ud83c\udffe|\ud83d\udc43\ud83c\udfff|\ud83d\udc46\ud83c\udffb|\ud83d\udc46\ud83c\udffc|\ud83d\udc46\ud83c\udffd|\ud83d\udc46\ud83c\udffe|\ud83d\udc46\ud83c\udfff|\ud83d\udc47\ud83c\udffb|\ud83d\udc47\ud83c\udffc|\ud83d\udc47\ud83c\udffd|\ud83d\udc47\ud83c\udffe|\ud83d\udc47\ud83c\udfff|\ud83d\udc48\ud83c\udffb|\ud83d\udc48\ud83c\udffc|\ud83d\udc48\ud83c\udffd|\ud83d\udc48\ud83c\udffe|\ud83d\udc48\ud83c\udfff|\ud83d\udc49\ud83c\udffb|\ud83d\udc49\ud83c\udffc|\ud83d\udc49\ud83c\udffd|\ud83d\udc49\ud83c\udffe|\ud83d\udc49\ud83c\udfff|\ud83d\udc4a\ud83c\udffb|\ud83d\udc4a\ud83c\udffc|\ud83d\udc4a\ud83c\udffd|\ud83d\udc4a\ud83c\udffe|\ud83d\udc4a\ud83c\udfff|\ud83d\udc4b\ud83c\udffb|\ud83d\udc4b\ud83c\udffc|\ud83d\udc4b\ud83c\udffd|\ud83d\udc4b\ud83c\udffe|\ud83d\udc4b\ud83c\udfff|\ud83d\udc4c\ud83c\udffb|\ud83d\udc4c\ud83c\udffc|\ud83d\udc4c\ud83c\udffd|\ud83d\udc4c\ud83c\udffe|\ud83d\udc4c\ud83c\udfff|\ud83d\udc4d\ud83c\udffb|\ud83d\udc4d\ud83c\udffc|\ud83d\udc4d\ud83c\udffd|\ud83d\udc4d\ud83c\udffe|\ud83d\udc4d\ud83c\udfff|\ud83d\udc4e\ud83c\udffb|\ud83d\udc4e\ud83c\udffc|\ud83d\udc4e\ud83c\udffd|\ud83d\udc4e\ud83c\udffe|\ud83d\udc4e\ud83c\udfff|\ud83d\udc4f\ud83c\udffb|\ud83d\udc4f\ud83c\udffc|\ud83d\udc4f\ud83c\udffd|\ud83d\udc4f\ud83c\udffe|\ud83d\udc4f\ud83c\udfff|\ud83d\udc50\ud83c\udffb|\ud83d\udc50\ud83c\udffc|\ud83d\udc50\ud83c\udffd|\ud83d\udc50\ud83c\udffe|\ud83d\udc50\ud83c\udfff|\ud83d\udc66\ud83c\udffb|\ud83d\udc66\ud83c\udffc|\ud83d\udc66\ud83c\udffd|\ud83d\udc66\ud83c\udffe|\ud83d\udc66\ud83c\udfff|\ud83d\udc67\ud83c\udffb|\ud83d\udc67\ud83c\udffc|\ud83d\udc67\ud83c\udffd|\ud83d\udc67\ud83c\udffe|\ud83d\udc67\ud83c\udfff|\ud83d\udc68\ud83c\udffb|\ud83d\udc68\ud83c\udffc|\ud83d\udc68\ud83c\udffd|\ud83d\udc68\ud83c\udffe|\ud83d\udc68\ud83c\udfff|\ud83d\udc69\ud83c\udffb|\ud83d\udc69\ud83c\udffc|\ud83d\udc69\ud83c\udffd|\ud83d\udc69\ud83c\udffe|\ud83d\udc69\ud83c\udfff|\ud83d\udc6e\ud83c\udffb|\ud83d\udc6e\ud83c\udffc|\ud83d\udc6e\ud83c\udffd|\ud83d\udc6e\ud83c\udffe|\ud83d\udc6e\ud83c\udfff|\ud83d\udc70\ud83c\udffb|\ud83d\udc70\ud83c\udffc|\ud83d\udc70\ud83c\udffd|\ud83d\udc70\ud83c\udffe|\ud83d\udc70\ud83c\udfff|\ud83d\udc71\ud83c\udffb|\ud83d\udc71\ud83c\udffc|\ud83d\udc71\ud83c\udffd|\ud83d\udc71\ud83c\udffe|\ud83d\udc71\ud83c\udfff|\ud83d\udc72\ud83c\udffb|\ud83d\udc72\ud83c\udffc|\ud83d\udc72\ud83c\udffd|\ud83d\udc72\ud83c\udffe|\ud83d\udc72\ud83c\udfff|\ud83d\udc73\ud83c\udffb|\ud83d\udc73\ud83c\udffc|\ud83d\udc73\ud83c\udffd|\ud83d\udc73\ud83c\udffe|\ud83d\udc73\ud83c\udfff|\ud83d\udc74\ud83c\udffb|\ud83d\udc74\ud83c\udffc|\ud83d\udc74\ud83c\udffd|\ud83d\udc74\ud83c\udffe|\ud83d\udc74\ud83c\udfff|\ud83d\udc75\ud83c\udffb|\ud83d\udc75\ud83c\udffc|\ud83d\udc75\ud83c\udffd|\ud83d\udc75\ud83c\udffe|\ud83d\udc75\ud83c\udfff|\ud83d\udc76\ud83c\udffb|\ud83d\udc76\ud83c\udffc|\ud83d\udc76\ud83c\udffd|\ud83d\udc76\ud83c\udffe|\ud83d\udc76\ud83c\udfff|\ud83d\udc77\ud83c\udffb|\ud83d\udc77\ud83c\udffc|\ud83d\udc77\ud83c\udffd|\ud83d\udc77\ud83c\udffe|\ud83d\udc77\ud83c\udfff|\ud83d\udc78\ud83c\udffb|\ud83d\udc78\ud83c\udffc|\ud83d\udc78\ud83c\udffd|\ud83d\udc78\ud83c\udffe|\ud83d\udc78\ud83c\udfff|\ud83d\udc7c\ud83c\udffb|\ud83d\udc7c\ud83c\udffc|\ud83d\udc7c\ud83c\udffd|\ud83d\udc7c\ud83c\udffe|\ud83d\udc7c\ud83c\udfff|\ud83d\udc81\ud83c\udffb|\ud83d\udc81\ud83c\udffc|\ud83d\udc81\ud83c\udffd|\ud83d\udc81\ud83c\udffe|\ud83d\udc81\ud83c\udfff|\ud83d\udc82\ud83c\udffb|\ud83d\udc82\ud83c\udffc|\ud83d\udc82\ud83c\udffd|\ud83d\udc82\ud83c\udffe|\ud83d\udc82\ud83c\udfff|\ud83d\udc83\ud83c\udffb|\ud83d\udc83\ud83c\udffc|\ud83d\udc83\ud83c\udffd|\ud83d\udc83\ud83c\udffe|\ud83d\udc83\ud83c\udfff|\ud83d\udc85\ud83c\udffb|\ud83d\udc85\ud83c\udffc|\ud83d\udc85\ud83c\udffd|\ud83d\udc85\ud83c\udffe|\ud83d\udc85\ud83c\udfff|\ud83d\udc86\ud83c\udffb|\ud83d\udc86\ud83c\udffc|\ud83d\udc86\ud83c\udffd|\ud83d\udc86\ud83c\udffe|\ud83d\udc86\ud83c\udfff|\ud83d\udc87\ud83c\udffb|\ud83d\udc87\ud83c\udffc|\ud83d\udc87\ud83c\udffd|\ud83d\udc87\ud83c\udffe|\ud83d\udc87\ud83c\udfff|\ud83d\udcaa\ud83c\udffb|\ud83d\udcaa\ud83c\udffc|\ud83d\udcaa\ud83c\udffd|\ud83d\udcaa\ud83c\udffe|\ud83d\udcaa\ud83c\udfff|\ud83d\udd90\ud83c\udffb|\ud83d\udd90\ud83c\udffc|\ud83d\udd90\ud83c\udffd|\ud83d\udd90\ud83c\udffe|\ud83d\udd90\ud83c\udfff|\ud83d\udd95\ud83c\udffb|\ud83d\udd95\ud83c\udffc|\ud83d\udd95\ud83c\udffd|\ud83d\udd95\ud83c\udffe|\ud83d\udd95\ud83c\udfff|\ud83d\udd96\ud83c\udffb|\ud83d\udd96\ud83c\udffc|\ud83d\udd96\ud83c\udffd|\ud83d\udd96\ud83c\udffe|\ud83d\udd96\ud83c\udfff|\ud83e\udd18\ud83c\udffb|\ud83e\udd18\ud83c\udffc|\ud83e\udd18\ud83c\udffd|\ud83e\udd18\ud83c\udffe|\ud83e\udd18\ud83c\udfff|\ud83d\ude45\ud83c\udffb|\ud83d\ude45\ud83c\udffc|\ud83d\ude45\ud83c\udffd|\ud83d\ude45\ud83c\udffe|\ud83d\ude45\ud83c\udfff|\ud83d\ude46\ud83c\udffb|\ud83d\ude46\ud83c\udffc|\ud83d\ude46\ud83c\udffd|\ud83d\ude46\ud83c\udffe|\ud83d\ude46\ud83c\udfff|\ud83d\ude47\ud83c\udffb|\ud83d\ude47\ud83c\udffc|\ud83d\ude47\ud83c\udffd|\ud83d\ude47\ud83c\udffe|\ud83d\ude47\ud83c\udfff|\ud83d\ude4b\ud83c\udffb|\ud83d\ude4b\ud83c\udffc|\ud83d\ude4b\ud83c\udffd|\ud83d\ude4b\ud83c\udffe|\ud83d\ude4b\ud83c\udfff|\ud83d\ude4c\ud83c\udffb|\ud83d\ude4c\ud83c\udffc|\ud83d\ude4c\ud83c\udffd|\ud83d\ude4c\ud83c\udffe|\ud83d\ude4c\ud83c\udfff|\ud83d\ude4d\ud83c\udffb|\ud83d\ude4d\ud83c\udffc|\ud83d\ude4d\ud83c\udffd|\ud83d\ude4d\ud83c\udffe|\ud83d\ude4d\ud83c\udfff|\ud83d\ude4e\ud83c\udffb|\ud83d\ude4e\ud83c\udffc|\ud83d\ude4e\ud83c\udffd|\ud83d\ude4e\ud83c\udffe|\ud83d\ude4e\ud83c\udfff|\ud83d\ude4f\ud83c\udffb|\ud83d\ude4f\ud83c\udffc|\ud83d\ude4f\ud83c\udffd|\ud83d\ude4f\ud83c\udffe|\ud83d\ude4f\ud83c\udfff|\ud83d\udea3\ud83c\udffb|\ud83d\udea3\ud83c\udffc|\ud83d\udea3\ud83c\udffd|\ud83d\udea3\ud83c\udffe|\ud83d\udea3\ud83c\udfff|\ud83d\udeb4\ud83c\udffb|\ud83d\udeb4\ud83c\udffc|\ud83d\udeb4\ud83c\udffd|\ud83d\udeb4\ud83c\udffe|\ud83d\udeb4\ud83c\udfff|\ud83d\udeb5\ud83c\udffb|\ud83d\udeb5\ud83c\udffc|\ud83d\udeb5\ud83c\udffd|\ud83d\udeb5\ud83c\udffe|\ud83d\udeb5\ud83c\udfff|\ud83d\udeb6\ud83c\udffb|\ud83d\udeb6\ud83c\udffc|\ud83d\udeb6\ud83c\udffd|\ud83d\udeb6\ud83c\udffe|\ud83d\udeb6\ud83c\udfff|\ud83d\udec0\ud83c\udffb|\ud83d\udec0\ud83c\udffc|\ud83d\udec0\ud83c\udffd|\ud83d\udec0\ud83c\udffe|\ud83d\udec0\ud83c\udfff|\ud83c\uddf9\ud83c\uddfc|\ud83c\uddfd\ud83c\uddea|☝\ud83c\udffb|☝\ud83c\udffc|☝\ud83c\udffd|☝\ud83c\udffe|☝\ud83c\udfff|✊\ud83c\udffb|✊\ud83c\udffc|✊\ud83c\udffd|✊\ud83c\udffe|✊\ud83c\udfff|✋\ud83c\udffb|✋\ud83c\udffc|✋\ud83c\udffd|✋\ud83c\udffe|✋\ud83c\udfff|✌\ud83c\udffb|✌\ud83c\udffc|✌\ud83c\udffd|✌\ud83c\udffe|✌\ud83c\udfff|\ud83c\udc04|\ud83c\udccf|\ud83c\udd70|\ud83c\udd71|\ud83c\udd7e|\ud83c\udd7f|\ud83c\udd8e|\ud83c\udd91|\ud83c\udd92|\ud83c\udd93|\ud83c\udd94|\ud83c\udd95|\ud83c\udd96|\ud83c\udd97|\ud83c\udd98|\ud83c\udd99|\ud83c\udd9a|\ud83c\ude01|\ud83c\ude02|\ud83c\ude1a|\ud83c\ude2f|\ud83c\ude32|\ud83c\ude33|\ud83c\ude34|\ud83c\ude35|\ud83c\ude36|\ud83c\ude37|\ud83c\ude38|\ud83c\ude39|\ud83c\ude3a|\ud83c\ude50|\ud83c\ude51|\ud83c\udf00|\ud83c\udf01|\ud83c\udf02|\ud83c\udf03|\ud83c\udf04|\ud83c\udf05|\ud83c\udf06|\ud83c\udf07|\ud83c\udf08|\ud83c\udf09|\ud83c\udf0a|\ud83c\udf0b|\ud83c\udf0c|\ud83c\udf0d|\ud83c\udf0e|\ud83c\udf0f|\ud83c\udf10|\ud83c\udf11|\ud83c\udf12|\ud83c\udf13|\ud83c\udf14|\ud83c\udf15|\ud83c\udf16|\ud83c\udf17|\ud83c\udf18|\ud83c\udf19|\ud83c\udf1a|\ud83c\udf1b|\ud83c\udf1c|\ud83c\udf1d|\ud83c\udf1e|\ud83c\udf1f|\ud83c\udf20|\ud83c\udf30|\ud83c\udf31|\ud83c\udf32|\ud83c\udf33|\ud83c\udf34|\ud83c\udf35|\ud83c\udf37|\ud83c\udf38|\ud83c\udf39|\ud83c\udf3a|\ud83c\udf3b|\ud83c\udf3c|\ud83c\udf3d|\ud83c\udf3e|\ud83c\udf3f|\ud83c\udf40|\ud83c\udf41|\ud83c\udf42|\ud83c\udf43|\ud83c\udf44|\ud83c\udf45|\ud83c\udf46|\ud83c\udf47|\ud83c\udf48|\ud83c\udf49|\ud83c\udf4a|\ud83c\udf4b|\ud83c\udf4c|\ud83c\udf4d|\ud83c\udf4e|\ud83c\udf4f|\ud83c\udf50|\ud83c\udf51|\ud83c\udf52|\ud83c\udf53|\ud83c\udf54|\ud83c\udf55|\ud83c\udf56|\ud83c\udf57|\ud83c\udf58|\ud83c\udf59|\ud83c\udf5a|\ud83c\udf5b|\ud83c\udf5c|\ud83c\udf5d|\ud83c\udf5e|\ud83c\udf5f|\ud83c\udf60|\ud83c\udf61|\ud83c\udf62|\ud83c\udf63|\ud83c\udf64|\ud83c\udf65|\ud83c\udf66|\ud83c\udf67|\ud83c\udf68|\ud83c\udf69|\ud83c\udf6a|\ud83c\udf6b|\ud83c\udf6c|\ud83c\udf6d|\ud83c\udf6e|\ud83c\udf6f|\ud83c\udf70|\ud83c\udf71|\ud83c\udf72|\ud83c\udf73|\ud83c\udf74|\ud83c\udf75|\ud83c\udf76|\ud83c\udf77|\ud83c\udf78|\ud83c\udf79|\ud83c\udf7a|\ud83c\udf7b|\ud83c\udf7c|\ud83c\udf80|\ud83c\udf81|\ud83c\udf82|\ud83c\udf83|\ud83c\udf84|\ud83c\udf85|\ud83c\udf86|\ud83c\udf87|\ud83c\udf88|\ud83c\udf89|\ud83c\udf8a|\ud83c\udf8b|\ud83c\udf8c|\ud83c\udf8d|\ud83c\udf8e|\ud83c\udf8f|\ud83c\udf90|\ud83c\udf91|\ud83c\udf92|\ud83c\udf93|\ud83c\udfa0|\ud83c\udfa1|\ud83c\udfa2|\ud83c\udfa3|\ud83c\udfa4|\ud83c\udfa5|\ud83c\udfa6|\ud83c\udfa7|\ud83c\udfa8|\ud83c\udfa9|\ud83c\udfaa|\ud83c\udfab|\ud83c\udfac|\ud83c\udfad|\ud83c\udfae|\ud83c\udfaf|\ud83c\udfb0|\ud83c\udfb1|\ud83c\udfb2|\ud83c\udfb3|\ud83c\udfb4|\ud83c\udfb5|\ud83c\udfb6|\ud83c\udfb7|\ud83c\udfb8|\ud83c\udfb9|\ud83c\udfba|\ud83c\udfbb|\ud83c\udfbc|\ud83c\udfbd|\ud83c\udfbe|\ud83c\udfbf|\ud83c\udfc0|\ud83c\udfc1|\ud83c\udfc2|\ud83c\udfc3|\ud83c\udfc4|\ud83c\udfc6|\ud83c\udfc7|\ud83c\udfc8|\ud83c\udfc9|\ud83c\udfca|\ud83c\udfe0|\ud83c\udfe1|\ud83c\udfe2|\ud83c\udfe3|\ud83c\udfe4|\ud83c\udfe5|\ud83c\udfe6|\ud83c\udfe7|\ud83c\udfe8|\ud83c\udfe9|\ud83c\udfea|\ud83c\udfeb|\ud83c\udfec|\ud83c\udfed|\ud83c\udfee|\ud83c\udfef|\ud83c\udff0|\ud83c\udffb|\ud83c\udffc|\ud83c\udffd|\ud83c\udffe|\ud83c\udfff|\ud83d\udc00|\ud83d\udc01|\ud83d\udc02|\ud83d\udc03|\ud83d\udc04|\ud83d\udc05|\ud83d\udc06|\ud83d\udc07|\ud83d\udc08|\ud83d\udc09|\ud83d\udc0a|\ud83d\udc0b|\ud83d\udc0c|\ud83d\udc0d|\ud83d\udc0e|\ud83d\udc0f|\ud83d\udc10|\ud83d\udc11|\ud83d\udc12|\ud83d\udc13|\ud83d\udc14|\ud83d\udc15|\ud83d\udc16|\ud83d\udc17|\ud83d\udc18|\ud83d\udc19|\ud83d\udc1a|\ud83d\udc1b|\ud83d\udc1c|\ud83d\udc1d|\ud83d\udc1e|\ud83d\udc1f|\ud83d\udc20|\ud83d\udc21|\ud83d\udc22|\ud83d\udc23|\ud83d\udc24|\ud83d\udc25|\ud83d\udc26|\ud83d\udc27|\ud83d\udc28|\ud83d\udc29|\ud83d\udc2a|\ud83d\udc2b|\ud83d\udc2c|\ud83d\udc2d|\ud83d\udc2e|\ud83d\udc2f|\ud83d\udc30|\ud83d\udc31|\ud83d\udc32|\ud83d\udc33|\ud83d\udc34|\ud83d\udc35|\ud83d\udc36|\ud83d\udc37|\ud83d\udc38|\ud83d\udc39|\ud83d\udc3a|\ud83d\udc3b|\ud83d\udc3c|\ud83d\udc3d|\ud83d\udc3e|\ud83d\udc40|\ud83d\udc42|\ud83d\udc43|\ud83d\udc44|\ud83d\udc45|\ud83d\udc46|\ud83d\udc47|\ud83d\udc48|\ud83d\udc49|\ud83d\udc4a|\ud83d\udc4b|\ud83d\udc4c|\ud83d\udc4d|\ud83d\udc4e|\ud83d\udc4f|\ud83d\udc50|\ud83d\udc51|\ud83d\udc52|\ud83d\udc53|\ud83d\udc54|\ud83d\udc55|\ud83d\udc56|\ud83d\udc57|\ud83d\udc58|\ud83d\udc59|\ud83d\udc5a|\ud83d\udc5b|\ud83d\udc5c|\ud83d\udc5d|\ud83d\udc5e|\ud83d\udc5f|\ud83d\udc60|\ud83d\udc61|\ud83d\udc62|\ud83d\udc63|\ud83d\udc64|\ud83d\udc65|\ud83d\udc66|\ud83d\udc67|\ud83d\udc68|\ud83d\udc69|\ud83d\udc6a|\ud83d\udc6b|\ud83d\udc6c|\ud83d\udc6d|\ud83d\udc6e|\ud83d\udc6f|\ud83d\udc70|\ud83d\udc71|\ud83d\udc72|\ud83d\udc73|\ud83d\udc74|\ud83d\udc75|\ud83d\udc76|\ud83d\udc77|\ud83d\udc78|\ud83d\udc79|\ud83d\udc7a|\ud83d\udc7b|\ud83d\udc7c|\ud83d\udc7d|\ud83d\udc7e|\ud83d\udc7f|\ud83d\udc80|\ud83d\udc81|\ud83d\udc82|\ud83d\udc83|\ud83d\udc84|\ud83d\udc85|\ud83d\udc86|\ud83d\udc87|\ud83d\udc88|\ud83d\udc89|\ud83d\udc8a|\ud83d\udc8b|\ud83d\udc8c|\ud83d\udc8d|\ud83d\udc8e|\ud83d\udc8f|\ud83d\udc90|\ud83d\udc91|\ud83d\udc92|\ud83d\udc93|\ud83d\udc94|\ud83d\udc95|\ud83d\udc96|\ud83d\udc97|\ud83d\udc98|\ud83d\udc99|\ud83d\udc9a|\ud83d\udc9b|\ud83d\udc9c|\ud83d\udc9d|\ud83d\udc9e|\ud83d\udc9f|\ud83d\udca0|\ud83d\udca1|\ud83d\udca2|\ud83d\udca3|\ud83d\udca4|\ud83d\udca5|\ud83d\udca6|\ud83d\udca7|\ud83d\udca8|\ud83d\udca9|\ud83d\udcaa|\ud83d\udcab|\ud83d\udcac|\ud83d\udcad|\ud83d\udcae|\ud83d\udcaf|\ud83d\udcb0|\ud83d\udcb1|\ud83d\udcb2|\ud83d\udcb3|\ud83d\udcb4|\ud83d\udcb5|\ud83d\udcb6|\ud83d\udcb7|\ud83d\udcb8|\ud83d\udcb9|\ud83d\udcba|\ud83d\udcbb|\ud83d\udcbc|\ud83d\udcbd|\ud83d\udcbe|\ud83d\udcbf|\ud83d\udcc0|\ud83d\udcc1|\ud83d\udcc2|\ud83d\udcc3|\ud83d\udcc4|\ud83d\udcc5|\ud83d\udcc6|\ud83d\udcc7|\ud83d\udcc8|\ud83d\udcc9|\ud83d\udcca|\ud83d\udccb|\ud83d\udccc|\ud83d\udccd|\ud83d\udcce|\ud83d\udccf|\ud83d\udcd0|\ud83d\udcd1|\ud83d\udcd2|\ud83d\udcd3|\ud83d\udcd4|\ud83d\udcd5|\ud83d\udcd6|\ud83d\udcd7|\ud83d\udcd8|\ud83d\udcd9|\ud83d\udcda|\ud83d\udcdb|\ud83d\udcdc|\ud83d\udcdd|\ud83d\udcde|\ud83d\udcdf|\ud83d\udce0|\ud83d\udce1|\ud83d\udce2|\ud83d\udce3|\ud83d\udce4|\ud83d\udce5|\ud83d\udce6|\ud83d\udce7|\ud83d\udce8|\ud83d\udce9|\ud83d\udcea|\ud83d\udceb|\ud83d\udcec|\ud83d\udced|\ud83d\udcee|\ud83d\udcef|\ud83d\udcf0|\ud83d\udcf1|\ud83d\udcf2|\ud83d\udcf3|\ud83d\udcf4|\ud83d\udcf5|\ud83d\udcf6|\ud83d\udcf7|\ud83d\udcf9|\ud83d\udcfa|\ud83d\udcfb|\ud83d\udcfc|\ud83d\udd00|\ud83d\udd01|\ud83d\udd02|\ud83d\udd03|\ud83d\udd04|\ud83d\udd05|\ud83d\udd06|\ud83d\udd07|\ud83d\udd08|\ud83d\udd09|\ud83d\udd0a|\ud83d\udd0b|\ud83d\udd0c|\ud83d\udd0d|\ud83d\udd0e|\ud83d\udd0f|\ud83d\udd10|\ud83d\udd11|\ud83d\udd12|\ud83d\udd13|\ud83d\udd14|\ud83d\udd15|\ud83d\udd16|\ud83d\udd17|\ud83d\udd18|\ud83d\udd19|\ud83d\udd1a|\ud83d\udd1b|\ud83d\udd1c|\ud83d\udd1d|\ud83d\udd1e|\ud83d\udd1f|\ud83d\udd20|\ud83d\udd21|\ud83d\udd22|\ud83d\udd23|\ud83d\udd24|\ud83d\udd25|\ud83d\udd26|\ud83d\udd27|\ud83d\udd28|\ud83d\udd29|\ud83d\udd2a|\ud83d\udd2b|\ud83d\udd2c|\ud83d\udd2d|\ud83d\udd2e|\ud83d\udd2f|\ud83d\udd30|\ud83d\udd31|\ud83d\udd32|\ud83d\udd33|\ud83d\udd34|\ud83d\udd35|\ud83d\udd36|\ud83d\udd37|\ud83d\udd38|\ud83d\udd39|\ud83d\udd3a|\ud83d\udd3b|\ud83d\udd3c|\ud83d\udd3d|\ud83d\udd50|\ud83d\udd51|\ud83d\udd52|\ud83d\udd53|\ud83d\udd54|\ud83d\udd55|\ud83d\udd56|\ud83d\udd57|\ud83d\udd58|\ud83d\udd59|\ud83d\udd5a|\ud83d\udd5b|\ud83d\udd5c|\ud83d\udd5d|\ud83d\udd5e|\ud83d\udd5f|\ud83d\udd60|\ud83d\udd61|\ud83d\udd62|\ud83d\udd63|\ud83d\udd64|\ud83d\udd65|\ud83d\udd66|\ud83d\udd67|\ud83d\udd90|\ud83d\udd95|\ud83d\udd96|\ud83e\udd18|\ud83d\uddfb|\ud83d\uddfc|\ud83d\uddfd|\ud83d\uddfe|\ud83d\uddff|\ud83d\ude00|\ud83d\ude01|\ud83d\ude02|\ud83d\ude03|\ud83d\ude04|\ud83d\ude05|\ud83d\ude06|\ud83d\ude07|\ud83d\ude08|\ud83d\ude09|\ud83d\ude0a|\ud83d\ude0b|\ud83d\ude0c|\ud83d\ude0d|\ud83d\ude0e|\ud83d\ude0f|\ud83d\ude10|\ud83d\ude11|\ud83d\ude12|\ud83d\ude13|\ud83d\ude14|\ud83d\ude15|\ud83d\ude16|\ud83d\ude17|\ud83d\ude18|\ud83d\ude19|\ud83d\ude1a|\ud83d\ude1b|\ud83d\ude1c|\ud83d\ude1d|\ud83d\ude1e|\ud83d\ude1f|\ud83d\ude20|\ud83d\ude21|\ud83d\ude22|\ud83d\ude23|\ud83d\ude24|\ud83d\ude25|\ud83d\ude26|\ud83d\ude27|\ud83d\ude28|\ud83d\ude29|\ud83d\ude2a|\ud83d\ude2b|\ud83d\ude2c|\ud83d\ude2d|\ud83d\ude2e|\ud83d\ude2f|\ud83d\ude30|\ud83d\ude31|\ud83d\ude32|\ud83d\ude33|\ud83d\ude34|\ud83d\ude35|\ud83d\ude36|\ud83d\ude37|\ud83d\ude38|\ud83d\ude39|\ud83d\ude3a|\ud83d\ude3b|\ud83d\ude3c|\ud83d\ude3d|\ud83d\ude3e|\ud83d\ude3f|\ud83d\ude40|\ud83d\ude45|\ud83d\ude46|\ud83d\ude47|\ud83d\ude48|\ud83d\ude49|\ud83d\ude4a|\ud83d\ude4b|\ud83d\ude4c|\ud83d\ude4d|\ud83d\ude4e|\ud83d\ude4f|\ud83d\ude80|\ud83d\ude81|\ud83d\ude82|\ud83d\ude83|\ud83d\ude84|\ud83d\ude85|\ud83d\ude86|\ud83d\ude87|\ud83d\ude88|\ud83d\ude89|\ud83d\ude8a|\ud83d\ude8b|\ud83d\ude8c|\ud83d\ude8d|\ud83d\ude8e|\ud83d\ude8f|\ud83d\ude90|\ud83d\ude91|\ud83d\ude92|\ud83d\ude93|\ud83d\ude94|\ud83d\ude95|\ud83d\ude96|\ud83d\ude97|\ud83d\ude98|\ud83d\ude99|\ud83d\ude9a|\ud83d\ude9b|\ud83d\ude9c|\ud83d\ude9d|\ud83d\ude9e|\ud83d\ude9f|\ud83d\udea0|\ud83d\udea1|\ud83d\udea2|\ud83d\udea3|\ud83d\udea4|\ud83d\udea5|\ud83d\udea6|\ud83d\udea7|\ud83d\udea8|\ud83d\udea9|\ud83d\udeaa|\ud83d\udeab|\ud83d\udeac|\ud83d\udead|\ud83d\udeae|\ud83d\udeaf|\ud83d\udeb0|\ud83d\udeb1|\ud83d\udeb2|\ud83d\udeb3|\ud83d\udeb4|\ud83d\udeb5|\ud83d\udeb6|\ud83d\udeb7|\ud83d\udeb8|\ud83d\udeb9|\ud83d\udeba|\ud83d\udebb|\ud83d\udebc|\ud83d\udebd|\ud83d\udebe|\ud83d\udebf|\ud83d\udec0|\ud83d\udec1|\ud83d\udec2|\ud83d\udec3|\ud83d\udec4|\ud83d\udec5|#⃣|0⃣|1⃣|2⃣|3⃣|4⃣|5⃣|6⃣|7⃣|8⃣|9⃣|©|®|‼|⁉|™|ℹ|↔|↕|↖|↗|↘|↙|↩|↪|⌚|⌛|⏩|⏪|⏫|⏬|⏰|⏳|Ⓜ|▪|▫|▶|◀|◻|◼|◽|◾|☀|☁|☎|☑|☔|☕|☝|☺|♈|♉|♊|♋|♌|♍|♎|♏|♐|♑|♒|♓|♠|♣|♥|♦|♨|♻|♿|⚓|⚠|⚡|⚪|⚫|⚽|⚾|⛄|⛅|⛎|⛔|⛪|⛲|⛳|⛵|⛺|⛽|✂|✅|✈|✉|✊|✋|✌|✏|✒|✔|✖|✨|✳|✴|❄|❇|❌|❎|❓|❔|❕|❗|❤|➕|➖|➗|➡|➰|➿|⤴|⤵|⬅|⬆|⬇|⬛|⬜|⭐|⭕|〰|〽|㊗|㊙)";
            var hashtag = "(^|[\\s\\.,:;<>|'\"\\[\\]\\{\\}`\\~\\!\\%\\^\\*\\(\\)\\-\\+=\\x10])#[\\w]{2,64}";
            var mention = "(^|[\\s\\.,:;<>|'\"\\[\\]\\{\\}`\\~\\!\\%\\^\\*\\(\\)\\-\\+=\\x10])@[A-Za-z_0-9]{4,32}";
            var command = "(^|[\\s\\.,:;<>|'\"\\[\\]\\{\\}`\\~\\!\\%\\^\\*\\(\\)\\-\\+=\\x10])/[A-Za-z_0-9]{1,64}(@[A-Za-z_0-9]{5,32})?";
            //var hyperlink = Patterns.AUTOLINK_WEB_URL;
            var hyperlink = "(?i)\\b(((?:https?://|www\\d{0,3}[.]|[a-z0-9.\\-]+[.][a-z]{2,4}/)(?:[^\\s()<>]+|\\(([^\\s()<>]+|(\\([^\\s()<>]+\\)))*\\))+(?:\\(([^\\s()<>]+|(\\([^\\s()<>]+\\)))*\\)|[^\\s`!()\\[\\]{};:'\".,<>?«»“”‘’]))|([a-z0-9.\\-]+(\\.ru|\\.com|\\.net|\\.org|\\.us|\\.it|\\.co\\.uk)(?![a-z0-9]))|([a-zA-Z0-9_.+-]+@[a-zA-Z0-9-]+\\.[a-zA-Z0-9-.]*[a-zA-Z0-9-]+))";
            AllRegex = new Regex($"({hashtag})|({mention})|({command})|({hyperlink})", RegexOptions.Compiled);
            //AllRegex = new Regex($"({emoji})|({hashtag})|({mention})|({command})|({hyperlink})", RegexOptions.Compiled);
            EmojiRegex = new Regex(emoji, RegexOptions.Compiled);
        }

        public static void ReplaceEntities(TLMessage message, Span paragraph, Brush foreground)
        {
            var text = message.Message;
            var previous = 0;

            foreach (var entity in message.Entities.OrderBy(x => x.Offset))
            {
                if (entity.Offset > previous)
                {
                    paragraph.Inlines.Add(new Run { Text = text.Substring(previous, entity.Offset - previous) });
                }

                if (entity.Length + entity.Offset > text.Length)
                {
                    previous = entity.Offset + entity.Length;
                    continue;
                }

                var type = entity.TypeId;
                if (type == TLType.MessageEntityBold)
                {
                    paragraph.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length), FontWeight = FontWeights.SemiBold });
                }
                else if (type == TLType.MessageEntityItalic)
                {
                    paragraph.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length), FontStyle = FontStyle.Italic });
                }
                else if (type == TLType.MessageEntityCode)
                {
                    paragraph.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length), FontFamily = new FontFamily("Consolas") });
                }
                else if (type == TLType.MessageEntityPre)
                {
                    // TODO any additional
                    paragraph.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length), FontFamily = new FontFamily("Consolas") });
                }
                else if (type == TLType.MessageEntityUrl ||
                         type == TLType.MessageEntityEmail ||
                         type == TLType.MessageEntityMention ||
                         type == TLType.MessageEntityHashtag ||
                         type == TLType.MessageEntityBotCommand)
                {
                    object data = text.Substring(entity.Offset, entity.Length);

                    var hyperlink = new Hyperlink();
                    hyperlink.Click += (s, args) => Hyperlink_Navigate(type, data, message);
                    hyperlink.Inlines.Add(new Run { Text = (string)data });
                    hyperlink.Foreground = foreground;
                    paragraph.Inlines.Add(hyperlink);

                    if (entity is TLMessageEntityUrl)
                    {
                        SetEntity(hyperlink, (string)data);
                    }
                }
                else if (type == TLType.MessageEntityTextUrl ||
                         type == TLType.MessageEntityMentionName ||
                         type == TLType.InputMessageEntityMentionName)
                {
                    object data;
                    if (type == TLType.MessageEntityTextUrl)
                    {
                        data = ((TLMessageEntityTextUrl)entity).Url;
                    }
                    else if (type == TLType.MessageEntityMentionName)
                    {
                        data = ((TLMessageEntityMentionName)entity).UserId;
                    }
                    else // if(type == TLType.InputMessageEntityMentionName)
                    {
                        data = ((TLInputMessageEntityMentionName)entity).UserId;
                    }

                    var hyperlink = new Hyperlink();
                    hyperlink.Click += (s, args) => Hyperlink_Navigate(type, data, message);
                    hyperlink.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length) });
                    hyperlink.Foreground = foreground;
                    paragraph.Inlines.Add(hyperlink);

                    if (entity is TLMessageEntityTextUrl textUrl)
                    {
                        SetEntity(hyperlink, textUrl.Url);
                        ToolTipService.SetToolTip(hyperlink, textUrl.Url);
                    }
                }

                previous = entity.Offset + entity.Length;
            }

            if (text.Length > previous)
            {
                paragraph.Inlines.Add(new Run { Text = text.Substring(previous) });
            }
        }

        public static void ReplaceAll(TLMessageBase message, string text, Span paragraph, Brush foreground, bool matchLinks)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            var regex = EmojiRegex;
            if (matchLinks)
            {
                regex = AllRegex;
            }

            var matches = regex.Matches(text);
            if (matches.Count > 0)
            {
                var lastIndex = 0;
                foreach (Match match in matches)
                {
                    var isCommand = IsValidCommand(match.Value, out int index);

                    if ((match.Index + index) - lastIndex > 0)
                    {
                        paragraph.Inlines.Add(new Run { Text = text.Substring(lastIndex, (match.Index + index) - lastIndex) });
                    }

                    lastIndex = match.Index + match.Length;

                    //if (IsEmoji(match.Value))
                    //{
                    //    paragraph.Inlines.Add(GetEmojiBlock(match.Value));
                    //}
                    //else
                    {
                        var label = match.Value.Substring(index);
                        if (!isCommand && label.Length > 55)
                        {
                            label = label.Substring(0, 55) + "…";
                        }

                        var navstr = match.Value.Substring(index);

                        var hyperlink = new Hyperlink();
                        hyperlink.Click += (s, args) => Hyperlink_Legacy(navstr, message as TLMessage);
                        hyperlink.UnderlineStyle = UnderlineStyle.Single;
                        hyperlink.Foreground = foreground;
                        hyperlink.Inlines.Add(new Run { Text = label });
                        paragraph.Inlines.Add(hyperlink);

                        if (navstr.StartsWith("@"))
                        {
                        }
                        else if (navstr.StartsWith("#"))
                        {
                        }
                        else if (navstr.StartsWith("/"))
                        {
                        }
                        else if (!navstr.Contains("@"))
                        {
                            SetEntity(hyperlink, navstr);
                        }
                    }
                }

                if (lastIndex < text.Length)
                {
                    paragraph.Inlines.Add(new Run { Text = text.Substring(lastIndex, text.Length - lastIndex) });
                }
            }
            else
            {
                paragraph.Inlines.Add(new Run { Text = text });
            }
        }

        //private static bool IsEmoji(string text)
        //{
        //    return char.IsSurrogate(text, 0) || char.IsSurrogate(text, text.Length - 1) || EmojiData.BasicEmojis.Contains(text);
        //}

        private static bool IsValidCommand(string text, out int index)
        {
            if (text[0] == '@' || text[0] == '/' || text[0] == '#')
            {
                index = 0;
                return true;
            }
            else if (text.Length > 1)
            {
                if (text[1] == '@' || text[1] == '/' || text[1] == '#')
                {
                    index = 1;
                    return true;
                }
            }

            index = 0;
            return false;
        }

        //private static Dictionary<string, WeakReference<BitmapImage>> _cachedEmoji = new Dictionary<string, WeakReference<BitmapImage>>();

        //private static Inline GetEmojiBlock(string emoji)
        //{
        //    WeakReference<BitmapImage> reference;
        //    BitmapImage bitmap;

        //    if (_cachedEmoji.TryGetValue(emoji, out reference) && reference.TryGetTarget(out bitmap))
        //    {
        //        var uiContainer = new InlineUIContainer();
        //        uiContainer.Child = new Image { Source = bitmap, Width = 18, Height = 18, Margin = new Thickness(0, 0, 0, -2) };

        //        return uiContainer;
        //    }
        //    else
        //    {
        //        var result = new BitmapImage(new Uri(EmojiDataItem.BuildUri(emoji)));
        //        _cachedEmoji[emoji] = new WeakReference<BitmapImage>(result);

        //        var uiContainer = new InlineUIContainer();
        //        uiContainer.Child = new Image { Source = result, Width = 18, Height = 18, Margin = new Thickness(0, 0, 0, -2) };

        //        return uiContainer;
        //    }
        //}

        private static void Hyperlink_Legacy(string navstr, TLMessage message)
        {
            if (string.IsNullOrEmpty(navstr))
            {
                return;
            }

            StickerSetView.Current.Hide();

            if (navstr.StartsWith("@"))
            {
                Hyperlink_Navigate(TLType.MessageEntityMention, navstr, message);
            }
            else if (navstr.StartsWith("#"))
            {
                Hyperlink_Navigate(TLType.MessageEntityHashtag, navstr, message);
            }
            else if (navstr.StartsWith("/"))
            {
                Hyperlink_Navigate(TLType.MessageEntityBotCommand, navstr, message);
            }
            else if (!navstr.Contains("@"))
            {
                Hyperlink_Navigate(TLType.MessageEntityUrl, navstr, message);
            }
            else
            {
                Hyperlink_Navigate(TLType.MessageEntityEmail, navstr, message);
            }
        }

        private static async void Hyperlink_Navigate(TLType type, object data, TLMessage message)
        {
            if (type == TLType.InputMessageEntityMentionName)
            {
                var peerUser = data as TLInputUser;
                if (peerUser != null)
                {
                    var user = InMemoryCacheService.Current.GetUser(peerUser.UserId);
                    if (user != null)
                    {
                        var service = WindowWrapper.Current().NavigationServices.GetByFrameId("Main");
                        if (service != null)
                        {
                            service.Navigate(typeof(UserDetailsPage), new TLPeerUser { UserId = user.Id });
                        }
                    }
                }
            }
            else if (type == TLType.MessageEntityMentionName)
            {
                var user = InMemoryCacheService.Current.GetUser((int)data);
                if (user != null)
                {
                    var service = WindowWrapper.Current().NavigationServices.GetByFrameId("Main");
                    if (service != null)
                    {
                        service.Navigate(typeof(UserDetailsPage), new TLPeerUser { UserId = user.Id });
                    }
                }
            }
            else if (type == TLType.MessageEntityMention)
            {
                NavigateToUsername(MTProtoService.Current, (string)data, null, null, null);
            }
            else if (type == TLType.MessageEntityHashtag)
            {
                //await UnigramContainer.Instance.ResolveType<MainViewModel>().Dialogs.SearchAsync((string)data);
                //UnigramContainer.Current.ResolveType<MainViewModel>().Dialogs.SearchQuery = (string)data;
                //UnigramContainer.Current.ResolveType<ITelegramEventAggregator>().Publish("Search");

                var service = WindowWrapper.Current().NavigationServices.GetByFrameId("Main");
                if (service != null && service.Content is DialogPage page)
                {
                    page.ViewModel.Search = new DialogSearchViewModel(page.ViewModel.ProtoService, page.ViewModel.CacheService, page.ViewModel.Aggregator, page.ViewModel);
                    page.ViewModel.Search.SearchCommand.Execute((string)data);
                }
            }
            else if (type == TLType.MessageEntityBotCommand)
            {
                // TODO: THIS IS BAD
                var service = WindowWrapper.Current().NavigationServices.GetByFrameId("Main");
                if (service != null && service.Content is DialogPage page)
                {
                    page.ViewModel.SendCommand.Execute((string)data);
                }
            }
            else
            {
                var navigation = (string)data;
                if (type == TLType.MessageEntityUrl || type == TLType.MessageEntityTextUrl)
                {
                    var url = navigation;
                    if (url.StartsWith("http") == false)
                    {
                        url = "http://" + url;
                    }

                    if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                    {
                        if (MessageHelper.IsTelegramUrl(uri))
                        {
                            HandleTelegramUrl(message, navigation);
                        }
                        else
                        {
                            //if (message?.Media is TLMessageMediaWebPage webpageMedia)
                            //{
                            //    if (webpageMedia.WebPage is TLWebPage webpage && webpage.HasCachedPage && webpage.Url.Equals(navigation))
                            //    {
                            //        var service = WindowWrapper.Current().NavigationServices.GetByFrameId("Main");
                            //        if (service != null)
                            //        {
                            //            service.Navigate(typeof(InstantPage), webpageMedia);
                            //            return;
                            //        }
                            //    }
                            //}

                            if (type == TLType.MessageEntityTextUrl)
                            {
                                var confirm = await TLMessageDialog.ShowAsync(string.Format(Strings.Android.OpenUrlAlert, navigation), Strings.Android.AppName, Strings.Android.OK, Strings.Android.Cancel);
                                if (confirm != ContentDialogResult.Primary)
                                {
                                    return;
                                }
                            }

                            //if (navigation.Contains("@"))
                            //{
                            //    await Launcher.LaunchUriAsync(new Uri($"mailto:{navigation}"));
                            //}
                            //else
                            try
                            {
                                await Launcher.LaunchUriAsync(uri);
                            }
                            catch { }
                        }
                    }
                }
                else if (type == TLType.MessageEntityEmail)
                {
                    await Launcher.LaunchUriAsync(new Uri($"mailto:{navigation}"));
                }
            }

            //if (string.IsNullOrEmpty(navstr))
            //{
            //    return;
            //}

            //if (navstr.StartsWith("@"))
            //{
            //    int index = navstr.IndexOf('@');
            //    if (index != -1)
            //    {
            //        var mention = navstr.Substring(navstr.IndexOf('@'));
            //        //var user = ServiceLocator.Current.GetInstance<ICacheService>().GetUser(new TLInt(userId));
            //        //if (user != null)
            //        //{
            //        //    var navigationService = WindowWrapper.Current().NavigationServices["Main"];
            //        //    if (navigationService != null)
            //        //    {
            //        //        navigationService.Navigate(typeof(ContactPage), user);
            //        //    }
            //        //}
            //    }
            //}
            //else if (navstr.StartsWith("#"))
            //{
            //    int index = navstr.IndexOf('#');
            //    if (index != -1)
            //    {
            //        var hashtag = navstr.Substring(index);
            //        //RaiseSearchHashtag(new TelegramHashtagEventArgs
            //        //{
            //        //    Hashtag = hashtag
            //        //});
            //    }
            //}
            //else if (navstr.StartsWith("/"))
            //{
            //    int index = navstr.LastIndexOf('/');
            //    if (index != -1)
            //    {
            //        var command = navstr.Substring(index);
            //        MessageCommand?.Invoke(null, new MessageCommandEventArgs(message, command, MessageCommandType.Invoke));
            //        //RaiseInvokeCommand(new TelegramCommandEventArgs
            //        //{
            //        //    Command = command,
            //        //    Message = message
            //        //});
            //    }
            //}
            //else if (!navstr.Contains("@"))
            //{
            //    if (navstr.ToLowerInvariant().Contains("telegram.me"))
            //    {
            //        //RaiseTelegramLinkAction(new TelegramEventArgs
            //        //{
            //        //    Uri = navstr
            //        //});
            //        return;
            //    }

            //    if (!navstr.StartsWith("http"))
            //    {
            //        navstr = "http://" + navstr;
            //    }

            //    await Launcher.LaunchUriAsync(new Uri(navstr));
            //}
            //else
            //{
            //    if (navstr.StartsWith("http://"))
            //    {
            //        navstr = navstr.Remove(0, 7);
            //    }

            //    var email = new EmailMessage();
            //    email.To.Add(new EmailRecipient(navstr));

            //    await EmailManager.ShowComposeNewEmailAsync(email);
            //}
        }

        public static bool IsTelegramUrl(Uri uri)
        {
            if (Constants.TelegramHosts.Contains(uri.Host))
            {
                return true;
            }

            var config = InMemoryCacheService.Current.GetConfig();
            if (config != null && Uri.TryCreate(config.MeUrlPrefix, UriKind.Absolute, out Uri meUri))
            {
                return uri.Host.Equals(meUri.Host, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        public static void HandleTelegramUrl(string url)
        {
            HandleTelegramUrl(null, url);
        }

        public static async void HandleTelegramUrl(TLMessage message, string url)
        {
            // TODO: in-app navigation
            if (url.Contains("joinchat"))
            {
                var index = url.TrimEnd('/').LastIndexOf("/", StringComparison.OrdinalIgnoreCase);
                if (index != -1)
                {
                    var text = url.Substring(index).Replace("/", string.Empty);
                    if (!string.IsNullOrEmpty(text))
                    {
                        NavigateToInviteLink(text);
                    }
                }
            }
            else if (url.Contains("addstickers"))
            {
                var index = url.TrimEnd('/').LastIndexOf("/", StringComparison.OrdinalIgnoreCase);
                if (index != -1)
                {
                    var text = url.Substring(index).Replace("/", string.Empty);
                    if (!string.IsNullOrEmpty(text))
                    {
                        NavigateToStickerSet(text);
                    }
                }
            }
            else
            {
                var query = url.ParseQueryString();

                var accessToken = GetAccessToken(query, out PageKind pageKind);
                var post = query.GetParameter("post");
                var game = query.GetParameter("game");
                var result = url.StartsWith("http") ? url : ("https://" + url);

                if (Uri.TryCreate(result, UriKind.Absolute, out Uri uri))
                {
                    if (uri.Segments.Length >= 2)
                    {
                        var username = uri.Segments[1].Replace("/", string.Empty);
                        if (string.IsNullOrEmpty(post) && uri.Segments.Length >= 3)
                        {
                            post = uri.Segments[2].Replace("/", string.Empty);
                        }
                        if (!string.IsNullOrEmpty(username))
                        {
                            if (username.Equals("confirmphone", StringComparison.OrdinalIgnoreCase))
                            {
                                var phone = query.GetParameter("phone");
                                var hash = query.GetParameter("hash");

                                NavigateToConfirmPhone(MTProtoService.Current, phone, hash);
                                return;
                            }
                            else if (username.Equals("iv", StringComparison.OrdinalIgnoreCase))
                            {
                                if (message?.Media is TLMessageMediaWebPage webpageMedia)
                                {
                                    if (webpageMedia.WebPage is TLWebPage webpage && webpage.HasCachedPage && webpage.Url.Equals(url))
                                    {
                                        var service = WindowWrapper.Current().NavigationServices.GetByFrameId("Main");
                                        if (service != null)
                                        {
                                            service.Navigate(typeof(InstantPage), webpageMedia);
                                            return;
                                        }
                                    }
                                }

                                var navigation = url;
                                if (navigation.StartsWith("http") == false)
                                {
                                    navigation = "http://" + navigation;
                                }

                                await Launcher.LaunchUriAsync(new Uri(navigation));
                            }
                            else if (username.Equals("socks", StringComparison.OrdinalIgnoreCase))
                            {
                                var server = query.GetParameter("server");
                                var port = query.GetParameter("port");
                                var user = query.GetParameter("user");
                                var pass = query.GetParameter("pass");

                                if (server != null && int.TryParse(port, out int portCode))
                                {
                                    NavigateToSocks(server, portCode, user, pass);
                                }
                            }
                            else if (username.Equals("share"))
                            {
                                var hasUrl = false;
                                var text = query.GetParameter("url");
                                if (text == null)
                                {
                                    text = "";
                                }
                                if (query.GetParameter("text") != null)
                                {
                                    if (text.Length > 0)
                                    {
                                        hasUrl = true;
                                        text += "\n";
                                    }
                                    text += query.GetParameter("text");
                                }
                                if (text.Length > 4096 * 4)
                                {
                                    text = text.Substring(0, 4096 * 4);
                                }
                                while (text.EndsWith("\n"))
                                {
                                    text = text.Substring(0, text.Length - 1);
                                }


                                NavigateToShare(text, hasUrl);
                            }
                            else
                            {
                                NavigateToUsername(MTProtoService.Current, username, accessToken, post, string.IsNullOrEmpty(game) ? null : game);
                            }
                        }
                    }
                }
            }
        }

        public static async void NavigateToShare(string text, bool hasUrl)
        {
            await ForwardView.Current.ShowAsync(text, hasUrl);
        }

        public static async void NavigateToSocks(string server, int port, string user, string pass)
        {
            var userText = user != null ? string.Format($"{Strings.Android.UseProxyUsername}: {user}\n", user) : string.Empty;
            var passText = pass != null ? string.Format($"{Strings.Android.UseProxyPassword}: {pass}\n", pass) : string.Empty;
            var confirm = await TLMessageDialog.ShowAsync($"{Strings.Android.EnableProxyAlert}\n\n{Strings.Android.UseProxyAddress}: {server}\n{Strings.Android.UseProxyPort}: {port}\n{userText}{passText}\n{Strings.Android.EnableProxyAlert2}", Strings.Android.Proxy, Strings.Android.ConnectingToProxyEnable, Strings.Android.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                SettingsHelper.ProxyServer = server;
                SettingsHelper.ProxyPort = port;
                SettingsHelper.ProxyUsername = user;
                SettingsHelper.ProxyPassword = pass;
                SettingsHelper.IsProxyEnabled = true;

                UnigramContainer.Current.ResolveType<IMTProtoService>().ToggleProxy();
            }
        }

        public static async void NavigateToConfirmPhone(IMTProtoService protoService, string phone, string hash)
        {
            var response = await protoService.SendConfirmPhoneCodeAsync(hash, false);
            if (response.IsSucceeded)
            {
                var state = new SignInSentCodePage.NavigationParameters
                {
                    PhoneNumber = phone,
                    Result = response.Result,
                };

                App.Current.NavigationService.Navigate(typeof(SignInSentCodePage), state);

                //Telegram.Api.Helpers.Execute.BeginOnUIThread(delegate
                //{
                //    if (frame != null)
                //    {
                //        frame.CloseBlockingProgress();
                //    }
                //    TelegramViewBase.NavigateToConfirmPhone(result);
                //});
            }
            else
            {
                //if (error.CodeEquals(ErrorCode.BAD_REQUEST) && error.TypeEquals(ErrorType.USERNAME_NOT_OCCUPIED))
                //{
                //    return;
                //}
                //Telegram.Api.Helpers.Execute.ShowDebugMessage(string.Format("account.sendConfirmPhoneCode error {0}", error));
            };
        }

        public static async void NavigateToStickerSet(string text)
        {
            await StickerSetView.Current.ShowAsync(new TLInputStickerSetShortName { ShortName = text });
        }

        public static void NavigateToUsername(string username, string accessToken, string post, string game)
        {
            NavigateToUsername(MTProtoService.Current, username, accessToken, post, game);
        }

        public static async void NavigateToUsername(IMTProtoService protoService, string username, string accessToken, string post, string game)
        {
            if (username.StartsWith("@"))
            {
                username = username.Substring(1);
            }

            var service = WindowWrapper.Current().NavigationServices.GetByFrameId("Main");
            if (service != null && protoService != null)
            {
                var user = InMemoryCacheService.Current.GetUser(username) as TLUser;
                if (user != null && user.HasAccessHash)
                {
                    //if (!string.IsNullOrEmpty(game))
                    //{
                    //    TelegramViewBase.NavigateToGame(user, game);
                    //    return;
                    //}
                    //TelegramViewBase.NavigateToUser(user, accessToken, pageKind);

                    if (game != null)
                    {
                        var inputGame = new TLInputGameShortName { ShortName = game, BotId = user.ToInputUser() };
                        await ShareView.Current.ShowAsync(new TLInputMediaGame { Id = inputGame });
                    }
                    else
                    {
                        if (user.IsBot)
                        {
                            service.NavigateToDialog(user, accessToken: accessToken);
                        }
                        else
                        {
                            service.Navigate(typeof(UserDetailsPage), user.ToPeer());
                        }
                    }

                    //if (user.IsBot)
                    //{
                    //    service.Navigate(typeof(DialogPage), new TLPeerUser { UserId = user.Id });
                    //}
                    //else
                    //{
                    //    service.Navigate(typeof(UserDetailsPage), new TLPeerUser { UserId = user.Id });
                    //}

                    return;
                }
                else
                {
                    var channel = InMemoryCacheService.Current.GetChannel(username) as TLChannel;
                    if (channel != null && channel.HasAccessHash)
                    {
                        if (int.TryParse(post, out int postId))
                        {
                            service.NavigateToDialog(channel, postId);
                        }
                        else
                        {
                            service.NavigateToDialog(channel);
                        }
                        return;
                    }

                    var response = await protoService.ResolveUsernameAsync(username);
                    if (response.IsSucceeded)
                    {
                        if (response.Result.Peer is TLPeerUser peerUser)
                        {
                            user = InMemoryCacheService.Current.GetUser(peerUser.Id) as TLUser;
                            if (game != null)
                            {
                                var inputGame = new TLInputGameShortName { ShortName = game, BotId = user.ToInputUser() };
                                await ShareView.Current.ShowAsync(new TLInputMediaGame { Id = inputGame });
                            }
                            else
                            {
                                if (user.IsBot)
                                {
                                    service.NavigateToDialog(user, accessToken: accessToken);
                                }
                                else
                                {
                                    service.Navigate(typeof(UserDetailsPage), peerUser);
                                }
                            }


                            //if (user.IsBot)
                            //{
                            //    service.Navigate(typeof(DialogPage), peerUser);
                            //}
                            //else
                            //{
                            //    service.Navigate(typeof(UserDetailsPage), peerUser);
                            //}
                            return;
                        }
                        else if (response.Result.Peer is TLPeerChannel peerChannel)
                        {
                            channel = InMemoryCacheService.Current.GetChat(peerChannel.Id) as TLChannel;
                            if (int.TryParse(post, out int postId))
                            {
                                service.NavigateToDialog(channel, postId);
                            }
                            else
                            {
                                service.NavigateToDialog(channel);
                            }

                            return;
                        }

                        await new TLMessageDialog("No user found with this username", "Argh!").ShowQueuedAsync();
                    }
                    else
                    {
                        // TODO
                        await new TLMessageDialog("No user found with this username", "Argh!").ShowQueuedAsync();
                    }

                    //mtProtoService.ResolveUsernameAsync(new TLString(username), delegate (TLResolvedPeer result)
                    //{
                    //    Telegram.Api.Helpers.Execute.BeginOnUIThread(delegate
                    //    {
                    //        if (frame != null)
                    //        {
                    //            frame.CloseBlockingProgress();
                    //        }
                    //        TLPeerUser tLPeerUser = result.Peer as TLPeerUser;
                    //        if (tLPeerUser != null)
                    //        {
                    //            TLUserBase tLUserBase = Enumerable.FirstOrDefault<TLUserBase>(result.Users);
                    //            if (tLUserBase != null)
                    //            {
                    //                if (!string.IsNullOrEmpty(game))
                    //                {
                    //                    TelegramViewBase.NavigateToGame(tLUserBase, game);
                    //                    return;
                    //                }
                    //                TelegramViewBase.NavigateToUser(tLUserBase, accessToken, pageKind);
                    //                return;
                    //            }
                    //        }
                    //        TLPeerChannel tLPeerChannel = result.Peer as TLPeerChannel;
                    //        TLPeerChat tLPeerChat = result.Peer as TLPeerChat;
                    //        if (tLPeerChannel != null || tLPeerChat != null)
                    //        {
                    //            TLChatBase tLChatBase = Enumerable.FirstOrDefault<TLChatBase>(result.Chats);
                    //            if (tLChatBase != null)
                    //            {
                    //                TelegramViewBase.NavigateToChat(tLChatBase, post);
                    //                return;
                    //            }
                    //        }
                    //        MessageBox.Show(string.Format(Strings.Resources.CantFindContactWithUsername, username), Strings.Resources.Error, 0);
                    //    });
                    //}, delegate (TLRPCError error)
                    //{
                    //    Telegram.Api.Helpers.Execute.BeginOnUIThread(delegate
                    //    {
                    //        if (frame != null)
                    //        {
                    //            frame.CloseBlockingProgress();
                    //        }
                    //        if (error.CodeEquals(ErrorCode.BAD_REQUEST) && error.TypeEquals(ErrorType.USERNAME_NOT_OCCUPIED))
                    //        {
                    //            MessageBox.Show(string.Format(Strings.Resources.CantFindContactWithUsername, username), Strings.Resources.Error, 0);
                    //            return;
                    //        }
                    //        Telegram.Api.Helpers.Execute.ShowDebugMessage(string.Format("contacts.resolveUsername {0} error {1}", username, error));
                    //    });
                    //});
                }
            }
        }

        public static async void NavigateToInviteLink(string link)
        {
            var protoService = UnigramContainer.Current.ResolveType<IMTProtoService>();
            var cacheService = UnigramContainer.Current.ResolveType<ICacheService>();

            var response = await protoService.CheckChatInviteAsync(link);
            if (response.IsSucceeded)
            {
                if (response.Result is TLChatInviteAlready inviteAlready)
                {
                    var service = WindowWrapper.Current().NavigationServices.GetByFrameId("Main");
                    if (service != null)
                    {
                        service.NavigateToDialog(inviteAlready.Chat);
                    }
                }
                else if (response.Result is TLChatInvite invite)
                {
                    var dialog = new JoinChatView { DataContext = invite };
                    var result = await dialog.ShowAsync();
                    if (result == ContentDialogBaseResult.OK)
                    {
                        var import = await protoService.ImportChatInviteAsync(link);
                        if (import.IsSucceeded)
                        {
                            var updates = import.Result as TLUpdates;
                            if (updates != null)
                            {
                                var chatBase = updates.Chats.FirstOrDefault();
                                if (chatBase != null)
                                {
                                    var service = WindowWrapper.Current().NavigationServices.GetByFrameId("Main");
                                    if (service == null)
                                    {
                                        return;
                                    }

                                    if (chatBase is TLChannel channel)
                                    {
                                        protoService.GetHistoryAsync(channel.ToInputPeer(), channel.ToPeer(), false, 0, 0, 0, 1, 0, history =>
                                        {
                                            var vector = new TLVector<int>(1);
                                            if (history is ITLMessages messages)
                                            {
                                                vector.AddRange(messages.Messages.Select(x => x.Id));
                                            }

                                            cacheService.DeleteChannelMessages(channel.Id, vector);
                                            cacheService.SyncPeerMessages(channel.ToPeer(), history, true, false, sync =>
                                            {
                                                service.Dispatcher.Dispatch(() => service.NavigateToDialog(chatBase));
                                            });
                                        }, 
                                        error =>
                                        {
                                            Execute.ShowDebugMessage("messages.getHistory error " + error);
                                        });
                                    }
                                    else
                                    {
                                        service.NavigateToDialog(chatBase);
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (!import.Error.CodeEquals(TLErrorCode.BAD_REQUEST))
                            {
                                Execute.ShowDebugMessage("messages.importChatInvite error " + import.Error);
                                return;
                            }
                            if (import.Error.TypeEquals(TLErrorType.INVITE_HASH_EMPTY) || import.Error.TypeEquals(TLErrorType.INVITE_HASH_INVALID) || import.Error.TypeEquals(TLErrorType.INVITE_HASH_EXPIRED))
                            {
                                //MessageBox.Show(Strings.Resources.GroupNotExistsError, Strings.Resources.Error, 0);
                                return;
                            }
                            else if (import.Error.TypeEquals(TLErrorType.USERS_TOO_MUCH))
                            {
                                //MessageBox.Show(Strings.Resources.UsersTooMuch, Strings.Resources.Error, 0);
                                return;
                            }
                            else if (import.Error.TypeEquals(TLErrorType.BOTS_TOO_MUCH))
                            {
                                //MessageBox.Show(Strings.Resources.BotsTooMuch, Strings.Resources.Error, 0);
                                return;
                            }
                            else if (import.Error.TypeEquals(TLErrorType.USER_ALREADY_PARTICIPANT))
                            {
                                return;
                            }

                            Execute.ShowDebugMessage("messages.importChatInvite error " + import.Error);
                        }
                    }

                }
            }
            else
            {
                if (!response.Error.CodeEquals(TLErrorCode.BAD_REQUEST))
                {
                    Execute.ShowDebugMessage("messages.checkChatInvite error " + response.Error);
                    return;
                }
                if (response.Error.TypeEquals(TLErrorType.INVITE_HASH_EMPTY) || response.Error.TypeEquals(TLErrorType.INVITE_HASH_INVALID) || response.Error.TypeEquals(TLErrorType.INVITE_HASH_EXPIRED))
                {
                    //MessageBox.Show(Strings.Resources.GroupNotExistsError, Strings.Resources.Error, 0);
                    await TLMessageDialog.ShowAsync("This invite link is broken or has expired.", "Warning", "OK");
                    return;
                }

                Execute.ShowDebugMessage("messages.checkChatInvite error " + response.Error);
            }
        }

        public static string GetAccessToken(Dictionary<string, string> uriParams, out PageKind pageKind)
        {
            pageKind = PageKind.Dialog;

            var result = string.Empty;
            if (uriParams.ContainsKey("start"))
            {
                result = uriParams["start"];
            }
            else if (uriParams.ContainsKey("startgroup"))
            {
                pageKind = PageKind.Search;
                result = uriParams["startgroup"];
            }

            return result;
        }

        public enum PageKind
        {
            Dialog,
            Profile,
            Search
        }

        public static bool IsValidCommand(string command)
        {
            if (command.Length <= 2)
            {
                return false;
            }
            if (command.Length > 32)
            {
                return false;
            }
            if (command[0] != '/')
            {
                return false;
            }
            for (int i = 1; i < command.Length; i++)
            {
                if (!IsValidUsernameSymbol(command[i]))
                {
                    return false;
                }
            }
            return true;
        }

        public static bool IsValidUsername(string username)
        {
            if (username.Length <= 2)
            {
                return false;
            }
            if (username.Length > 32)
            {
                return false;
            }
            if (username[0] != '@')
            {
                return false;
            }
            for (int i = 1; i < username.Length; i++)
            {
                if (!IsValidUsernameSymbol(username[i]))
                {
                    return false;
                }
            }
            return true;
        }

        public static bool IsValidCommandSymbol(char symbol)
        {
            return (symbol >= 'a' && symbol <= 'z') || (symbol >= 'A' && symbol <= 'Z') || (symbol >= '0' && symbol <= '9') || symbol == '_';
        }

        public static bool IsValidUsernameSymbol(char symbol)
        {
            return (symbol >= 'a' && symbol <= 'z') || (symbol >= 'A' && symbol <= 'Z') || (symbol >= '0' && symbol <= '9') || symbol == '_';
        }

        public static void PreprocessEntities(ref TLMessage message)
        {
            if (string.IsNullOrWhiteSpace(message.Message)) return;

            var entities = new List<TLMessageEntityBase>();

            var matches = AllRegex.Matches(message.Message);
            if (matches.Count > 0)
            {
                foreach (Match match in matches)
                {
                    var isCommand = IsValidCommand(match.Value, out int index);

                    var navstr = match.Value.Substring(index);
                    if (navstr.StartsWith("@"))
                    {
                        entities.Add(new TLMessageEntityMention { Offset = match.Index + index, Length = match.Length - index });
                    }
                    else if (navstr.StartsWith("#"))
                    {
                        entities.Add(new TLMessageEntityHashtag { Offset = match.Index + index, Length = match.Length - index });
                    }
                    else if (navstr.StartsWith("/"))
                    {
                        entities.Add(new TLMessageEntityBotCommand { Offset = match.Index + index, Length = match.Length - index });
                    }
                    else if (!navstr.Contains("@"))
                    {
                        entities.Add(new TLMessageEntityUrl { Offset = match.Index + index, Length = match.Length - index });
                    }
                    else
                    {
                        entities.Add(new TLMessageEntityEmail { Offset = match.Index + index, Length = match.Length - index });
                    }
                }
            }

            if (message.Entities == null && entities.Count > 0)
            {
                message.Entities = new TLVector<TLMessageEntityBase>(entities);
                message.HasEntities = true;
            }
            else if (message.Entities != null && entities.Count > 0)
            {
                foreach (var entity in entities)
                {
                    message.Entities.Add(entity);
                }
            }
        }

        public static void CopyToClipboard(TLMessage message)
        {
            CopyToClipboard(message.Message, (IEnumerable<TLMessageEntityBase>)message.Entities ?? new TLMessageEntityBase[0]);
        }

        public static void CopyToClipboard(string message, IEnumerable<TLMessageEntityBase> entities)
        {
            var tdesktop = GetTDesktopClipboard(entities);

            var dataPackage = new DataPackage();
            if (tdesktop != null) dataPackage.SetData("application/x-td-field-tags", tdesktop);
            dataPackage.SetText(message);
            ClipboardEx.TrySetContent(dataPackage);
        }

        private static IRandomAccessStream GetTDesktopClipboard(IEnumerable<TLMessageEntityBase> entities)
        {
            var mentions = entities.Where(x => x is TLInputMessageEntityMentionName || x is TLMessageEntityMentionName).ToList();
            if (mentions.Count > 0)
            {
                using (var stream = new InMemoryRandomAccessStream())
                {
                    using (var writer = new BinaryWriter(stream.AsStream()))
                    {
                        var count = BitConverter.GetBytes(mentions.Count);
                        Array.Reverse(count);
                        writer.Write(count);

                        foreach (var entity in mentions)
                        {
                            var offset = BitConverter.GetBytes(entity.Offset);
                            Array.Reverse(offset);
                            writer.Write(offset);

                            var length = BitConverter.GetBytes(entity.Length);
                            Array.Reverse(length);
                            writer.Write(length);

                            var tag = string.Empty;
                            var inputMention = entity as TLInputMessageEntityMentionName;
                            if (inputMention != null)
                            {
                                var userId = inputMention.UserId as TLInputUser;
                                var tempTag = $"mention://user.{userId.UserId}.{userId.AccessHash}:{SettingsHelper.UserId}";

                                foreach (var c in tempTag)
                                {
                                    tag += $"\0{c}";
                                }
                            }

                            //tag = "\0m\0e\0n\0t\0i\0o\0n\0:\0/\0/\0u\0s\0e\0r\0.\02\04\03\01\03\06\06\05\01\0.\04\07\01\06\00\02\09\07\09\00\03\03\08\04\01\04\04\07\03\0:\03\08\04\07\05\08\06\01";

                            var tagLength = BitConverter.GetBytes(tag.Length);
                            Array.Reverse(tagLength);
                            writer.Write(tagLength);

                            var tagBytes = Encoding.ASCII.GetBytes(tag);
                            writer.Write(tagBytes);
                        }

                        return stream.CloneStream();
                    }
                }
            }

            return null;
        }

        private string GetLinkFromEntity(TLMessage message, TLMessageEntityBase entity)
        {
            if (entity is TLMessageEntityTextUrl textUrl)
            {
                return textUrl.Url;
            }
            else if (entity is TLMessageEntityUrl)
            {
                if (message.Message.Length < entity.Offset + entity.Length)
                {
                    return message.Message.Substring(entity.Offset, entity.Length);
                }
            }

            return null;
        }

        #region Entity

        public static void Hyperlink_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var text = sender as RichTextBlock;
            if (args.TryGetPosition(sender, out Point point))
            {
                if (point.X < 0 || point.Y < 0)
                {
                    point = new Point(Math.Max(point.X, 0), Math.Max(point.Y, 0));
                }

                var length = text.SelectedText.Length;
                if (length > 0)
                {
                    var link = text.SelectedText;

                    var copy = new MenuFlyoutItem { Text = Strings.Android.Copy, DataContext = link };

                    copy.Click += LinkCopy_Click;

                    var flyout = new MenuFlyout();
                    flyout.Items.Add(copy);
                    flyout.ShowAt(sender, point);

                    args.Handled = true;
                }
                else
                {
                    var hyperlink = text.GetHyperlinkFromPoint(point);
                    if (hyperlink == null)
                    {
                        return;
                    }

                    var link = GetEntity(hyperlink);
                    if (link == null)
                    {
                        return;
                    }

                    var open = new MenuFlyoutItem { Text = Strings.Android.Open, DataContext = link };
                    var copy = new MenuFlyoutItem { Text = Strings.Android.Copy, DataContext = link };

                    open.Click += LinkOpen_Click;
                    copy.Click += LinkCopy_Click;

                    var flyout = new MenuFlyout();
                    flyout.Items.Add(open);
                    flyout.Items.Add(copy);
                    flyout.ShowAt(sender, point);

                    args.Handled = true;
                }
            }
        }

        private async static void LinkOpen_Click(object sender, RoutedEventArgs e)
        {
            var item = sender as MenuFlyoutItem;
            var entity = item.DataContext as string;

            var url = entity;
            if (entity.StartsWith("http") == false)
            {
                url = "http://" + url;
            }

            if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                await Launcher.LaunchUriAsync(uri);
            }
        }

        private static void LinkCopy_Click(object sender, RoutedEventArgs e)
        {
            var item = sender as MenuFlyoutItem;
            var entity = item.DataContext as string;

            var dataPackage = new DataPackage();
            dataPackage.SetText(entity);
            ClipboardEx.TrySetContent(dataPackage);
        }

        public static string GetEntity(DependencyObject obj)
        {
            return (string)obj.GetValue(EntityProperty);
        }

        public static void SetEntity(DependencyObject obj, string value)
        {
            obj.SetValue(EntityProperty, value);
        }

        public static readonly DependencyProperty EntityProperty =
            DependencyProperty.RegisterAttached("Entity", typeof(string), typeof(MessageHelper), new PropertyMetadata(null));

        #endregion
    }

    public enum MessageCommandType
    {
        Invoke,
        Mention,
        Hashtag
    }
}
