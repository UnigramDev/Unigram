using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Template10.Common;
using Template10.Mvvm;
using Unigram.Common;
using Unigram.Controls.Views;
using Unigram.Converters;
using Unigram.Core.Common;
using Unigram.Helpers;
using Unigram.Services;
using Unigram.ViewModels.Chats;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Users;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;

namespace Unigram.ViewModels
{
    public abstract class GalleryViewModelBase : UnigramViewModelBase/*, IHandle<UpdateFile>*/
    {
        public IFileDelegate Delegate { get; set; }

        public GalleryViewModelBase(IProtoService protoService, IEventAggregator aggregator)
            : base(protoService, null, aggregator)
        {
            StickersCommand = new RelayCommand(StickersExecute);
            ViewCommand = new RelayCommand(ViewExecute);
            DeleteCommand = new RelayCommand(DeleteExecute);
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
                if (SelectedItem is GalleryMessageItem message && message.IsHot)
                {
                    return false;
                }

                return true;
            }
        }

        public virtual bool CanOpenWith
        {
            get
            {
                if (SelectedItem is GalleryMessageItem message && message.IsHot)
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

                var response = await ProtoService.SendAsync(new GetAttachedStickerSets(file.Id));
                if (response is StickerSets sets)
                {
                    if (sets.Sets.Count > 1)
                    {
                        await AttachedStickersView.GetForCurrentView().ShowAsync(sets.Sets);
                    }
                    else if (sets.Sets.Count > 0)
                    {
                        await StickerSetView.GetForCurrentView().ShowAsync(sets.Sets[0].Id);
                    }
                }
            }
        }

        public RelayCommand ViewCommand { get; }
        protected virtual void ViewExecute()
        {
            NavigationService.GoBack();

            var message = _selectedItem as GalleryMessageItem;
            if (message == null)
            {
                return;
            }

            var service = WindowWrapper.Current().NavigationServices.GetByFrameId("Main");
            if (service != null)
            {
                service.NavigateToChat(message.ChatId, message: message.Id);
            }
        }

        public RelayCommand DeleteCommand { get; }
        protected virtual void DeleteExecute()
        {
        }

        public RelayCommand SaveCommand { get; }
        protected virtual async void SaveExecute()
        {
            var item = _selectedItem;
            if (item == null)
            {
                return;
            }

            var result = item.GetFileAndName();

            var file = result.File;
            if (file == null || !file.Local.IsDownloadingCompleted)
            {
                return;
            }

            var fileName = result.FileName;
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = System.IO.Path.GetFileName(file.Local.Path);
            }

            var extension = System.IO.Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(extension))
            {
                extension = ".dat";
            }

            var picker = new FileSavePicker();
            picker.FileTypeChoices.Add($"{extension.TrimStart('.').ToUpper()} File", new[] { extension });
            picker.SuggestedStartLocation = PickerLocationId.Downloads;
            picker.SuggestedFileName = fileName;

            var picked = await picker.PickSaveFileAsync();
            if (picked != null)
            {
                try
                {
                    var cached = await StorageFile.GetFileFromPathAsync(file.Local.Path);
                    await cached.CopyAndReplaceAsync(picked);
                }
                catch { }
            }
        }

        public RelayCommand OpenWithCommand { get; }
        protected virtual async void OpenWithExecute()
        {
            var item = _selectedItem;
            if (item == null)
            {
                return;
            }

            var file = item.GetFile();
            if (file != null && file.Local.IsDownloadingCompleted)
            {
                try
                {
                    var temp = await StorageFile.GetFileFromPathAsync(file.Local.Path);

                    var options = new LauncherOptions();
                    options.DisplayApplicationPicker = true;

                    await Launcher.LaunchFileAsync(temp, options);

                }
                catch { }
            }
        }

        public void OpenMessage(GalleryItem galleryItem)
        {
            var message = galleryItem as GalleryMessageItem;
            if (message == null)
            {
                return;
            }

            ProtoService.Send(new OpenMessageContent(message.ChatId, message.Id));
        }
    }

    public abstract class GalleryItem : BindableBase
    {
        protected readonly IProtoService _protoService;

        public GalleryItem(IProtoService protoService)
        {
            _protoService = protoService;
        }

        public IProtoService ProtoService => _protoService;

        public abstract File GetFile();
        public abstract File GetThumbnail();

        public abstract (File File, string FileName) GetFileAndName();

        public abstract bool UpdateFile(File file);

        public virtual object Constraint { get; private set; }

        public virtual object From { get; private set; }

        public virtual string Caption { get; private set; }

        public virtual int Date { get; private set; }

        public bool IsPhoto => !IsVideo;

        public virtual bool IsVideo { get; private set; }
        public virtual bool IsLoop { get; private set; }
        public virtual bool IsShareEnabled { get; private set; }

        public virtual bool HasStickers { get; private set; }

        public virtual bool CanView { get; private set; }

        public virtual void Share()
        {
            throw new NotImplementedException();
        }
    }

    public class GalleryMessageItem : GalleryItem
    {
        protected readonly Message _message;

        public GalleryMessageItem(IProtoService protoService, Message message)
            : base(protoService)
        {
            _message = message;
        }

        public long ChatId => _message.ChatId;
        public long Id => _message.Id;

        public bool IsHot => _message.IsHot();

        public override File GetFile()
        {
            var file = _message.GetFile();
            if (file == null)
            {
                var photo = _message.GetPhoto();
                if (photo != null)
                {
                    file = photo.GetBig()?.Photo;
                }
            }

            return file;
        }

        public override File GetThumbnail()
        {
            var file = _message.GetThumbnail();
            if (file == null)
            {
                var photo = _message.GetPhoto();
                if (photo != null)
                {
                    file = photo.GetSmall()?.Photo;
                }
            }

            return file;
        }

        public override (File File, string FileName) GetFileAndName()
        {
            return _message.GetFileAndName(true);
        }

        public override bool UpdateFile(File file)
        {
            return _message.UpdateFile(file);
        }

        public override object Constraint => _message.Content;

        public override object From
        {
            get
            {
                if (_message.ForwardInfo != null)
                {
                    // TODO: ...
                }

                if (_message.IsChannelPost)
                {
                    return _protoService.GetChat(_message.ChatId);
                }

                return _protoService.GetUser(_message.SenderUserId);
            }
        }

        public override string Caption => _message.GetCaption()?.Text;
        public override int Date => _message.Date;

        public override bool IsVideo
        {
            get
            {
                if (_message.Content is MessageVideo)
                {
                    return true;
                }
                else if (_message.Content is MessageText text)
                {
                    return text.WebPage?.Video != null;
                }

                return false;
            }
        }

        public override bool HasStickers
        {
            get
            {
                if (_message.Content is MessagePhoto photo)
                {
                    return photo.Photo.HasStickers;
                }
                else if (_message.Content is MessageVideo video)
                {
                    return video.Video.HasStickers;
                }

                return false;
            }
        }



        public override bool CanView => true;
    }

    public class GalleryProfilePhotoItem : GalleryItem
    {
        private readonly ProfilePhoto _photo;
        private readonly string _caption;

        public GalleryProfilePhotoItem(IProtoService protoService, ProfilePhoto photo)
            : base(protoService)
        {
            _photo = photo;
        }

        public GalleryProfilePhotoItem(IProtoService protoService, ProfilePhoto photo, string caption)
            : base(protoService)
        {
            _photo = photo;
            _caption = caption;
        }

        public long Id => _photo.Id;

        public override File GetFile()
        {
            return _photo.Big;
        }

        public override File GetThumbnail()
        {
            return _photo.Small;
        }

        public override (File File, string FileName) GetFileAndName()
        {
            var big = _photo.Big;
            if (big != null)
            {
                return (big, null);
            }

            return (null, null);
        }

        public override bool UpdateFile(File file)
        {
            if (_photo.Big.Id == file.Id)
            {
                _photo.Big = file;
                return true;
            }

            if (_photo.Small.Id == file.Id)
            {
                _photo.Small = file;
                return true;
            }

            return false;
        }

        public override object Constraint => new PhotoSize(string.Empty, null, 600, 600);

        public override string Caption => _caption;
    }

    public class GalleryPhotoItem : GalleryItem
    {
        private readonly Photo _photo;
        private readonly string _caption;

        public GalleryPhotoItem(IProtoService protoService, Photo photo)
            : base(protoService)
        {
            _photo = photo;
        }

        public GalleryPhotoItem(IProtoService protoService, Photo photo, string caption)
            : base(protoService)
        {
            _photo = photo;
            _caption = caption;
        }

        public long Id => _photo.Id;

        public override File GetFile()
        {
            return _photo.GetBig()?.Photo;
        }

        public override File GetThumbnail()
        {
            return _photo?.GetSmall().Photo;
        }

        public override (File File, string FileName) GetFileAndName()
        {
            var big = _photo.GetBig();
            if (big != null)
            {
                return (big.Photo, null);
            }

            return (null, null);
        }

        public override bool UpdateFile(File file)
        {
            return _photo.UpdateFile(file);
        }

        public override object Constraint => _photo;

        public override string Caption => _caption;

        public override bool HasStickers => _photo.HasStickers;
    }

    public class GalleryVideoItem : GalleryItem
    {
        private readonly Video _video;
        private readonly string _caption;

        public GalleryVideoItem(IProtoService protoService, Video video)
            : base(protoService)
        {
            _video = video;
        }

        public GalleryVideoItem(IProtoService protoService, Video video, string caption)
            : base(protoService)
        {
            _video = video;
            _caption = caption;
        }

        //public long Id => _video.Id;

        public override File GetFile()
        {
            return _video.VideoValue;
        }

        public override File GetThumbnail()
        {
            return _video.Thumbnail?.Photo;
        }

        public override (File File, string FileName) GetFileAndName()
        {
            return (_video.VideoValue, _video.FileName);
        }

        public override bool UpdateFile(File file)
        {
            return _video.UpdateFile(file);
        }

        public override object Constraint => _video;

        public override string Caption => _caption;

        public override bool HasStickers => _video.HasStickers;
    }

    public class GalleryAnimationItem : GalleryItem
    {
        private readonly Animation _animation;
        private readonly string _caption;

        public GalleryAnimationItem(IProtoService protoService, Animation animation)
            : base(protoService)
        {
            _animation = animation;
        }

        public GalleryAnimationItem(IProtoService protoService, Animation animation, string caption)
            : base(protoService)
        {
            _animation = animation;
            _caption = caption;
        }

        //public long Id => _animation.Id;

        public override File GetFile()
        {
            return _animation.AnimationValue;
        }

        public override File GetThumbnail()
        {
            return _animation.Thumbnail?.Photo;
        }

        public override (File File, string FileName) GetFileAndName()
        {
            return (_animation.AnimationValue, _animation.FileName);
        }

        public override bool UpdateFile(File file)
        {
            return _animation.UpdateFile(file);
        }

        public override object Constraint => _animation;

        public override string Caption => _caption;
    }

    public interface IGalleryDelegate
    {
        void OpenFile(GalleryItem item, File file);
    }
}
