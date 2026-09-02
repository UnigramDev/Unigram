//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.Views.Popups;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using Windows.UI.Xaml;

namespace Telegram.ViewModels.Gallery
{
    public abstract class GalleryViewModelBase : ViewModelBase, IDelegable<IGalleryDelegate>
    {
        // How close to either end of Items the selection has to get before the next page is
        // requested. The window can't move past the last item, so loading only once it is
        // reached leaves the user waiting at every page boundary.
        private const int LoadMoreThreshold = 2;

        private readonly IStorageService _storageService;

        protected bool _hasProtectedContent;

        // How many items exist before Items[0] on the server: what turns the index of an item
        // into its absolute position. A source sets it once it knows where its first item sits;
        // LoadMore keeps it in step with whatever gets prepended.
        protected int _offset;

        private bool _hasPrevious = true;
        private bool _hasNext = true;

        public IGalleryDelegate Delegate { get; set; }

        public GalleryViewModelBase(IClientService clientService, IStorageService storageService, IEventAggregator aggregator)
            : base(clientService, clientService.Session.Resolve<ISettingsService>(), aggregator)
        {
            _storageService = storageService;
        }

        public bool HasProtectedContent => _hasProtectedContent;

        public int Position => _offset + SelectedIndex + 1;

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

        public RangeObservableCollection<GalleryMedia> Items { get; protected set; }

        public virtual RangeObservableCollection<GalleryMedia> Group { get; }

        #region Paging

        /// <summary>
        /// True while the initial fill or a page load is running. Only one runs at a time: a
        /// request that arrives meanwhile is dropped rather than queued, because the next
        /// navigation asks again and a queued one would repeat a range already being loaded.
        /// </summary>
        protected bool IsLoading { get; set; }

        public async void LoadMore()
        {
            if (IsLoading || Items == null || _selectedItem == null)
            {
                return;
            }

            var index = Items.IndexOf(_selectedItem);
            if (index < 0)
            {
                return;
            }

            var previous = _hasPrevious && index <= LoadMoreThreshold;
            var next = _hasNext && index >= Items.Count - 1 - LoadMoreThreshold;

            if (!previous && !next)
            {
                return;
            }

            IsLoading = true;

            try
            {
                if (previous)
                {
                    var result = await LoadPreviousAsync();

                    // Whatever was prepended used to be part of the offset.
                    _offset -= (int)result.Count;
                    _hasPrevious = result.HasMoreItems;
                }

                if (next)
                {
                    _hasNext = (await LoadNextAsync()).HasMoreItems;
                }
            }
            finally
            {
                IsLoading = false;
            }

            RaisePropertyChanged(nameof(Position));
        }

        /// <summary>
        /// Prepends the page before <see cref="Items"/>[0] and reports how many were added, so
        /// that <see cref="_offset"/> can follow. <c>HasMoreItems</c> false stops this direction
        /// from being asked again.
        /// </summary>
        protected virtual Task<IncrementalLoadResult> LoadPreviousAsync()
        {
            return Task.FromResult(new IncrementalLoadResult(0, false));
        }

        /// <summary>
        /// Appends the page after the last item. Only <c>HasMoreItems</c> is read: appending
        /// leaves the offset alone.
        /// </summary>
        protected virtual Task<IncrementalLoadResult> LoadNextAsync()
        {
            return Task.FromResult(new IncrementalLoadResult(0, false));
        }

        #endregion

        protected virtual void OnSelectedItemChanged(GalleryMedia item)
        {
            RaisePropertyChanged(nameof(Position));

            if (item == null || Window == null)
            {
                return;
            }

            if (item.HasProtectedContent && !_hasProtectedContent)
            {
                _hasProtectedContent = true;
                Window.DisableScreenCapture(GetHashCode());
            }
            else if (_hasProtectedContent && !item.HasProtectedContent)
            {
                _hasProtectedContent = false;
                Window.EnableScreenCapture(GetHashCode());
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
                if (SelectedItem is GalleryMessage message && message.HasProtectedContent)
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
                    var file = _selectedItem.File;
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
            if (message == null || !message.CanBeViewed)
            {
                return;
            }

            NavigationService.NavigateToChat(message.ChatId, message: message.Id);
        }

        public virtual async void Forward()
        {
            if (_selectedItem is GalleryMessage message)
            {
                var response = await ClientService.SendAsync(new GetMessageProperties(message.ChatId, message.Id));
                if (response is MessageProperties properties && properties.CanBeForwarded)
                {
                    ShowPopup(new ChooseChatsPopup(), new ChooseChatsConfigurationShareMessages(new MessageToShare(message.Message, properties)), ElementTheme.Dark);
                }
            }
            else
            {
                var input = _selectedItem?.ToInput();
                if (input != null)
                {
                    ShowPopup(new ChooseChatsPopup(), new ChooseChatsConfigurationPostMessage(input), ElementTheme.Dark);
                }
            }
        }

        public virtual void Delete()
        {
        }

        public async void Copy()
        {
            var item = _selectedItem;
            if (item == null || !item.CanBeCopied)
            {
                return;
            }

            var file = item.File;
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

                ToastPopup.Show(XamlRoot, Strings.ImageCopied, ToastPopupIcon.Copied);
            }
        }

        public virtual async void Save()
        {
            var item = _selectedItem;
            if (item == null || !item.CanBeSaved)
            {
                return;
            }

            var file = item.File;
            if (file != null)
            {
                await _storageService.SaveFileAsAsync(XamlRoot, file);
            }
        }

        public virtual async void OpenWith()
        {
            var item = _selectedItem;
            if (item == null || !CanOpenWith)
            {
                return;
            }

            var file = item.File;
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

        public virtual void PlaybackStarted(GalleryMedia item)
        {

        }

        public virtual void PlaybackStopped()
        {

        }

        public virtual void AdvertisementDisplayed()
        {

        }

        public virtual void HideAdvertisement()
        {

        }
    }
}
