using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class TransferButton : GlyphHyperlinkButton
    {
        #region Media

        public TLMessageMediaBase Media
        {
            get { return (TLMessageMediaBase)GetValue(MediaProperty); }
            set { SetValue(MediaProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Media.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MediaProperty =
            DependencyProperty.Register("Media", typeof(TLMessageMediaBase), typeof(TransferButton), new PropertyMetadata(null, OnMediaChanged));

        private static void OnMediaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TransferButton)d).OnMediaChanged((TLMessageMediaBase)e.NewValue, (TLMessageMediaBase)e.OldValue);
        }

        private void OnMediaChanged(TLMessageMediaBase newValue, TLMessageMediaBase oldValue)
        {
            Glyph = UpdateGlyph(newValue, oldValue);
        }

        #endregion

        public void Update()
        {
            OnMediaChanged(Media, Media);
        }

        private string UpdateGlyph(TLMessageMediaBase newValue, TLMessageMediaBase oldValue)
        {
            if (newValue is TLMessageMediaPhoto photoMedia)
            {
                if (photoMedia.Photo is TLPhoto photo)
                {
                    if (photo.Full is TLPhotoSize photoSize)
                    {
                        var fileName = string.Format("{0}_{1}_{2}.jpg", photoSize.Location.VolumeId, photoSize.Location.LocalId, photoSize.Location.Secret);
                        if (File.Exists(FileUtils.GetTempFileName(fileName)))
                        {
                            Visibility = Visibility.Collapsed;
                            return "\uE160";
                        }

                        Visibility = Visibility.Visible;
                        return "\uE118";
                    }
                }
            }

            if (newValue is TLMessageMediaDocument documentMedia)
            {
                Visibility = Visibility.Visible;

                if (documentMedia.Document is TLDocument document)
                {
                    var fileName = document.GetFileName();
                    if (File.Exists(FileUtils.GetTempFileName(fileName)))
                    {
                        if (TLMessage.IsVideo(document))
                        {
                            return "\uE102";
                        }

                        return "\uE160";
                    }
                    else if (documentMedia.DownloadingProgress > 0 && documentMedia.DownloadingProgress < 1)
                    {
                        return "\uE10A";
                    }
                    else if (documentMedia.UploadingProgress > 0 && documentMedia.DownloadingProgress < 1)
                    {
                        return "\uE10A";
                    }

                    return "\uE118";
                }
            }

            return "\uE118";
        }
    }
}
