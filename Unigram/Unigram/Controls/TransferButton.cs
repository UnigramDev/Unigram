using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Views;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class TransferButton : GlyphHyperlinkButton
    {
        public TransferButton()
        {
            Click += OnClick;
        }

        public event EventHandler<TransferCompletedEventArgs> Completed;

        private void OnClick(object sender, RoutedEventArgs e)
        {
            if (Transferable is TLPhoto photo)
            {
                if (photo.Full is TLPhotoSize photoSize)
                {
                    var fileName = string.Format("{0}_{1}_{2}.jpg", photoSize.Location.VolumeId, photoSize.Location.LocalId, photoSize.Location.Secret);
                    if (File.Exists(FileUtils.GetTempFileName(fileName)))
                    {
                        Completed?.Invoke(this, new TransferCompletedEventArgs(fileName));
                    }
                    else
                    {
                        if (photo.DownloadingProgress > 0 && photo.DownloadingProgress < 1)
                        {
                            var manager = UnigramContainer.Current.ResolveType<IDownloadFileManager>();
                            photo.Cancel(manager, null);
                        }
                        else if (photo.UploadingProgress > 0 && photo.UploadingProgress < 1)
                        {
                            var manager = UnigramContainer.Current.ResolveType<IUploadFileManager>();
                            photo.Cancel(null, manager);
                        }
                        else
                        {
                            var context = DefaultPhotoConverter.BitmapContext[photo, false];
                        }
                    }
                }
            }
            else if (Transferable is TLDocument document)
            {
                var fileName = document.GetFileName();
                if (File.Exists(FileUtils.GetTempFileName(fileName)))
                {
                    Completed?.Invoke(this, new TransferCompletedEventArgs(fileName));
                }
                else
                {
                    if (document.DownloadingProgress > 0 && document.DownloadingProgress < 1)
                    {
                        var manager = ChooseDownloadManager(document);
                        document.Cancel(manager, null);
                    }
                    else if (document.UploadingProgress > 0 && document.UploadingProgress < 1)
                    {
                        var manager = ChooseUploadManager(document);
                        document.Cancel(null, manager);
                    }
                    else
                    {
                        var manager = ChooseDownloadManager(document);
                        document.DownloadAsync(manager);
                    }
                }
            }
        }

        private IDownloadManager ChooseDownloadManager(TLDocument document)
        {
            if (TLMessage.IsVideo(document))
            {
                return UnigramContainer.Current.ResolveType<IDownloadVideoFileManager>();
            }

            return UnigramContainer.Current.ResolveType<IDownloadDocumentFileManager>();
        }

        private IUploadManager ChooseUploadManager(TLDocument document)
        {
            if (TLMessage.IsVideo(document))
            {
                return UnigramContainer.Current.ResolveType<IUploadVideoManager>();
            }

            return UnigramContainer.Current.ResolveType<IUploadDocumentManager>();
        }

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

        #region IsTransferring

        public bool IsTransferring
        {
            get { return (bool)GetValue(IsTransferringProperty); }
            set { SetValue(IsTransferringProperty, value); }
        }

        public static readonly DependencyProperty IsTransferringProperty =
            DependencyProperty.Register("IsTransferring", typeof(bool), typeof(TransferButton), new PropertyMetadata(false, OnIsTransferringChanged));

        private static void OnIsTransferringChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TransferButton)d).Update();
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
                return UpdateGlyph(document);
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
                else if (photo.IsTransferring)
                {
                    return "\uE10A";
                }
                else if (photo.DownloadingProgress > 0 && photo.DownloadingProgress < 1)
                {
                    return "\uE10A";
                }
                else if (photo.UploadingProgress > 0 && photo.DownloadingProgress < 1)
                {
                    return "\uE10A";
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
                if (TLMessage.IsVideo(document) || TLMessage.IsRoundVideo(document) || TLMessage.IsGif(document) || TLMessage.IsMusic(document))
                {
                    return "\uE102";
                }

                return "\uE160";
            }
            else if (document.IsTransferring)
            {
                return "\uE10A";
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

    public class TransferCompletedEventArgs : EventArgs
    {
        public string FileName { get; private set; }

        public TransferCompletedEventArgs(string fileName)
        {
            FileName = fileName;
        }
    }
}