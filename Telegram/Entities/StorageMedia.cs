//
// Copyright (c) Fela Ameghino 2015-2026
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
    public partial class StorageInvalid : StorageMedia
    {
        public StorageInvalid()
            : base(null, 0)
        {
        }
    }

    public abstract class StorageMedia : BindableBase
    {
        public StorageMedia(StorageFile file, ulong fileSize)
        {
            File = file;
            Size = fileSize;

            EditState = new ImageGeneration();
        }

        public StorageFile File { get; private set; }

        public ulong Size { get; }

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

        public virtual int Width { get; }
        public virtual int Height { get; }

        public double ActualWidth
        {
            get
            {
                if (_editState is ImageGeneration editState && !editState.IsEmpty)
                {
                    if (editState.Rotation is ImageRotation.Clockwise90Degrees or ImageRotation.Clockwise270Degrees)
                    {
                        return editState.Rectangle.Width * Height;
                    }

                    return editState.Rectangle.Width * Width;
                }

                return Width;
            }
        }

        public double ActualHeight
        {
            get
            {
                if (_editState is ImageGeneration editState && !editState.IsEmpty)
                {
                    if (editState.Rotation is ImageRotation.Clockwise90Degrees or ImageRotation.Clockwise270Degrees)
                    {
                        return editState.Rectangle.Height * Width;
                    }

                    return editState.Rectangle.Height * Height;
                }

                return Height;
            }
        }

        protected ImageGeneration _editState;
        public ImageGeneration EditState
        {
            get => _editState;
            set
            {
                Set(ref _editState, value);
                RaisePropertyChanged(nameof(IsEdited));
            }
        }

        public bool IsEdited => !_editState?.IsEmpty ?? false;

        public virtual async void Refresh()
        {
            if (_editState is ImageGeneration editState && !editState.IsEmpty)
            {
                try
                {
                    // TODO: actual logical pixel size
                    _preview = await ImageHelper.CropAndPreviewAsync(this, editState, 600);
                }
                catch
                {
                    await RefreshAsync();
                }
            }
            else
            {
                await RefreshAsync();
            }

            RaisePropertyChanged(nameof(Preview));
        }

        private async Task RefreshAsync()
        {
            try
            {
                if (this is StorageVideo)
                {
                    // TODO: actual logical pixel size
                    _preview = await ImageHelper.GetPreviewBitmapAsync(this, 600);
                }
                else
                {
                    var preview = new BitmapImage
                    {
                        DecodePixelWidth = 300,
                        DecodePixelType = DecodePixelType.Logical
                    };

                    using var stream = await File.OpenReadAsync();
                    await preview.SetSourceAsync(stream);

                    _preview = preview;
                }
            }
            catch
            {
                _preview = new BitmapImage();
            }
        }

        public static async Task<StorageMedia> CreateAsync(StorageFile file, bool probe = true)
        {
            if (file == null || !file.IsAvailable)
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

            if (probe is false)
            {
                return new StorageDocument(file, basicProperties.Size);
            }

            if (file.HasExtension(".jpeg", ".jpg", ".png", ".bmp", ".gif", ".heic", ".heif"))
            {
                var photo = await StoragePhoto.CreateAsync(file, basicProperties.Size);
                if (photo != null)
                {
                    return photo;
                }
            }
            else if (file.HasExtension(".mp4", ".mov"))
            {
                var video = await StorageVideo.CreateAsync(file, basicProperties.Size);
                if (video != null)
                {
                    return video;
                }
            }
            else if (file.HasExtension(".mp3", ".wav", ".m4a", ".ogg", ".oga", ".opus", ".flac"))
            {
                var audio = await StorageAudio.CreateAsync(file, basicProperties.Size);
                if (audio != null)
                {
                    return audio;
                }
            }

            return new StorageDocument(file, basicProperties.Size);
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
