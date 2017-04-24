using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.FileManager;
using Telegram.Api.Services.FileManager.EventArgs;
using Telegram.Api.TL;
using Unigram.Common;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class DialogPhotosViewModel : GalleryViewModelBase
    {
        private readonly DisposableMutex _loadMoreLock = new DisposableMutex();
        private readonly TLInputPeerBase _peer;

        private int _lastMaxId;

        public DialogPhotosViewModel(TLInputPeerBase peer, TLMessage selected, IMTProtoService protoService)
            : base(protoService, null, null)
        {
            if (selected.Media is TLMessageMediaPhoto photoMedia || selected.IsVideo())
            {
                Items = new ObservableCollection<GalleryItem> { new GalleryMessageItem(selected) };
                SelectedItem = Items[0];
            }
            else
            {
                Items = new ObservableCollection<GalleryItem>();
            }

            _peer = peer;
            _lastMaxId = selected.Id;

            Initialize();
        }

        private async void Initialize()
        {
            using (await _loadMoreLock.WaitAsync())
            {
                var result = await ProtoService.SearchAsync(_peer, string.Empty, new TLInputMessagesFilterPhotoVideo(), 0, 0, -5, _lastMaxId, 15);
                if (result.IsSucceeded)
                {
                    if (result.Result is TLMessagesMessagesSlice)
                    {
                        var slice = result.Result as TLMessagesMessagesSlice;
                        TotalItems = slice.Count;
                    }
                    else
                    {
                        TotalItems = result.Result.Messages.Count;
                    }

                    //Items.Clear();

                    foreach (var photo in result.Result.Messages)
                    {
                        if (photo is TLMessage message && (message.Media is TLMessageMediaPhoto media || message.IsVideo()))
                        {
                            Items.Add(new GalleryMessageItem(message));
                        }
                    }

                    SelectedItem = Items[0];
                }
            }
        }

        //protected override async void LoadNext()
        //{
        //    if (User != null)
        //    {
        //        using (await _loadMoreLock.WaitAsync())
        //        {
        //            var result = await ProtoService.GetUserPhotosAsync(User.ToInputUser(), Items.Count, 0, 0);
        //            if (result.IsSucceeded)
        //            {
        //                foreach (var photo in result.Value.Photos)
        //                {
        //                    Items.Add(photo);
        //                }
        //            }
        //        }
        //    }
        //}
    }

    public class GalleryMessageItem : GalleryItem
    {
        private readonly TLMessage _message;

        public GalleryMessageItem(TLMessage message)
        {
            _message = message;
        }

        public TLMessage Message => _message;

        public override object Source
        {
            get
            {
                if (_message.Media is TLMessageMediaPhoto photoMedia && photoMedia.Photo is TLPhoto photo)
                {
                    return photo;
                }
                else if (_message.Media is TLMessageMediaDocument documentMedia && documentMedia.Document is TLDocument document)
                {
                    return document;
                }

                return null;
            }
        }

        public override string Caption
        {
            get
            {
                if (_message.Media is ITLMessageMediaCaption captionMedia)
                {
                    return captionMedia.Caption;
                }

                return null;
            }
        }

        public override ITLDialogWith From => _message.From;

        public override int Date => _message.Date;

        public override bool IsVideo
        {
            get
            {
                return _message.IsVideo();
            }
        }

        public override bool HasStickers
        {
            get
            {
                if (_message.Media is TLMessageMediaPhoto photoMedia && photoMedia.Photo is TLPhoto photo)
                {
                    return photo.IsHasStickers;
                }
                else if (_message.Media is TLMessageMediaDocument documentMedia && documentMedia.Document is TLDocument document)
                {
                    return document.Attributes.Any(x => x is TLDocumentAttributeHasStickers);
                }

                return false;
            }
        }

        public override TLInputStickeredMediaBase ToInputStickeredMedia()
        {
            if (_message.Media is TLMessageMediaPhoto photoMedia && photoMedia.Photo is TLPhoto photo)
            {
                return new TLInputStickeredMediaPhoto { Id = photo.ToInputPhoto() };
            }
            else if (_message.Media is TLMessageMediaDocument documentMedia && documentMedia.Document is TLDocument document)
            {
                return new TLInputStickeredMediaDocument { Id = document.ToInputDocument() };
            }

            return null;
        }

        public override Uri GetVideoSource()
        {
            if (_message.Media is TLMessageMediaDocument documentMedia && documentMedia.Document is TLDocument document)
            {
                return FileUtils.GetTempFileUri(document.GetFileName());
            }

            return null;
        }
    }
}
