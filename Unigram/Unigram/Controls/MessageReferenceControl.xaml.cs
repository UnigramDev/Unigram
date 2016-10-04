using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Windows.Foundation;
using Windows.Foundation.Collections;
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
    public sealed partial class MessageReferenceControl : Grid
    {
        public static IValueConverter DefaultPhotoConverter;

        static MessageReferenceControl()
        {
            if (App.Current.Resources.ContainsKey("DefaultPhotoConverter"))
            {
                DefaultPhotoConverter = (IValueConverter)App.Current.Resources["DefaultPhotoConverter"];
            }
        }

        public MessageReferenceControl()
        {
            InitializeComponent();
        }

        public bool IsPinned { get; set; }

        public TLObject Message
        {
            get { return (TLObject)GetValue(MessageProperty); }
            set { SetValue(MessageProperty, value); }
        }

        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register("Message", typeof(TLObject), typeof(MessageReferenceControl), new PropertyMetadata(null, OnMessageChanged));

        private static void OnMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((MessageReferenceControl)d).GetMessageTemplate((TLObject)e.NewValue);
        }

        #region Reply

        private bool GetMessageTemplate(TLObject obj)
        {
            Visibility = Visibility.Collapsed;

            var message = obj as TLMessage;
            if (message != null)
            {
                if (!string.IsNullOrEmpty(message.Message) && (message.Media == null || message.Media is TLMessageMediaEmpty || message.Media is TLMessageMediaWebPage))
                {
                    return ReplyTextTemplate(message);
                }

                var media = message.Media;
                if (media != null)
                {
                    switch (media.TypeId)
                    {
                        case TLType.MessageMediaPhoto:
                            return ReplyPhotoTemplate(message);
                        case TLType.MessageMediaGeo:
                            return ReplyGeoPointTemplate(message);
                        case TLType.MessageMediaVenue:
                            return ReplyVenueTemplate(message);
                        case TLType.MessageMediaContact:
                            return ReplyContactTemplate(message);
                        case TLType.MessageMediaEmpty:
                            return ReplyUnsupportedTemplate(message);
                        case TLType.MessageMediaDocument:
                            if (message.IsVoice())
                            {
                                return ReplyVoiceMessageTemplate(message);
                            }
                            if (message.IsVideo())
                            {
                                return ReplyVideoTemplate(message);
                            }
                            if (message.IsGif())
                            {
                                return ReplyGifTemplate(message);
                            }
                            if (message.IsSticker())
                            {
                                return ReplyStickerTemplate(message);
                            }

                            return ReplyDocumentTemplate(message);
                    }
                }
            }

            var serviceMessage = obj as TLMessageService;
            if (serviceMessage != null)
            {
                var action = serviceMessage.Action;
                if (action is TLMessageActionChatEditPhoto)
                {
                    return ReplyServicePhotoTemplate(message);
                }

                return ReplyServiceTextTemplate(message);
            }
            else
            {
                var emptyMessage = obj as TLMessageEmpty;
                if (emptyMessage != null)
                {
                    return ReplyEmptyTemplate(message);
                }

                return ReplyUnsupportedTemplate(message);
            }
        }

        private bool ReplyTextTemplate(TLMessage message)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            TitleLabel.Text = IsPinned ? "Pinned message" : message.From?.FullName ?? string.Empty;
            ServiceLabel.Text = string.Empty;
            MessageLabel.Text = message.Message;

            return true;
        }

        private bool ReplyPhotoTemplate(TLMessage message)
        {
            Visibility = Visibility.Visible;

            FindName(nameof(ThumbRoot));
            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Visible;

            TitleLabel.Text = IsPinned ? "Pinned message" : message.From?.FullName ?? string.Empty;
            ServiceLabel.Text = "Photo";

            var photoMedia = message.Media as TLMessageMediaPhoto;
            if (photoMedia != null)
            {
                if (!string.IsNullOrWhiteSpace(photoMedia.Caption))
                {
                    ServiceLabel.Text += ",";
                    MessageLabel.Text += photoMedia.Caption;
                }

                ThumbImage.Source = (ImageSource)DefaultPhotoConverter.Convert(photoMedia, null, null, null);
            }

            return true;
        }

        private bool ReplyGeoPointTemplate(TLMessage message)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            TitleLabel.Text = IsPinned ? "Pinned message" : message.From?.FullName ?? string.Empty;
            ServiceLabel.Text = "Location";
            MessageLabel.Text = string.Empty;

            return true;
        }

        private bool ReplyVenueTemplate(TLMessage message)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            TitleLabel.Text = IsPinned ? "Pinned message" : message.From?.FullName ?? string.Empty;
            ServiceLabel.Text = "Location";
            MessageLabel.Text = string.Empty;

            var venueMedia = message.Media as TLMessageMediaVenue;
            if (venueMedia != null && !string.IsNullOrWhiteSpace(venueMedia.Title))
            {
                ServiceLabel.Text += ",";
                MessageLabel.Text = venueMedia.Title;
            }

            return true;
        }

        private bool ReplyContactTemplate(TLMessage message)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            TitleLabel.Text = IsPinned ? "Pinned message" : message.From?.FullName ?? string.Empty;
            ServiceLabel.Text = "Contact";
            MessageLabel.Text = string.Empty;

            return true;
        }

        private bool ReplyVoiceMessageTemplate(TLMessage message)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            TitleLabel.Text = IsPinned ? "Pinned message" : message.From?.FullName ?? string.Empty;
            ServiceLabel.Text = "Audio";
            MessageLabel.Text = string.Empty;

            return true;
        }

        private bool ReplyVideoTemplate(TLMessage message)
        {
            Visibility = Visibility.Visible;

            FindName(nameof(ThumbRoot));
            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Visible;

            TitleLabel.Text = IsPinned ? "Pinned message" : message.From?.FullName ?? string.Empty;
            ServiceLabel.Text = "Video";
            MessageLabel.Text = string.Empty;

            var documentMedia = message.Media as TLMessageMediaDocument;
            if (documentMedia != null)
            {
                if (!string.IsNullOrWhiteSpace(documentMedia.Caption))
                {
                    ServiceLabel.Text += ",";
                    MessageLabel.Text += documentMedia.Caption;
                }

                ThumbImage.Source = (ImageSource)DefaultPhotoConverter.Convert(documentMedia.Document, null, null, null);
            }

            return true;
        }

        private bool ReplyGifTemplate(TLMessage message)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            TitleLabel.Text = IsPinned ? "Pinned message" : message.From?.FullName ?? string.Empty;
            ServiceLabel.Text = "GIF";
            MessageLabel.Text = string.Empty;

            return true;
        }

        private bool ReplyStickerTemplate(TLMessage message)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            TitleLabel.Text = IsPinned ? "Pinned message" : message.From?.FullName ?? string.Empty;
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

        private bool ReplyDocumentTemplate(TLMessage message)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            TitleLabel.Text = IsPinned ? "Pinned message" : message.From?.FullName ?? string.Empty;
            ServiceLabel.Text = message.Message;
            MessageLabel.Text = string.Empty;

            return true;
        }

        private bool ReplyServiceTextTemplate(TLMessage message)
        {
            Visibility = Visibility.Collapsed;
            return false;
        }

        private bool ReplyServicePhotoTemplate(TLMessage message)
        {
            Visibility = Visibility.Collapsed;
            return false;
        }

        private bool ReplyEmptyTemplate(TLMessage message)
        {
            Visibility = Visibility.Collapsed;
            return false;
        }

        private bool ReplyUnsupportedTemplate(TLMessage message)
        {
            Visibility = Visibility.Collapsed;
            return false;
        }

        #endregion
    }
}
