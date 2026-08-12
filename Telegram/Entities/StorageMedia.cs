//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Td.Api;
using Windows.Storage;
using Windows.Storage.FileProperties;

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

            foreach (StorageFile file in items.OfType<StorageFile>())
            {
                try
                {
                    var media = await CreateAsync(file);
                    if (media != null)
                    {
                        results.Add(media);
                    }
                }
                catch
                {
                    // All the remote procedure calls must be wrapped in a try-catch block, and
                    // per-file: one unreadable item used to discard every file after it.
                }
            }

            return results;
        }

        /// <summary>
        /// Probes files concurrently, reporting each one through <paramref name="resolved"/> as it
        /// lands so the caller can show a popup before anything has been typed. Results arrive out
        /// of order — <c>index</c> is the file's position in <paramref name="files"/>.
        /// </summary>
        /// <remarks>
        /// <paramref name="resolved"/> is invoked on the calling thread's context.
        /// </remarks>
        public static async Task ProbeAsync(IReadOnlyList<StorageFile> files, Action<int, StorageMedia> resolved, CancellationToken cancellationToken)
        {
            // A probe is a file open plus a header decode, or a whole ffmpeg open for video and
            // audio. Running them one after another is what used to stall a large drop; the cap
            // keeps that same drop from opening hundreds of decoders at once.
            using var throttle = new SemaphoreSlim(Math.Clamp(Environment.ProcessorCount, 2, 8));

            async Task ProbeOneAsync(int index)
            {
                try
                {
                    await throttle.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                try
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    var media = await CreateAsync(files[index]);
                    if (media != null && !cancellationToken.IsCancellationRequested)
                    {
                        resolved(index, media);
                    }
                }
                catch
                {
                    // One unreadable file must not take the rest of the drop with it.
                }
                finally
                {
                    throttle.Release();
                }
            }

            var probes = new Task[files.Count];

            for (int i = 0; i < files.Count; i++)
            {
                probes[i] = ProbeOneAsync(i);
            }

            await Task.WhenAll(probes);
        }
    }
}
