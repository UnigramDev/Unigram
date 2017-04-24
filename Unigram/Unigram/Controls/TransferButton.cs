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
        #region Transferable

        public ITLTransferable Transferable
        {
            get { return (ITLTransferable)GetValue(TransferableProperty); }
            set { SetValue(TransferableProperty, value); }
        }

        public static readonly DependencyProperty TransferableProperty =
            DependencyProperty.Register("Transferable", typeof(ITLTransferable), typeof(TransferButton), new PropertyMetadata(null, OnTransferableChanged));

        private static void OnTransferableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TransferButton)d).OnTransferableChanged(e.NewValue as ITLTransferable, e.OldValue as ITLTransferable);
        }

        private void OnTransferableChanged(ITLTransferable newValue, ITLTransferable oldValue)
        {
            Glyph = UpdateGlyph(newValue, oldValue);
        }

        #endregion

        public void Update()
        {
            OnTransferableChanged(Transferable, null);
        }

        private string UpdateGlyph(ITLTransferable newValue, ITLTransferable oldValue)
        {
            if (newValue is TLPhoto photo)
            {
                return UpdateGlyph(photo);
            }
            else if (newValue is TLDocument document)
            {
                UpdateGlyph(document);
            }

            return "\uE118";
        }

        private string UpdateGlyph(TLPhoto photo)
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

            return "\uE118";
        }

        private string UpdateGlyph(TLDocument document)
        {
            Visibility = Visibility.Visible;

            var fileName = document.GetFileName();
            if (File.Exists(FileUtils.GetTempFileName(fileName)))
            {
                if (TLMessage.IsVideo(document))
                {
                    return "\uE102";
                }

                return "\uE160";
            }
            else if (document.DownloadingProgress > 0 && document.DownloadingProgress < 1)
            {
                return "\uE10A";
            }
            else if (document.UploadingProgress > 0 && document.DownloadingProgress < 1)
            {
                return "\uE10A";
            }

            return "\uE118";
        }
    }
}