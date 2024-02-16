//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Td.Api;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Entities
{
    public abstract class StorageMedia : BindableBase
    {
        public StorageMedia(StorageFile file, BasicProperties basic)
        {
            File = file;
            //DateModified = basic.DateModified;
            //ItemDate = basic.ItemDate;
            Size = basic.Size;

            EditState = new BitmapEditState();
        }

        public StorageFile File { get; private set; }

        //public DateTimeOffset DateModified { get; }
        //public DateTimeOffset ItemDate { get; }
        public ulong Size { get; }

        protected BitmapImage _thumbnail;
        public BitmapImage Thumbnail
        {
            get
            {
                if (_thumbnail == null)
                {
                    LoadThumbnail();
                }

                return _thumbnail;
            }
        }

        protected ImageSource _bitmap;
        public ImageSource Bitmap
        {
            get
            {
                if (_bitmap == null)
                {
                    Refresh();
                }

                return _bitmap;
            }
        }

        protected ImageSource _preview;
        public ImageSource Preview
        {
            get
            {
                if (_preview == null)
                {
                    Refresh();
                }

                return _preview;
            }
        }

        protected bool _hasSpoiler;
        public bool HasSpoiler
        {
            get => _hasSpoiler;
            set => Set(ref _hasSpoiler, value);
        }

        protected MessageSelfDestructType _ttl;
        public MessageSelfDestructType Ttl
        {
            get => _ttl;
            set
            {
                Set(ref _ttl, value);
                RaisePropertyChanged(nameof(IsSecret));
            }
        }

        public bool IsSecret => _ttl != null;

        public bool IsScreenshot { get; set; }

        public virtual uint Width { get; }
        public virtual uint Height { get; }

        protected BitmapEditState _editState;
        public BitmapEditState EditState
        {
            get => _editState;
            set
            {
                Set(ref _editState, value);
                RaisePropertyChanged(nameof(IsEdited));
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

                    RaisePropertyChanged(nameof(Thumbnail));
                }
            }
            catch { }
        }

        public virtual async void Refresh()
        {
            if (_bitmap == null)
            {
                try
                {
                    _bitmap = await ImageHelper.GetPreviewBitmapAsync(File);
                }
                catch { }
            }

            _bitmap ??= new BitmapImage();

            if (_editState is BitmapEditState editState && !editState.IsEmpty)
            {
                try
                {
                    _preview = await ImageHelper.CropAndPreviewAsync(File, editState);
                }
                catch
                {
                    _preview = _bitmap;
                }
            }
            else
            {
                _preview = _bitmap;
            }

            RaisePropertyChanged(nameof(Preview));
        }

        public static async Task<StorageMedia> CreateAsync(StorageFile file)
        {
            if (file == null)
            {
                return null;
            }

            BasicProperties basicProperties;
            try
            {
                basicProperties = await file.GetBasicPropertiesAsync();
            }
            catch
            {
                return null;
            }

            if (file.FileType.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                file.FileType.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                file.FileType.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                file.FileType.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
                file.FileType.Equals(".gif", StringComparison.OrdinalIgnoreCase))
            {
                var photo = await StoragePhoto.CreateAsync(file, basicProperties);
                if (photo != null)
                {
                    return photo;
                }
            }
            else if (file.FileType.Equals(".mp4", StringComparison.OrdinalIgnoreCase))
            {
                var video = await StorageVideo.CreateAsync(file, basicProperties);
                if (video != null)
                {
                    return video;
                }
            }
            else if (file.ContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
            {
                var audio = await StorageAudio.CreateAsync(file, basicProperties);
                if (audio != null)
                {
                    return audio;
                }
            }

            return new StorageDocument(file, basicProperties);
        }

        public static async Task<IList<StorageMedia>> CreateAsync(IEnumerable<IStorageItem> items)
        {
            var results = new List<StorageMedia>();

            try
            {
                foreach (StorageFile file in items.OfType<StorageFile>())
                {
                    var media = await CreateAsync(file);
                    if (media != null)
                    {
                        results.Add(media);
                    }
                }
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }

            return results;
        }
    }
}
