using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Converters;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Controls
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MessageReference : UserControl
    {
        public MessageReference()
        {
            InitializeComponent();
        }

        //public string Title
        //{
        //    get
        //    {
        //        return TitleLabel.Text;
        //    }
        //    set
        //    {
        //        TitleLabel.Text = value;
        //    }
        //}

        public string Title { get; set; }

        #region Message

        public object Message
        {
            get { return (object)GetValue(MessageProperty); }
            set { SetValue(MessageProperty, value); }
        }

        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register("Message", typeof(object), typeof(MessageReference), new PropertyMetadata(null, OnMessageChanged));

        private static void OnMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((MessageReference)d).SetTemplateCore((object)e.NewValue);
        }

        #endregion

        private bool SetTemplateCore(object item)
        {
            if (item == null)
            {
                Visibility = Visibility.Collapsed;
                return false;
            }

            var replyInfo = item as ReplyInfo;
            if (replyInfo == null)
            {
                if (item is TLMessageBase)
                {
                    return GetMessageTemplate(item as TLObject);
                }

                return SetUnsupportedTemplate(null, null);
            }
            else
            {
                if (replyInfo.Reply == null)
                {
                    //return ReplyLoadingTemplate;
                }

                var contain = replyInfo.Reply as TLMessagesContainter;
                if (contain != null)
                {
                    return GetMessagesContainerTemplate(contain);
                }

                if (replyInfo.ReplyToMsgId == null || replyInfo.ReplyToMsgId.Value == 0)
                {
                    return SetUnsupportedTemplate(null, null);
                }

                return GetMessageTemplate(replyInfo.Reply);
            }
        }

        #region Container

        private bool GetMessagesContainerTemplate(TLMessagesContainter container)
        {
            //if (container.WebPageMedia != null)
            //{
            //    var webpageMedia = container.WebPageMedia as TLMessageMediaWebPage;
            //    if (webpageMedia != null)
            //    {
            //        var pendingWebpage = webpageMedia.Webpage as TLWebPagePending;
            //        if (pendingWebpage != null)
            //        {
            //            return WebPagePendingTemplate;
            //        }

            //        var webpage = webpageMedia.Webpage as TLWebPage;
            //        if (webpage != null)
            //        {
            //            return WebPageTemplate;
            //        }

            //        var emptyWebpage = webpageMedia.Webpage as TLWebPageEmpty;
            //        if (emptyWebpage != null)
            //        {
            //            return WebPageEmptyTemplate;
            //        }
            //    }
            //}

            if (container.FwdMessages != null)
            {
                if (container.FwdMessages.Count == 1)
                {
                    var forwardMessage = container.FwdMessages[0];
                    if (forwardMessage != null)
                    {
                        if (!string.IsNullOrEmpty(forwardMessage.Message) && (forwardMessage.Media == null || forwardMessage.Media is TLMessageMediaEmpty || forwardMessage.Media is TLMessageMediaWebPage))
                        {
                            return SetTextTemplate(forwardMessage, Title);
                        }

                        var media = container.FwdMessages[0].Media;
                        if (media != null)
                        {
                            switch (media.TypeId)
                            {
                                case TLType.MessageMediaPhoto:
                                    return SetPhotoTemplate(forwardMessage, null);
                                case TLType.MessageMediaGeo:
                                    return SetGeoPointTemplate(forwardMessage, null);
                                case TLType.MessageMediaVenue:
                                    return SetVenueTemplate(forwardMessage, null);
                                case TLType.MessageMediaContact:
                                    return SetContactTemplate(forwardMessage, null);
                                case TLType.MessageMediaGame:
                                    return SetGameTemplate(forwardMessage, null);
                                case TLType.MessageMediaEmpty:
                                    return SetUnsupportedTemplate(forwardMessage, null);
                                case TLType.MessageMediaDocument:
                                    if (forwardMessage.IsSticker())
                                    {
                                        return SetStickerTemplate(forwardMessage, null);
                                    }
                                    else if (forwardMessage.IsGif())
                                    {
                                        return SetGifTemplate(forwardMessage, null);
                                    }
                                    else if (forwardMessage.IsVoice())
                                    {
                                        return SetVoiceMessageTemplate(forwardMessage, null);
                                    }
                                    else if (forwardMessage.IsVideo())
                                    {
                                        return SetVideoTemplate(forwardMessage, null);
                                    }
                                    else if (forwardMessage.IsAudio())
                                    {
                                        return SetAudioTemplate(forwardMessage, null);
                                    }

                                    return SetDocumentTemplate(forwardMessage, null);
                            }
                        }
                    }
                }

                return SetForwardedMessagesTemplate(container.FwdMessages);
            }

            if (container.EditMessage != null)
            {
                var editMessage = container.EditMessage;
                if (editMessage != null)
                {
                    if (!string.IsNullOrEmpty(editMessage.Message) && (editMessage.Media == null || editMessage.Media is TLMessageMediaEmpty || editMessage.Media is TLMessageMediaWebPage))
                    {
                        return SetTextTemplate(editMessage, "Edit message");
                    }

                    var media = editMessage.Media;
                    if (media != null)
                    {
                        switch (media.TypeId)
                        {
                            case TLType.MessageMediaPhoto:
                                return SetPhotoTemplate(editMessage, "Edit message");
                            case TLType.MessageMediaGeo:
                                return SetGeoPointTemplate(editMessage, "Edit message");
                            case TLType.MessageMediaVenue:
                                return SetVenueTemplate(editMessage, "Edit message");
                            case TLType.MessageMediaContact:
                                return SetContactTemplate(editMessage, "Edit message");
                            case TLType.MessageMediaGame:
                                return SetGameTemplate(editMessage, "Edit message");
                            case TLType.MessageMediaEmpty:
                                return SetUnsupportedTemplate(editMessage, "Edit message");
                            case TLType.MessageMediaDocument:
                                if (editMessage.IsSticker())
                                {
                                    return SetStickerTemplate(editMessage, "Edit message");
                                }
                                else if (editMessage.IsGif())
                                {
                                    return SetGifTemplate(editMessage, "Edit message");
                                }
                                else if (editMessage.IsVoice())
                                {
                                    return SetVoiceMessageTemplate(editMessage, "Edit message");
                                }
                                else if (editMessage.IsVideo())
                                {
                                    return SetVideoTemplate(editMessage, "Edit message");
                                }
                                else if (editMessage.IsAudio())
                                {
                                    return SetAudioTemplate(editMessage, "Edit message");
                                }

                                return SetDocumentTemplate(editMessage, "Edit message");
                        }
                    }
                }

                return SetUnsupportedTemplate(editMessage, "Edit message");
            }

            return SetUnsupportedTemplate(null, "Edit message");
        }

        #endregion

        #region Reply

        private bool GetMessageTemplate(TLObject obj)
        {
            Visibility = Visibility.Collapsed;

            var message = obj as TLMessage;
            if (message != null)
            {
                if (!string.IsNullOrEmpty(message.Message) && (message.Media == null || message.Media is TLMessageMediaEmpty))
                {
                    return SetTextTemplate(message, Title);
                }

                var media = message.Media;
                if (media != null)
                {
                    switch (media.TypeId)
                    {
                        case TLType.MessageMediaPhoto:
                            return SetPhotoTemplate(message, Title);
                        case TLType.MessageMediaGeo:
                            return SetGeoPointTemplate(message, Title);
                        case TLType.MessageMediaVenue:
                            return SetVenueTemplate(message, Title);
                        case TLType.MessageMediaContact:
                            return SetContactTemplate(message, Title);
                        case TLType.MessageMediaGame:
                            return SetGameTemplate(message, Title);
                        case TLType.MessageMediaEmpty:
                            return SetUnsupportedTemplate(message, Title);
                        case TLType.MessageMediaWebPage:
                            return SetWebPageTemplate(message, Title);
                        case TLType.MessageMediaDocument:
                            if (message.IsSticker())
                            {
                                return SetStickerTemplate(message, Title);
                            }
                            else if (message.IsGif())
                            {
                                return SetGifTemplate(message, Title);
                            }
                            else if (message.IsVoice())
                            {
                                return SetVoiceMessageTemplate(message, Title);
                            }
                            else if (message.IsVideo())
                            {
                                return SetVideoTemplate(message, Title);
                            }
                            else if (message.IsAudio())
                            {
                                return SetAudioTemplate(message, Title);
                            }

                            return SetDocumentTemplate(message, Title);
                    }
                }
            }

            var serviceMessage = obj as TLMessageService;
            if (serviceMessage != null)
            {
                var action = serviceMessage.Action;
                if (action is TLMessageActionChatEditPhoto)
                {
                    return SetServicePhotoTemplate(serviceMessage, Title);
                }

                return SetServiceTextTemplate(serviceMessage, Title);
            }
            else
            {
                var emptyMessage = obj as TLMessageEmpty;
                if (emptyMessage != null)
                {
                    return SetEmptyTemplate(message, Title);
                }

                return SetUnsupportedTemplate(message, Title);
            }
        }

        private bool SetForwardedMessagesTemplate(TLVector<TLMessage> messages)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            TitleLabel.Text = string.Empty;
            ServiceLabel.Text = $"{messages.Count} forwarded messages";
            MessageLabel.Text = string.Empty;

            var users = messages.Select(x => x.From).Distinct(new LambdaComparer<TLUser>((x, y) => x.Id == y.Id)).ToList();
            if (users.Count > 2)
            {
                TitleLabel.Text = $"{users[0].FullName} and {users.Count - 1} others";
            }
            else if (users.Count == 2)
            {
                TitleLabel.Text = $"{users[0].FullName} and {users[1].FullName}";
            }
            else if (users.Count == 1)
            {
                TitleLabel.Text = users[0].FullName;
            }

            return true;
        }

        private bool SetTextTemplate(TLMessage message, string title)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            TitleLabel.Text = GetFromLabel(message, title);
            ServiceLabel.Text = string.Empty;
            MessageLabel.Text = message.Message.Replace("\r\n", "\n").Replace('\n', ' ');

            return true;
        }

        private bool SetPhotoTemplate(TLMessage message, string title)
        {
            Visibility = Visibility.Visible;

            FindName(nameof(ThumbRoot));
            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Visible;

            // 🖼

            TitleLabel.Text = GetFromLabel(message, title);
            ServiceLabel.Text = "Photo";
            MessageLabel.Text = string.Empty;

            var photoMedia = message.Media as TLMessageMediaPhoto;
            if (photoMedia != null)
            {
                if (!string.IsNullOrWhiteSpace(photoMedia.Caption))
                {
                    ServiceLabel.Text += ", ";
                    MessageLabel.Text += photoMedia.Caption.Replace("\r\n", "\n").Replace('\n', ' ');
                }

                ThumbImage.Source = (ImageSource)DefaultPhotoConverter.Convert(photoMedia, "thumbnail");
            }

            return true;
        }

        private bool SetGeoPointTemplate(TLMessage message, string title)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            TitleLabel.Text = GetFromLabel(message, title);
            ServiceLabel.Text = "Location";
            MessageLabel.Text = string.Empty;

            return true;
        }

        private bool SetVenueTemplate(TLMessage message, string title)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            TitleLabel.Text = GetFromLabel(message, title);
            ServiceLabel.Text = "Location";
            MessageLabel.Text = string.Empty;

            var venueMedia = message.Media as TLMessageMediaVenue;
            if (venueMedia != null && !string.IsNullOrWhiteSpace(venueMedia.Title))
            {
                ServiceLabel.Text += ", ";
                MessageLabel.Text = venueMedia.Title.Replace("\r\n", "\n").Replace('\n', ' ');
            }

            return true;
        }

        private bool SetGameTemplate(TLMessage message, string title)
        {
            Visibility = Visibility.Visible;

            FindName(nameof(ThumbRoot));
            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Visible;

            TitleLabel.Text = GetFromLabel(message, title);
            ServiceLabel.Text = "🎮 Game";
            MessageLabel.Text = string.Empty;

            var gameMedia = message.Media as TLMessageMediaGame;
            if (gameMedia != null && gameMedia.Game != null)
            {
                ServiceLabel.Text = $"🎮 {gameMedia.Game.Title}";

                ThumbImage.Source = (ImageSource)DefaultPhotoConverter.Convert(gameMedia.Game.Photo, "thumbnail");
            }

            return true;
        }

        private bool SetContactTemplate(TLMessage message, string title)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            TitleLabel.Text = GetFromLabel(message, title);
            ServiceLabel.Text = "Contact";
            MessageLabel.Text = string.Empty;

            return true;
        }

        private bool SetAudioTemplate(TLMessage message, string title)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            TitleLabel.Text = GetFromLabel(message, title);
            ServiceLabel.Text = "Audio";
            MessageLabel.Text = string.Empty;

            var documentMedia = message.Media as TLMessageMediaDocument;
            if (documentMedia != null)
            {
                var document = documentMedia.Document as TLDocument;
                if (document != null)
                {
                    var audioAttribute = document.Attributes.OfType<TLDocumentAttributeAudio>().FirstOrDefault();
                    if (audioAttribute != null)
                    {
                        if (audioAttribute.HasPerformer && audioAttribute.HasTitle)
                        {
                            ServiceLabel.Text = $"{audioAttribute.Performer} - {audioAttribute.Title}";
                        }
                        else if (audioAttribute.HasPerformer && !audioAttribute.HasTitle)
                        {
                            ServiceLabel.Text = $"{audioAttribute.Performer} - Unknown Track";
                        }
                        else if (audioAttribute.HasTitle && !audioAttribute.HasPerformer)
                        {
                            ServiceLabel.Text = $"{audioAttribute.Title}";
                        }
                        else
                        {
                            ServiceLabel.Text = document.FileName;
                        }
                    }
                    else
                    {
                        ServiceLabel.Text = document.FileName;
                    }
                }

                if (!string.IsNullOrWhiteSpace(documentMedia.Caption))
                {
                    ServiceLabel.Text += ", ";
                    MessageLabel.Text += documentMedia.Caption.Replace('\n', ' ');
                }
            }

            return true;
        }

        private bool SetVoiceMessageTemplate(TLMessage message, string title)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            TitleLabel.Text = GetFromLabel(message, title);
            ServiceLabel.Text = "Voice message";
            MessageLabel.Text = string.Empty;

            var documentMedia = message.Media as TLMessageMediaDocument;
            if (documentMedia != null)
            {
                if (!string.IsNullOrWhiteSpace(documentMedia.Caption))
                {
                    ServiceLabel.Text += ", ";
                    MessageLabel.Text += documentMedia.Caption.Replace("\r\n", "\n").Replace('\n', ' ');
                }
            }

            return true;
        }

        private bool SetWebPageTemplate(TLMessage message, string title)
        {
            var webPageMedia = message.Media as TLMessageMediaWebPage;
            if (webPageMedia != null)
            {
                var webPage = webPageMedia.WebPage as TLWebPage;
                if (webPage != null && webPage.Photo != null && webPage.Type != null)
                {
                    Visibility = Visibility.Visible;

                    FindName(nameof(ThumbRoot));
                    if (ThumbRoot != null)
                        ThumbRoot.Visibility = Visibility.Visible;

                    TitleLabel.Text = GetFromLabel(message, title);
                    ServiceLabel.Text = string.Empty;
                    MessageLabel.Text = message.Message.Replace("\r\n", "\n").Replace('\n', ' ');

                    ThumbImage.Source = (ImageSource)DefaultPhotoConverter.Convert(webPage.Photo, "thumbnail");
                }
                else
                {
                    return SetTextTemplate(message, title);
                }
            }

            return true;
        }

        private bool SetVideoTemplate(TLMessage message, string title)
        {
            Visibility = Visibility.Visible;

            FindName(nameof(ThumbRoot));
            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Visible;

            TitleLabel.Text = GetFromLabel(message, title);
            ServiceLabel.Text = "Video";
            MessageLabel.Text = string.Empty;

            var documentMedia = message.Media as TLMessageMediaDocument;
            if (documentMedia != null)
            {
                if (!string.IsNullOrWhiteSpace(documentMedia.Caption))
                {
                    ServiceLabel.Text += ", ";
                    MessageLabel.Text += documentMedia.Caption.Replace("\r\n", "\n").Replace('\n', ' ');
                }

                ThumbImage.Source = (ImageSource)DefaultPhotoConverter.Convert(documentMedia.Document, "thumbnail");
            }

            return true;
        }

        private bool SetGifTemplate(TLMessage message, string title)
        {
            Visibility = Visibility.Visible;

            FindName(nameof(ThumbRoot));
            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Visible;

            TitleLabel.Text = GetFromLabel(message, title);
            ServiceLabel.Text = "GIF";
            MessageLabel.Text = string.Empty;

            var documentMedia = message.Media as TLMessageMediaDocument;
            if (documentMedia != null)
            {
                if (!string.IsNullOrWhiteSpace(documentMedia.Caption))
                {
                    ServiceLabel.Text += ", ";
                    MessageLabel.Text += documentMedia.Caption.Replace("\r\n", "\n").Replace('\n', ' ');
                }

                ThumbImage.Source = (ImageSource)DefaultPhotoConverter.Convert(documentMedia.Document, "thumbnail");
            }

            return true;
        }

        private bool SetStickerTemplate(TLMessage message, string title)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            TitleLabel.Text = GetFromLabel(message, title);
            ServiceLabel.Text = "Sticker";
            MessageLabel.Text = string.Empty;

            var documentMedia = message.Media as TLMessageMediaDocument;
            if (documentMedia != null)
            {
                var document = documentMedia.Document as TLDocument;
                if (document != null)
                {
                    var attribute = document.Attributes.OfType<TLDocumentAttributeSticker>().FirstOrDefault();
                    if (attribute != null)
                    {
                        if (!string.IsNullOrEmpty(attribute.Alt))
                        {
                            ServiceLabel.Text = $"{attribute.Alt} Sticker";
                        }
                    }
                }
            }

            return true;
        }

        private bool SetDocumentTemplate(TLMessage message, string title)
        {
            var documentMedia = message.Media as TLMessageMediaDocument;
            if (documentMedia != null)
            {
                var document = documentMedia.Document as TLDocument;
                if (document != null)
                {
                    var photoSize = document.Thumb as TLPhotoSize;
                    var photoCachedSize = document.Thumb as TLPhotoCachedSize;
                    if (photoCachedSize != null || photoSize != null)
                    {
                        Visibility = Visibility.Visible;

                        FindName(nameof(ThumbRoot));
                        if (ThumbRoot != null)
                            ThumbRoot.Visibility = Visibility.Visible;

                        ThumbImage.Source = (ImageSource)DefaultPhotoConverter.Convert(documentMedia.Document, "thumbnail");
                    }
                    else
                    {
                        Visibility = Visibility.Visible;

                        if (ThumbRoot != null)
                            ThumbRoot.Visibility = Visibility.Collapsed;
                    }

                    TitleLabel.Text = GetFromLabel(message, title);
                    ServiceLabel.Text = document.FileName;
                    MessageLabel.Text = string.Empty;

                    if (!string.IsNullOrWhiteSpace(documentMedia.Caption))
                    {
                        ServiceLabel.Text += ", ";
                        MessageLabel.Text += documentMedia.Caption.Replace("\r\n", "\n").Replace('\n', ' ');
                    }
                }
            }
            return true;
        }

        private bool SetServiceTextTemplate(TLMessageService message, string title)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            TitleLabel.Text = GetFromLabel(message, title);
            ServiceLabel.Text = string.Empty;
            MessageLabel.Text = ServiceHelper.Convert(message);

            return true;
        }

        private bool SetServicePhotoTemplate(TLMessageService message, string title)
        {
            Visibility = Visibility.Visible;

            FindName(nameof(ThumbRoot));
            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Visible;

            TitleLabel.Text = GetFromLabel(message, title);
            ServiceLabel.Text = string.Empty;
            MessageLabel.Text = ServiceHelper.Convert(message);

            var action = message.Action as TLMessageActionChatEditPhoto;
            if (action != null)
            {
                ThumbImage.Source = (ImageSource)DefaultPhotoConverter.Convert(action.Photo, "thumbnail");
            }

            return true;
        }

        private bool SetEmptyTemplate(TLMessage message, string title)
        {
            Visibility = Visibility.Collapsed;
            return false;
        }

        private bool SetUnsupportedTemplate(TLMessage message, string title)
        {
            Visibility = Visibility.Collapsed;
            return false;
        }

        #endregion

        private string GetFromLabel(TLMessage message, string title)
        {
            if (!string.IsNullOrWhiteSpace(title))
            {
                return title;
            }

            var from = message.From?.FullName ?? string.Empty;
            if (message.ViaBot != null)
            {
                from += $" via @{message.ViaBot.Username}";
            }

            return from;
        }

        private string GetFromLabel(TLMessageService message, string title)
        {
            if (!string.IsNullOrWhiteSpace(title))
            {
                return Title;
            }

            var from = message.From?.FullName ?? string.Empty;
            return from;
        }

        #region Cursor

        // Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Hand, 1);

        protected override void OnPointerEntered(PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Hand, 1);
            base.OnPointerEntered(e);
        }

        protected override void OnPointerExited(PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 1);
            base.OnPointerExited(e);
        }

        #endregion
    }
}
