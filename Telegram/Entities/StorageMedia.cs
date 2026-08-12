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
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

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

        /// <summary>
        /// Copies a bitmap out of a data package into the temporary folder and types the result.
        /// A pasted or dropped image arrives as pixels rather than a file, so one has to be made
        /// before any of the send path can take it.
        /// </summary>
        public static async Task<StorageMedia> CreateFromBitmapAsync(DataPackageView package)
        {
            var bitmap = await package.GetBitmapAsync();

            var fileName = string.Format("image_{0:yyyy}-{0:MM}-{0:dd}_{0:HH}-{0:mm}-{0:ss}.png", DateTime.Now);
            var cache = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);

            using (var source = await bitmap.OpenReadAsync())
            using (var destination = await cache.OpenAsync(FileAccessMode.ReadWrite))
            {
                await RandomAccessStream.CopyAsync(
                    source.GetInputStreamAt(0),
                    destination.GetOutputStreamAt(0));
            }

            var media = await CreateAsync(cache);
            if (media != null)
            {
                media.IsScreenshot = true;
            }

            return media;
        }

        /// <summary>
        /// The files a data package carries, in order. Folders are dropped — nothing downstream
        /// expands them.
        /// </summary>
        public static async Task<IReadOnlyList<StorageFile>> GetFilesAsync(DataPackageView package)
        {
            var items = await package.GetStorageItemsAsync();
            var files = new List<StorageFile>(items.Count);

            foreach (StorageFile file in items.OfType<StorageFile>())
            {
                files.Add(file);
            }

            return files;
        }

        /// <summary>
        /// Types every file, concurrently, and returns them in the order they were given. Files
        /// that could not be typed are left out.
        /// </summary>
        public static async Task<IList<StorageMedia>> CreateAsync(IEnumerable<IStorageItem> items)
        {
            var files = items.OfType<StorageFile>().ToArray();

            // Indexed rather than appended: probing finishes out of order, and callers rely on the
            // order they gave — the share target attaches its caption to the last item.
            var probed = new StorageMedia[files.Length];

            await ProbeAsync(files, (index, media) => probed[index] = media, CancellationToken.None);

            var results = new List<StorageMedia>(files.Length);

            foreach (var media in probed)
            {
                if (media != null)
                {
                    results.Add(media);
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
        /// <paramref name="resolved"/> is invoked on the calling thread's context, and is invoked
        /// for every index including the ones that could not be typed, where it gets null. A caller
        /// reassembling the original order needs to know that a slot has settled either way.
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

                    StorageMedia media = null;

                    try
                    {
                        media = await CreateAsync(files[index]);
                    }
                    catch (Exception ex)
                    {
                        // One unreadable file must not take the rest of the drop with it. Worth
                        // recording though: the per-type factories already return null for the
                        // expected "this is not a photo" case, so reaching here is a surprise.
                        Logger.Error(ex);
                    }

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        resolved(index, media);
                    }
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

    /// <summary>
    /// The content of a <c>SendFilesPopup</c>, which may not all exist yet.
    ///
    /// Callers either hand over items they have already typed, or the files behind them; the popup
    /// takes one of these either way, so it cannot be handed a half-built set or be asked to start
    /// loading as a separate step.
    /// </summary>
    public sealed partial class StorageMediaSource
    {
        private readonly IReadOnlyList<StorageFile> _files;

        private StorageMediaSource(IReadOnlyList<StorageMedia> ready, IReadOnlyList<StorageFile> files)
        {
            Ready = ready;
            _files = files;
        }

        public static StorageMediaSource FromMedia(IReadOnlyList<StorageMedia> items)
        {
            return new StorageMediaSource(items, null);
        }

        public static StorageMediaSource FromFiles(IReadOnlyList<StorageFile> files)
        {
            return new StorageMediaSource(Array.Empty<StorageMedia>(), files);
        }

        /// <summary>
        /// Everything that exists before the popup is shown, so it can be seeded rather than filled
        /// in. Empty when the files still have to be typed.
        /// </summary>
        public IReadOnlyList<StorageMedia> Ready { get; }

        /// <summary>
        /// How many items will arrive in total, known up front either way — the title can state the
        /// size of a drop before any of it has been typed.
        /// </summary>
        public int Count => _files?.Count ?? Ready.Count;

        /// <summary>
        /// True when <see cref="Ready"/> is all there is, and <see cref="LoadAsync"/> has nothing
        /// left to deliver.
        /// </summary>
        public bool IsComplete => _files == null;

        /// <summary>
        /// Delivers whatever <see cref="Ready"/> did not, reporting each item as it lands.
        /// </summary>
        public Task LoadAsync(Action<int, StorageMedia> resolved, CancellationToken cancellationToken)
        {
            if (_files == null)
            {
                return Task.CompletedTask;
            }

            return StorageMedia.ProbeAsync(_files, resolved, cancellationToken);
        }
    }
}
