//
// Copyright Fela Ameghino & Contributors 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;

namespace Telegram.ViewModels.Gallery
{
    public abstract class GalleryViewModelBase : ViewModelBase
    {
        private readonly IStorageService _storageService;

        protected int _additionalPhotos;
        protected bool _hasProtectedContent;

        public GalleryViewModelBase(IClientService clientService, IStorageService storageService, IEventAggregator aggregator)
            : base(clientService, null, aggregator)
        {
            _storageService = storageService;
            //Aggregator.Subscribe(this);
        }

        //public void Handle(UpdateFile update)
        //{
        //    BeginOnUIThread(() => Delegate?.UpdateFile(update.File));
        //}

        //protected override void BeginOnUIThread(Action action)
        //{
        //    // This is somehow needed because this viewmodel requires a Dispatcher
        //    // in some situations where base one might be null.
        //    Execute.BeginOnUIThread(action);
        //}

        public bool HasProtectedContent => _hasProtectedContent;

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

                return Items.IndexOf(SelectedItem);
            }
        }

        protected int _totalItems;
        public int TotalItems
        {
            get => _totalItems;
            set
            {
                Set(ref _totalItems, value);
                RaisePropertyChanged(nameof(Position));
            }
        }

        protected GalleryMedia _selectedItem;
        public GalleryMedia SelectedItem
        {
            get => _selectedItem;
            set
            {
                Set(ref _selectedItem, value);
                OnSelectedItemChanged(value);
                //RaisePropertyChanged(() => SelectedIndex);
            }
        }

        protected GalleryMedia _firstItem;
        public GalleryMedia FirstItem
        {
            get => _firstItem;
            set => Set(ref _firstItem, value);
        }

        protected object _poster;
        public object Poster
        {
            get => _poster;
            set => Set(ref _poster, value);
        }

        public MvxObservableCollection<GalleryMedia> Items { get; protected set; }

        public virtual MvxObservableCollection<GalleryMedia> Group { get; }

        public void LoadMore()
        {
            if (Items.Count > 1)
            {
                var index = SelectedIndex;
                if (index == Items.Count - 1)
                {
                    LoadNext();
                }
                if (index == 0)
                {
                    LoadPrevious();
                }
            }
        }

        protected virtual void LoadPrevious() { }
        protected virtual void LoadNext() { }

        protected virtual void OnSelectedItemChanged(GalleryMedia item)
        {
            RaisePropertyChanged(nameof(Position));

            if (item == null || NavigationService == null)
            {
                return;
            }

            if (item.IsProtected && !_hasProtectedContent)
            {
                _hasProtectedContent = true;
                NavigationService.Window.DisableScreenCapture(GetHashCode());
            }
            else if (_hasProtectedContent && !item.IsProtected)
            {
                _hasProtectedContent = false;
                NavigationService.Window.EnableScreenCapture(GetHashCode());
            }
        }

        public override INavigationService NavigationService
        {
            get => base.NavigationService;
            set
            {
                base.NavigationService = value;
                OnSelectedItemChanged(SelectedItem);
            }
        }

        public virtual bool CanDelete
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
                if (SelectedItem is GalleryMessage message && message.IsProtected)
                {
                    return false;
                }

                return true;
            }
        }

        public async void OpenStickers()
        {
            if (_selectedItem != null && _selectedItem.HasStickers)
            {
                if (_selectedItem is GalleryChatPhoto chatPhoto)
                {
                    if (chatPhoto.Sticker?.Type is ChatPhotoStickerTypeRegularOrMask regularOrMask)
                    {
                        await StickersPopup.ShowAsync(NavigationService, regularOrMask.StickerSetId);
                    }
                    else if (chatPhoto.Sticker?.Type is ChatPhotoStickerTypeCustomEmoji customEmoji)
                    {
                        var response = await ClientService.SendAsync(new GetCustomEmojiStickers(new[] { customEmoji.CustomEmojiId }));
                        if (response is Stickers stickers && stickers.StickersValue.Count == 1)
                        {
                            await StickersPopup.ShowAsync(NavigationService, stickers.StickersValue[0].SetId);
                        }
                    }
                }
                else
                {
                    var file = _selectedItem.GetFile();
                    if (file == null)
                    {
                        return;
                    }

                    await StickersPopup.ShowAsync(NavigationService, new InputFileId(file.Id));
                }
            }
        }

        public virtual void View()
        {
            FirstItem = null;

            var message = _selectedItem as GalleryMessage;
            if (message == null || !message.CanView)
            {
                return;
            }

            NavigationService.NavigateToChat(message.ChatId, message: message.Id);
        }

        public virtual async void Forward()
        {
            if (_selectedItem is GalleryMessage message)
            {
                await ShowPopupAsync(typeof(ChooseChatsPopup), new ChooseChatsConfigurationShareMessage(message.ChatId, message.Id));
            }
            else
            {
                var input = _selectedItem?.ToInput();
                if (input == null)
                {
                    return;
                }

                await ShowPopupAsync(typeof(ChooseChatsPopup), new ChooseChatsConfigurationPostMessage(input));
            }
        }

        public virtual void Delete()
        {
        }

        public async void Copy()
        {
            var item = _selectedItem;
            if (item == null || !item.CanCopy)
            {
                return;
            }

            var file = item.GetFile();
            if (file == null)
            {
                return;
            }

            var cached = await ClientService.GetFileAsync(file);
            if (cached != null)
            {
                var dataPackage = new DataPackage();
                dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromFile(cached));
                ClipboardEx.TrySetContent(dataPackage);
            }
        }

        public virtual async void Save()
        {
            var item = _selectedItem;
            if (item == null || !item.CanSave)
            {
                return;
            }

            var file = item.GetFile();
            if (file != null)
            {
                await _storageService.SaveFileAsAsync(file);
            }
        }

        public virtual async void OpenWith()
        {
            var item = _selectedItem;
            if (item == null || !CanOpenWith)
            {
                return;
            }

            var file = item.GetFile();
            if (file != null)
            {
                await _storageService.OpenFileWithAsync(file);
            }
        }

        public void OpenMessage(GalleryMedia galleryItem)
        {
            var message = galleryItem as GalleryMessage;
            if (message == null)
            {
                return;
            }

            ClientService.Send(new OpenMessageContent(message.ChatId, message.Id));
        }
    }
}
