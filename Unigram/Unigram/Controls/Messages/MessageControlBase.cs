using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.TL;
using Unigram.Converters;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Messages
{
    public class MessageControlBase : StackPanel
    {
        public TLMessage ViewModel => DataContext as TLMessage;

        public BindConvert Convert => BindConvert.Current;

        //public MessageControlBase()
        //{
        //    DataContextChanged += (s, args) =>
        //    {
        //        if (ViewModel != null)
        //        {
        //            Loading(s, null);
        //        }
        //    };
        //}

        protected void OnMessageChanged(TextBlock paragraph)
        {
            paragraph.Inlines.Clear();

            var message = DataContext as TLMessage;
            if (message != null)
            {
                if (message.IsFirst && !message.IsOut && !message.IsPost && (message.ToId is TLPeerChat || message.ToId is TLPeerChannel) && (paragraph.Inlines.Count > 0 || !IsFullMedia(message.Media)))
                {
                    var hyperlink = new Hyperlink();
                    hyperlink.Inlines.Add(new Run { Text = message.From?.FullName, Foreground = Convert.Bubble(message.FromId) });
                    hyperlink.UnderlineStyle = UnderlineStyle.None;

                    paragraph.Inlines.Add(hyperlink);
                }

                if (message.HasFwdFrom)
                {
                    if (paragraph.Inlines.Count > 0)
                        paragraph.Inlines.Add(new LineBreak());

                    paragraph.Inlines.Add(new Run { Text = "Forwarded from " });

                    var name = string.Empty;

                    var chat = message.FwdFromChannel;
                    if (chat != null)
                    {
                        name = chat.FullName;
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

                    paragraph.Inlines.Add(hyperlink);
                }

                if (message.ViaBot != null)
                {
                    var hyperlink = new Hyperlink();
                    hyperlink.Inlines.Add(new Run { Text = paragraph.Inlines.Count > 0 ? " via @" : "via @" + message.ViaBot.Username });
                    hyperlink.UnderlineStyle = UnderlineStyle.None;

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

        /// <summary>
        /// x:Bind hack
        /// </summary>
        public new event TypedEventHandler<FrameworkElement, object> Loading;

        #region Static
        protected static SolidColorBrush StatusDarkBackgroundBrush = (SolidColorBrush)App.Current.Resources["MessageOverlayBackgroundBrush"];
        protected static SolidColorBrush StatusDarkForegroundBrush = new SolidColorBrush(Colors.White);
        protected static SolidColorBrush StatusLightLabelForegroundBrush = (SolidColorBrush)App.Current.Resources["MessageSubtleLabelBrush"];
        protected static SolidColorBrush StatusLightGlyphForegroundBrush = (SolidColorBrush)App.Current.Resources["MessageSubtleGlyphBrush"];

        protected static bool IsFullMedia(TLMessageMediaBase media)
        {
            if (media == null) return false;

            if (media.TypeId == TLType.MessageMediaGeo) return true;
            if (media.TypeId == TLType.MessageMediaPhoto) return true;
            if (media.TypeId == TLType.MessageMediaDocument)
            {
                var documentMedia = media as TLMessageMediaDocument;
                if (TLMessage.IsGif(documentMedia.Document)) return true;
                else if (TLMessage.IsVideo(documentMedia.Document)) return true;
            }

            return false;
        }

        protected static bool IsInlineMedia(TLMessageMediaBase media)
        {
            if (media == null) return false;

            if (media.TypeId == TLType.MessageMediaContact) return true;
            if (media.TypeId == TLType.MessageMediaVenue) return true;
            if (media.TypeId == TLType.MessageMediaPhoto)
            {
                var photoMedia = media as TLMessageMediaPhoto;
                if (string.IsNullOrWhiteSpace(photoMedia.Caption))
                {
                    return false;
                }

                return true;
            }
            if (media.TypeId == TLType.MessageMediaDocument)
            {
                var documentMedia = media as TLMessageMediaDocument;
                if (TLMessage.IsMusic(documentMedia.Document)) return true;
                else if (TLMessage.IsVoice(documentMedia.Document)) return true;
                else if (TLMessage.IsVideo(documentMedia.Document))
                {
                    if (string.IsNullOrWhiteSpace(documentMedia.Caption))
                    {
                        return false;
                    }
                }
                else if (TLMessage.IsGif(documentMedia.Document))
                {
                    if (string.IsNullOrWhiteSpace(documentMedia.Caption))
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }
        #endregion
    }
}
