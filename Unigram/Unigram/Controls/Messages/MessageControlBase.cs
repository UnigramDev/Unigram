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
