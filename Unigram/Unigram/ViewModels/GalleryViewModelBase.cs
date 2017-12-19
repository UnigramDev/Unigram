using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Template10.Common;
using Template10.Mvvm;
using Unigram.Common;
using Unigram.Controls.Views;
using Unigram.Converters;
using Unigram.Core.Common;
using Unigram.Helpers;
using Unigram.ViewModels.Chats;
using Unigram.ViewModels.Users;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;

namespace Unigram.ViewModels
{
    public abstract class GalleryViewModelBase : UnigramViewModelBase
    {
        public GalleryViewModelBase(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            StickersCommand = new RelayCommand(StickersExecute);
            ViewCommand = new RelayCommand(ViewExecute);
            DeleteCommand = new RelayCommand(DeleteExecute);
            SaveCommand = new RelayCommand(SaveExecute);
            OpenWithCommand = new RelayCommand(OpenWithExecute);
        }

        public virtual int Position
        {
            get
            {
                return SelectedIndex + 1;
            }
        }

        public int SelectedIndex
        {
            get
            {
                if (Items == null || SelectedItem == null)
                {
                    return 0;
                }

                var index = Items.IndexOf(SelectedItem);
                if (Items.Count > 1)
                {
                    if (index == Items.Count - 1)
                    {
                        LoadNext();
                    }
                    if (index == 0)
                    {
                        LoadPrevious();
                    }
                }

                return index;
            }
        }

        protected int _totalItems;
        public int TotalItems
        {
            get
            {
                return _totalItems;
            }
            set
            {
                Set(ref _totalItems, value);
            }
        }

        protected GalleryItem _selectedItem;
        public GalleryItem SelectedItem
        {
            get
            {
                return _selectedItem;
            }
            set
            {
                Set(ref _selectedItem, value);
                OnSelectedItemChanged(value);
                //RaisePropertyChanged(() => SelectedIndex);
                RaisePropertyChanged(() => Position);
            }
        }

        protected GalleryItem _firstItem;
        public GalleryItem FirstItem
        {
            get
            {
                return _firstItem;
            }
            set
            {
                Set(ref _firstItem, value);
            }
        }

        protected object _poster;
        public object Poster
        {
            get
            {
                return _poster;
            }
            set
            {
                Set(ref _poster, value);
            }
        }

        public MvxObservableCollection<GalleryItem> Items { get; protected set; }

        public virtual MvxObservableCollection<GalleryItem> Group { get; }

        protected virtual void LoadPrevious() { }

        protected virtual void LoadNext() { }

        protected virtual void OnSelectedItemChanged(GalleryItem item) { }

        public virtual bool CanDelete
        {
            get
            {
                return false;
            }
        }

        public virtual bool CanSave
        {
            get
            {
                return true;
            }
        }

        public virtual bool CanOpenWith
        {
            get
            {
                return true;
            }
        }

        public RelayCommand StickersCommand { get; }
        private async void StickersExecute()
        {
            if (_selectedItem != null && _selectedItem.HasStickers)
            {
                var inputStickered = _selectedItem.ToInputStickeredMedia();
                if (inputStickered != null)
                {
                    var response = await ProtoService.GetAttachedStickersAsync(inputStickered);
                    if (response.IsSucceeded)
                    {
                        if (response.Result.Count > 1)
                        {
                            await AttachedStickersView.Current.ShowAsync(response.Result);
                        }
                        else if (response.Result.Count > 0)
                        {
                            await StickerSetView.Current.ShowAsync(response.Result[0]);
                        }
                    }
                }
            }
        }

        public RelayCommand ViewCommand { get; }
        protected virtual void ViewExecute()
        {
            TLMessageCommonBase messageCommon = null;
            if (_selectedItem is GalleryMessageItem messageItem)
            {
                messageCommon = messageItem.Message;
            }
            else if (_selectedItem is GalleryMessageServiceItem serviceItem)
            {
                messageCommon = serviceItem.Message;
            }

            if (messageCommon == null)
            {
                return;
            }

            NavigationService.GoBack();

            var service = WindowWrapper.Current().NavigationServices.GetByFrameId("Main");
            if (service != null)
            {
                service.NavigateToDialog(messageCommon.Parent, messageCommon.Id);
            }
        }

        public RelayCommand DeleteCommand { get; }
        protected virtual void DeleteExecute()
        {
        }

        public RelayCommand SaveCommand { get; }
        protected virtual async void SaveExecute()
        {
            var value = GetTLObjectFromSelectedGalleryItem();

            if (value is TLPhoto photo && photo.Full is TLPhotoSize photoSize)
            {
                await TLFileHelper.SavePhotoAsync(photoSize, photo.Date, false);
            }
            else if (value is TLDocument document)
            {
                await TLFileHelper.SaveDocumentAsync(document, document.Date, false);
            }
        }

        public RelayCommand OpenWithCommand { get; }
        protected virtual async void OpenWithExecute()
        {
            var value = GetTLObjectFromSelectedGalleryItem();

            if (value is TLPhoto photo && photo.Full is TLPhotoSize photoSize)
            {
                var fileName = string.Format("{0}_{1}_{2}.jpg", photoSize.Location.VolumeId, photoSize.Location.LocalId, photoSize.Location.Secret);
                var file = await FileUtils.TryGetTempItemAsync(fileName);
                if (file != null)
                {
                    var options = new LauncherOptions();
                    options.DisplayApplicationPicker = true;

                    await Launcher.LaunchFileAsync(file as StorageFile, options);
                }
            }
            else if (value is TLDocument document)
            {
                var fileName = document.GetFileName();
                var file = await FileUtils.TryGetTempItemAsync(fileName);
                if (file != null)
                {
                    var options = new LauncherOptions();
                    options.DisplayApplicationPicker = true;

                    await Launcher.LaunchFileAsync(file as StorageFile, options);
                }
            }

            // Get the source 
            //var something = (TLPhoto)DefaultPhotoConverter.Convert(_selectedItem.Source);
            //var sizeBase = something.Full;
            //var photoSize = sizeBase as TLPhotoSize;
            //var fileLocation = photoSize.Location as TLFileLocation;
            //fileLocation.   // Find a way to get the IStorageFile of that darn picture

            // Open that file
            //await Windows.System.Launcher.LaunchFileAsync(*INSERT FILE HERE*);
        }

        private object GetTLObjectFromSelectedGalleryItem()
        {
            if (SelectedItem is GalleryMessageItem messageItem)
            {
                if (messageItem.Message.Media is TLMessageMediaPhoto photoMedia)
                {
                    return photoMedia.Photo;
                }

                if (messageItem.Message.Media is TLMessageMediaDocument documentMedia && documentMedia.Document is TLDocument document)
                {
                    return document;
                }
            }

            if (SelectedItem is GalleryMessageServiceItem serviceItem && serviceItem.Message.Action is TLMessageActionChatEditPhoto chatEditPhotoAction)
            {
                return chatEditPhotoAction.Photo;
            }

            if (SelectedItem is GalleryPhotoItem photoItem)
            {
                return photoItem.Photo;
            }

            if (SelectedItem is GalleryDocumentItem documentItem)
            {
                return documentItem.Document;
            }

            return null;
        }
    }

    public class GalleryItem : BindableBase
    {
        public GalleryItem()
        {

        }

        public GalleryItem(ITLTransferable source, string caption, ITLDialogWith from, int date, bool stickers)
        {
            Source = source;
            Caption = caption;
            From = from;
            Date = date;
            HasStickers = stickers;
        }

        public virtual ITLTransferable Source { get; private set; }

        public virtual ITLDialogWith From { get; private set; }

        public virtual string Caption { get; private set; }

        public virtual int Date { get; private set; }

        public virtual bool IsVideo { get; private set; }
        public virtual bool IsLoop { get; private set; }
        public virtual bool IsShareEnabled { get; private set; }

        public virtual bool HasStickers { get; private set; }

        public virtual bool CanView { get; private set; }

        public virtual TLInputStickeredMediaBase ToInputStickeredMedia()
        {
            throw new NotImplementedException();
        }

        public virtual Uri GetVideoSource()
        {
            throw new NotImplementedException();
        }

        public virtual void Share()
        {
            throw new NotImplementedException();
        }
    }


    public class GalleryMessageItem : GalleryItem
    {
        protected readonly TLMessage _message;

        public GalleryMessageItem(TLMessage message)
        {
            _message = message;
        }

        public TLMessage Message => _message;

        public override ITLTransferable Source
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

        //public override ITLDialogWith From => _message.IsPost ? _message.Parent : _message.From;

        public override ITLDialogWith From
        {
            get
            {
                if (_message.HasFwdFrom)
                {
                    return (ITLDialogWith)_message.FwdFrom.Channel ?? _message.FwdFrom.User;
                }

                return _message.IsPost ? _message.Parent : _message.From;
            }
        }

        public override int Date => _message.Date;

        public override bool IsVideo => _message.IsVideo() || _message.IsGif() || _message.IsRoundVideo();

        public override bool IsLoop => _message.IsGif() || _message.IsRoundVideo();

        public override bool IsShareEnabled => _message.Parent != null;

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

        public override bool CanView => true;

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

        public override async void Share()
        {
            if (ShouldShare(_message, true))
            {
                await ShareView.Current.ShowAsync(_message);
            }
            else
            {
                await ShareView.Current.ShowAsync(_message.Media.ToInputMedia());
            }
        }

        private bool ShouldShare(TLMessage message, bool allowOut)
        {
            if (message.IsSticker())
            {
                return false;
            }
            else if (message.HasFwdFrom && message.FwdFrom.HasChannelId && (!message.IsOut || allowOut))
            {
                return true;
            }
            else if (message.HasFromId && !message.IsPost)
            {
                if (message.Media is TLMessageMediaEmpty || message.Media == null || message.Media is TLMessageMediaWebPage webpageMedia && !(webpageMedia.WebPage is TLWebPage))
                {
                    return false;
                }

                var user = message.From;
                if (user != null && user.IsBot)
                {
                    return true;
                }

                if (!message.IsOut || allowOut)
                {
                    if (message.Media is TLMessageMediaGame || message.Media is TLMessageMediaInvoice)
                    {
                        return true;
                    }

                    var parent = message.Parent as TLChannel;
                    if (parent != null && parent.IsMegaGroup)
                    {
                        //TLRPC.Chat chat = MessagesController.getInstance().getChat(messageObject.messageOwner.to_id.channel_id);
                        //return chat != null && chat.username != null && chat.username.length() > 0 && !(messageObject.messageOwner.media instanceof TLRPC.TL_messageMediaContact) && !(messageObject.messageOwner.media instanceof TLRPC.TL_messageMediaGeo);

                        return parent.HasUsername && !(message.Media is TLMessageMediaContact) && !(message.Media is TLMessageMediaGeo);
                    }
                }
            }
            //else if (messageObject.messageOwner.from_id < 0 || messageObject.messageOwner.post)
            else if (message.IsPost)
            {
                //if (messageObject.messageOwner.to_id.channel_id != 0 && (messageObject.messageOwner.via_bot_id == 0 && messageObject.messageOwner.reply_to_msg_id == 0 || messageObject.type != 13))
                //{
                //    return Visibility.Visible;
                //}

                if (message.ToId is TLPeerChannel && (!message.HasViaBotId && !message.HasReplyToMsgId))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public class GalleryMessageServiceItem : GalleryItem
    {
        private readonly TLMessageService _message;

        public GalleryMessageServiceItem(TLMessageService message)
        {
            _message = message;
        }

        public TLMessageService Message => _message;

        public override ITLTransferable Source
        {
            get
            {
                if (_message.Action is TLMessageActionChatEditPhoto chatEditPhotoAction && chatEditPhotoAction.Photo is TLPhoto photo)
                {
                    return photo;
                }

                return null;
            }
        }

        //public override ITLDialogWith From => _message.IsPost ? _message.Parent : _message.From;

        public override ITLDialogWith From
        {
            get
            {
                return _message.IsPost ? _message.Parent : _message.From;
            }
        }

        public override int Date => _message.Date;

        public override bool HasStickers
        {
            get
            {
                if (_message.Action is TLMessageActionChatEditPhoto chatEditPhotoAction && chatEditPhotoAction.Photo is TLPhoto photo)
                {
                    return photo.IsHasStickers;
                }

                return false;
            }
        }

        public override bool CanView => true;

        public override TLInputStickeredMediaBase ToInputStickeredMedia()
        {
            if (_message.Action is TLMessageActionChatEditPhoto chatEditPhotoAction && chatEditPhotoAction.Photo is TLPhoto photo)
            {
                return new TLInputStickeredMediaPhoto { Id = photo.ToInputPhoto() };
            }

            return null;
        }
    }

    public class GalleryPhotoItem : GalleryItem
    {
        private readonly TLPhoto _photo;
        private readonly ITLDialogWith _from;
        private readonly string _caption;

        public GalleryPhotoItem(TLPhoto photo, ITLDialogWith from)
        {
            _photo = photo;
            _from = from;
        }

        public GalleryPhotoItem(TLPhoto photo, string caption)
        {
            _photo = photo;
            _caption = caption;
        }

        public TLPhoto Photo => _photo;

        public override ITLTransferable Source => _photo;

        public override string Caption => _caption;

        public override ITLDialogWith From => _from;

        public override int Date => _photo.Date;

        public override bool HasStickers => _photo.IsHasStickers;

        public override TLInputStickeredMediaBase ToInputStickeredMedia()
        {
            return new TLInputStickeredMediaPhoto { Id = _photo.ToInputPhoto() };
        }
    }

    public class GalleryDocumentItem : GalleryItem
    {
        private readonly TLDocument _document;
        private readonly ITLDialogWith _from;
        private readonly string _caption;

        public GalleryDocumentItem(TLDocument document, ITLDialogWith from)
        {
            _document = document;
            _from = from;
        }

        public GalleryDocumentItem(TLDocument document, string caption)
        {
            _document = document;
            _caption = caption;
        }

        public TLDocument Document => _document;

        public override ITLTransferable Source => _document;

        public override string Caption => _caption;

        public override ITLDialogWith From => _from;

        public override int Date => _document.Date;

        public override bool IsVideo => TLMessage.IsVideo(_document) || TLMessage.IsGif(_document) || TLMessage.IsRoundVideo(_document);

        public override bool IsLoop => TLMessage.IsGif(_document) || TLMessage.IsRoundVideo(_document);

        public override bool HasStickers => _document.Attributes.Any(x => x is TLDocumentAttributeHasStickers);

        public override TLInputStickeredMediaBase ToInputStickeredMedia()
        {
            return new TLInputStickeredMediaDocument { Id = _document.ToInputDocument() };
        }

        public override Uri GetVideoSource()
        {
            return FileUtils.GetTempFileUri(_document.GetFileName());
        }
    }
}
