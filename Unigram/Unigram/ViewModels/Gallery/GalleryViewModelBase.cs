using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Navigation;
using Unigram.Services;
using Unigram.Views.Popups;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;

namespace Unigram.ViewModels.Gallery
{
    public abstract class GalleryViewModelBase : TLViewModelBase
    {
        private readonly IStorageService _storageService;

        private bool _hasProtectedContent;

        public GalleryViewModelBase(IClientService clientService, IStorageService storageService, IEventAggregator aggregator)
            : base(clientService, null, aggregator)
        {
            _storageService = storageService;

            StickersCommand = new RelayCommand(StickersExecute);
            ViewCommand = new RelayCommand(ViewExecute);
            ForwardCommand = new RelayCommand(ForwardExecute);
            DeleteCommand = new RelayCommand(DeleteExecute);
            CopyCommand = new RelayCommand(CopyExecute);
            SaveCommand = new RelayCommand(SaveExecute);
            OpenWithCommand = new RelayCommand(OpenWithExecute);

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

        protected GalleryContent _selectedItem;
        public GalleryContent SelectedItem
        {
            get => _selectedItem;
            set
            {
                Set(ref _selectedItem, value);
                OnSelectedItemChanged(value);
                //RaisePropertyChanged(() => SelectedIndex);
            }
        }

        protected GalleryContent _firstItem;
        public GalleryContent FirstItem
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

        public MvxObservableCollection<GalleryContent> Items { get; protected set; }

        public virtual MvxObservableCollection<GalleryContent> Group { get; }

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

        protected virtual void OnSelectedItemChanged(GalleryContent item)
        {
            RaisePropertyChanged(nameof(Position));

            if (item == null)
            {
                return;
            }

            if (item.IsProtected && !_hasProtectedContent)
            {
                _hasProtectedContent = true;
                WindowContext.Current.DisableScreenCapture(GetHashCode());
            }
            else if (_hasProtectedContent && !item.IsProtected)
            {
                _hasProtectedContent = false;
                WindowContext.Current.EnableScreenCapture(GetHashCode());
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

        public RelayCommand StickersCommand { get; }
        private async void StickersExecute()
        {
            if (_selectedItem != null && _selectedItem.HasStickers)
            {
                var file = _selectedItem.GetFile();
                if (file == null)
                {
                    return;
                }

                await StickersPopup.ShowAsync(new InputFileId(file.Id));
            }
        }

        public RelayCommand ViewCommand { get; }
        protected virtual void ViewExecute()
        {
            FirstItem = null;
            NavigationService.GoBack();

            var message = _selectedItem as GalleryMessage;
            if (message == null || !message.CanView)
            {
                return;
            }

            var service = WindowContext.Current.NavigationServices.GetByFrameId("Main" + ClientService.SessionId);
            if (service != null)
            {
                service.NavigateToChat(message.ChatId, message: message.Id);
            }
        }

        public RelayCommand ForwardCommand { get; }
        protected virtual async void ForwardExecute()
        {
            if (_selectedItem is GalleryMessage message)
            {
                await SharePopup.GetForCurrentView().ShowAsync(message.Message);
            }
            else
            {
                var input = _selectedItem?.ToInput();
                if (input == null)
                {
                    return;
                }

                await SharePopup.GetForCurrentView().ShowAsync(input);
            }
        }

        public RelayCommand DeleteCommand { get; }
        protected virtual void DeleteExecute()
        {
        }

        public RelayCommand CopyCommand { get; }
        protected async void CopyExecute()
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

            var temp = await ClientService.GetFileAsync(file);
            if (temp != null)
            {
                var dataPackage = new DataPackage();
                dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromFile(temp));
                ClipboardEx.TrySetContent(dataPackage);
            }
        }

        public RelayCommand SaveCommand { get; }
        protected virtual async void SaveExecute()
        {
            var item = _selectedItem;
            if (item == null || !item.CanSave)
            {
                return;
            }

            var file = item.GetFile();
            if (file != null)
            {
                await _storageService.SaveAsAsync(file);
            }
        }

        public RelayCommand OpenWithCommand { get; }
        protected virtual async void OpenWithExecute()
        {
            var item = _selectedItem;
            if (item == null || !CanOpenWith)
            {
                return;
            }

            var file = item.GetFile();
            if (file != null)
            {
                await _storageService.OpenWithAsync(file);
            }
        }

        public void OpenMessage(GalleryContent galleryItem)
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
