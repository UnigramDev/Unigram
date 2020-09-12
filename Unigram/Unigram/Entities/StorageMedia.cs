using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Navigation;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Entities
{
    public abstract class StorageMedia : BindableBase
    {
        public StorageMedia(StorageFile file, BasicProperties basic)
        {
            File = file;
            Basic = basic;

            EditState = new BitmapEditState();
        }

        public StorageFile File { get; private set; }

        public BasicProperties Basic { get; private set; }

        protected BitmapImage _thumbnail;
        public BitmapImage Thumbnail
        {
            get
            {
                if (_thumbnail == null)
                    LoadThumbnail();

                return _thumbnail;
            }
        }

        protected ImageSource _bitmap;
        public ImageSource Bitmap
        {
            get
            {
                if (_bitmap == null)
                    Refresh();

                return _bitmap;
            }
        }

        protected ImageSource _preview;
        public ImageSource Preview
        {
            get
            {
                if (_preview == null)
                    Refresh();

                return _preview;
            }
        }

        protected int _ttl;
        public int Ttl
        {
            get
            {
                return _ttl;
            }
            set
            {
                Set(ref _ttl, value);
                RaisePropertyChanged(() => IsSecret);
            }
        }

        public bool IsSecret => _ttl > 0;

        public virtual uint Width { get; }
        public virtual uint Height { get; }

        protected BitmapEditState _editState;
        public BitmapEditState EditState
        {
            get => _editState;
            set
            {
                Set(ref _editState, value);
                RaisePropertyChanged(() => IsEdited);
            }
        }

        public bool IsEdited => !_editState?.IsEmpty ?? false;

        private async void LoadThumbnail()
        {
            try
            {
                if (!File.Attributes.HasFlag(FileAttributes.Temporary))
                {
                    using (var thumbnail = await File.GetThumbnailAsync(ThumbnailMode.ListView, 96, ThumbnailOptions.UseCurrentScale))
                    {
                        if (thumbnail != null)
                        {
                            var bitmapImage = new BitmapImage();
                            await bitmapImage.SetSourceAsync(thumbnail);

                            _thumbnail = bitmapImage;
                        }
                    }

                    RaisePropertyChanged(() => Thumbnail);
                }
            }
            catch { }
        }

        public virtual async void Refresh()
        {
            if (_bitmap == null)
            {
                _bitmap = await ImageHelper.GetPreviewBitmapAsync(File);
            }

            if (_bitmap == null)
            {
                _bitmap = new BitmapImage();
            }

            if (_editState is BitmapEditState editState && !editState.IsEmpty)
            {
                _preview = await ImageHelper.CropAndPreviewAsync(File, editState);
            }
            else
            {
                _preview = _bitmap;
            }

            RaisePropertyChanged(() => Preview);
        }

        public static async Task<StorageMedia> CreateAsync(StorageFile file)
        {
            if (file == null)
            {
                return null;
            }
            else if (file.ContentType.Equals("video/mp4"))
            {
                return await StorageVideo.CreateAsync(file);
            }
            else
            {
                return await StoragePhoto.CreateAsync(file);
            }
        }

        public static async Task<IList<StorageMedia>> CreateAsync(IEnumerable<IStorageItem> items)
        {
            var results = new List<StorageMedia>();

            foreach (StorageFile file in items.OfType<StorageFile>())
            {
                if (file.ContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) ||
                    file.ContentType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ||
                    file.ContentType.Equals("image/bmp", StringComparison.OrdinalIgnoreCase) ||
                    file.ContentType.Equals("image/gif", StringComparison.OrdinalIgnoreCase))
                {
                    var photo = await StoragePhoto.CreateAsync(file);
                    if (photo != null)
                    {
                        results.Add(photo);
                    }
                    else
                    {
                        results.Add(new StorageDocument(file));
                    }
                }
                else if (file.ContentType == "video/mp4")
                {
                    var video = await StorageVideo.CreateAsync(file);
                    if (video != null)
                    {
                        results.Add(video);
                    }
                    else
                    {
                        results.Add(new StorageDocument(file));
                    }
                }
                else
                {
                    results.Add(new StorageDocument(file));
                }
            }

            return results;
        }
    }
}
