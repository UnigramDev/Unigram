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
using Unigram.ViewModels.Chats;
using Unigram.ViewModels.Users;
using Windows.Storage;
using Windows.System;

namespace Unigram.ViewModels
{
    public abstract class GalleryViewModelBase : UnigramViewModelBase
    {
        public GalleryViewModelBase(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
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
                if (index == Items.Count - 1)
                {
                    LoadNext();
                }
                if (index == 0)
                {
                    LoadPrevious();
                }

                return index + 1;
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
                RaisePropertyChanged(() => SelectedIndex);
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

        protected virtual void LoadPrevious() { }

        protected virtual void LoadNext() { }

        public virtual bool CanGoto
        {
            get
            {
                return false;
            }
        }

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
                return false;
            }
        }

        public virtual bool CanOpenWith
        {
            get
            {
                return true;
            }
        }

        public RelayCommand StickersCommand => new RelayCommand(StickersExecute);
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

        public RelayCommand GotoCommand => new RelayCommand(GotoExecute);
        protected virtual void GotoExecute()
        {
            NavigationService.GoBack();

            TLMessageCommonBase messageCommon = null;
            if (_selectedItem is GalleryMessageItem messageItem)
            {
                messageCommon = messageItem.Message;
            }
            else if (_selectedItem is GalleryMessageServiceItem serviceItem)
            {
                messageCommon = serviceItem.Message;
            }

            var service = WindowWrapper.Current().NavigationServices.GetByFrameId("Main");
            if (service != null && messageCommon != null)
            {
                service.NavigateToDialog(messageCommon.Parent, messageCommon.Id);
            }
        }

        public RelayCommand DeleteCommand => new RelayCommand(DeleteExecute);
        protected virtual void DeleteExecute()
        {
        }

        public RelayCommand OpenWithCommand => new RelayCommand(OpenWithExecute);
        protected virtual async void OpenWithExecute()
        {
            object value = null;

            if (SelectedItem is GalleryMessageItem messageItem)
            {
                if (messageItem.Message.Media is TLMessageMediaPhoto photoMedia)
                {
                    value = photoMedia.Photo;
                }
                else if (messageItem.Message.Media is TLMessageMediaDocument documentMedia && documentMedia.Document is TLDocument document)
                {
                    value = document;
                }
            }
            else if (SelectedItem is GalleryMessageServiceItem serviceItem && serviceItem.Message.Action is TLMessageActionChatEditPhoto chatEditPhotoAction)
            {
                value = chatEditPhotoAction.Photo;
            }
            else if (SelectedItem is GalleryPhotoItem photoItem)
            {
                value = photoItem.Photo;
            }
            else if (SelectedItem is GalleryDocumentItem documentItem)
            {
                value = documentItem.Document;
            }

            if (value is TLPhoto photo && photo.Full is TLPhotoSize photoSize)
            {
                var fileName = string.Format("{0}_{1}_{2}.jpg", photoSize.Location.VolumeId, photoSize.Location.LocalId, photoSize.Location.Secret);
                var file = await FileUtils.TryGetTempFileAsync(fileName);
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
                var file = await FileUtils.TryGetTempFileAsync(fileName);
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

        public virtual string Caption { get; private set; }

        public virtual ITLDialogWith From { get; private set; }

        public virtual int Date { get; private set; }

        public virtual bool IsVideo { get; private set; }

        public virtual bool IsLoop { get; private set; }

        public virtual bool IsShareEnabled { get; private set; }

        public virtual bool HasStickers { get; private set; }

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
}
