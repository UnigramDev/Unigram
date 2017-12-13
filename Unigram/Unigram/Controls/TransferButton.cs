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
using LinqToVisualTree;

namespace Unigram.Controls
{
    public class TransferButton : GlyphHyperlinkButton
    {
        private bool _unloaded;

        public TransferButton()
        {
            Click += OnClick;

            Loaded += (s, args) => _unloaded = false;
            Unloaded += (s, args) => _unloaded = true;
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
                        Update();
                        Completed?.Invoke(this, new TransferCompletedEventArgs(fileName));
                    }
                    else
                    {
                        if (photo.IsTransferring)
                        {
                            photo.Cancel(UnigramContainer.Current.ResolveType<IDownloadFileManager>(), UnigramContainer.Current.ResolveType<IUploadFileManager>());
                        }
                        else
                        {
                            photo.DownloadAsync(UnigramContainer.Current.ResolveType<IDownloadFileManager>(), CompletedPhoto);
                        }
                    }
                }
            }
            else if (Transferable is TLDocument document)
            {
                var fileName = document.GetFileName();
                if (File.Exists(FileUtils.GetTempFileName(fileName)))
                {
                    Update();
                    Completed?.Invoke(this, new TransferCompletedEventArgs(fileName));
                }
                else
                {
                    if (document.IsTransferring)
                    {
                        document.Cancel(ChooseDownloadManager(document), ChooseUploadManager(document));
                    }
                    else
                    {
                        document.DownloadAsync(ChooseDownloadManager(document), CompletedDocument);
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

        #region Progress

        public double Progress
        {
            get { return (double)GetValue(ProgressProperty); }
            set { SetValue(ProgressProperty, value); }
        }

        public static readonly DependencyProperty ProgressProperty =
            DependencyProperty.Register("Progress", typeof(double), typeof(TransferButton), new PropertyMetadata(0.0));

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

        #region ContentVisibility

        public Visibility ContentVisibility
        {
            get { return (Visibility)GetValue(ContentVisibilityProperty); }
            set { SetValue(ContentVisibilityProperty, value); }
        }

        public static readonly DependencyProperty ContentVisibilityProperty =
            DependencyProperty.Register("ContentVisibility", typeof(Visibility), typeof(TransferButton), new PropertyMetadata(Visibility.Collapsed));

        #endregion

        private void CompletedPhoto(TLPhoto photo)
        {
            var context = DefaultPhotoConverter.BitmapContext[photo, false];
            Update();
        }

        private void CompletedDocument(TLDocument document)
        {
            Update();
        }

        public void Update()
        {
            if (_unloaded)
            {
                return;
            }

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
                if (photo.DownloadingProgress > 0 && photo.DownloadingProgress < 1)
                {
                    ContentVisibility = Visibility.Visible;
                    Visibility = Visibility.Visible;
                    return "\uE10A";
                }
                else if (photo.UploadingProgress > 0 && photo.UploadingProgress < 1)
                {
                    Visibility = Visibility.Visible;
                    return "\uE10A";
                }
                else if (File.Exists(FileUtils.GetTempFileName(string.Format("{0}_{1}_{2}.jpg", photoSize.Location.VolumeId, photoSize.Location.LocalId, photoSize.Location.Secret))))
                {
                    ContentVisibility = Visibility.Collapsed;

                    var message = DataContext as TLMessage;
                    if (message != null && message.Media is TLMessageMediaPhoto photoMedia && photoMedia.HasTTLSeconds)
                    {
                        return "\uE60D";
                    }

                    Visibility = Visibility.Collapsed;
                    return "\uE160";
                }

                ContentVisibility = Visibility.Visible;
                Visibility = Visibility.Visible;
                return "\uE118";
            }

            return "\uE118";
        }

        private string UpdateGlyph(TLDocument document)
        {
            ContentVisibility = Visibility.Visible;
            Visibility = Visibility.Visible;

            if (document.DownloadingProgress > 0 && document.DownloadingProgress < 1)
            {
                ContentVisibility = Visibility.Visible;
                return "\uE10A";
            }
            else if (document.UploadingProgress > 0 && document.UploadingProgress < 1)
            {
                ContentVisibility = Visibility.Collapsed;
                return "\uE10A";
            }
            else if (File.Exists(FileUtils.GetTempFileName(document.GetFileName())))
            {
                ContentVisibility = Visibility.Collapsed;

                var message = DataContext as TLMessage;
                if (message != null && message.Media is TLMessageMediaDocument documentMedia && documentMedia.HasTTLSeconds)
                {
                    return "\uE60D";
                }

                if (TLMessage.IsVideo(document) || TLMessage.IsRoundVideo(document) || TLMessage.IsMusic(document) || TLMessage.IsVoice(document))
                {
                    return "\uE102";
                }
                else if (TLMessage.IsGif(document))
                {
                    //Visibility = Visibility.Collapsed;
                    return "\uE906";
                }

                return "\uE160";
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