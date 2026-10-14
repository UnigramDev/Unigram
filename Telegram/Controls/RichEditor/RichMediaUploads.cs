//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.Controls.RichEditor
{
    /// <summary>
    /// Media added to an article uploads from the moment it is added (TDLib's
    /// preliminaryUploadFile), so sending does not wait for it and the block shows how far it
    /// got. What is sent then names the file by its id (<see cref="GetInput"/>), and TDLib carries
    /// on with the upload it already has. A source taken out of the article stops its upload, and
    /// so does closing the window without sending (<see cref="Cancel"/>).
    /// </summary>
    public sealed partial class RichMediaUploads : IRichMediaInputs
    {
        // The subscriber for file updates: a control, so they arrive on its thread.
        private readonly RichEditorView _owner;
        private readonly IClientService _clientService;

        private readonly Dictionary<RichMediaSource, Upload> _uploads = new();

        // Kept for Retain, which runs on every structural change while anything uploads.
        private readonly HashSet<RichMediaSource> _live = new();
        private readonly List<RichMediaSource> _gone = new();

        public RichMediaUploads(RichEditorView owner, IClientService clientService)
        {
            _owner = owner;
            _clientService = clientService;
        }

        /// <summary>
        /// Starts uploading <paramref name="source"/> if it is a file from this device that is not
        /// uploading yet. A source read from a message, or one already on the server (a sticker
        /// added as a file), has nothing to upload.
        /// </summary>
        public void Track(RichMediaSource source)
        {
            var file = source?.Input != null ? GetFile(source.Input) : null;
            if (file is not (InputFileLocal or InputFileGenerated) || _uploads.ContainsKey(source))
            {
                return;
            }

            var upload = new Upload(this, source);
            _uploads[source] = upload;

            Start(upload, file);
        }

        private async void Start(Upload upload, InputFile file)
        {
            var response = await _clientService.SendAsync(new PreliminaryUploadFile(file, ToFileType(upload.Source.Kind), 1));

            var current = _uploads.TryGetValue(upload.Source, out Upload value) && value == upload;

            if (response is not File result)
            {
                // Uploaded with the message after all, as it always was.
                if (current)
                {
                    _uploads.Remove(upload.Source);
                }

                return;
            }

            // Taken out of the article, or the window gone, while TDLib was answering.
            if (!current)
            {
                _clientService.Send(new CancelPreliminaryUploadFile(result.Id));
                return;
            }

            upload.File = result;
            UpdateManager.Subscribe(_owner, _clientService, result, ref upload.Token, upload.OnFileUpdated);

            _owner.OnUploadChanged(upload.Source);
        }

        /// <summary>How far <paramref name="source"/> has uploaded, while it is uploading.</summary>
        public bool TryGetProgress(RichMediaSource source, out long uploaded, out long size)
        {
            uploaded = 0;
            size = 0;

            if (source == null || !_uploads.TryGetValue(source, out Upload upload))
            {
                return false;
            }

            // Asked for, not answered yet: about to start.
            var file = upload.File;
            if (file == null)
            {
                return true;
            }

            if (file.Remote.IsUploadingCompleted || !file.Remote.IsUploadingActive)
            {
                return false;
            }

            uploaded = file.Remote.UploadedSize;
            size = Math.Max(file.Size, file.ExpectedSize);
            return true;
        }

        /// <summary>Stops the uploads of what is no longer in <paramref name="document"/>.</summary>
        public void Retain(RichDocument document)
        {
            if (_uploads.Count == 0)
            {
                return;
            }

            foreach (var block in document.AllBlocks)
            {
                if (block is RichMediaBlock { Source: RichMediaSource source })
                {
                    _live.Add(source);
                }
            }

            foreach (var source in _uploads.Keys)
            {
                if (!_live.Contains(source))
                {
                    _gone.Add(source);
                }
            }

            foreach (var source in _gone)
            {
                Stop(source, true);
            }

            _live.Clear();
            _gone.Clear();
        }

        /// <summary>The window closed without sending: nothing will use what was uploaded.</summary>
        public void Cancel()
        {
            Clear(true);
        }

        /// <summary>The article was sent: TDLib finishes the uploads with the message.</summary>
        public void Detach()
        {
            Clear(false);
        }

        private void Clear(bool cancel)
        {
            foreach (var source in _uploads.Keys)
            {
                _gone.Add(source);
            }

            foreach (var source in _gone)
            {
                Stop(source, cancel);
            }

            _gone.Clear();
        }

        private void Stop(RichMediaSource source, bool cancel)
        {
            if (!_uploads.TryGetValue(source, out Upload upload))
            {
                return;
            }

            _uploads.Remove(source);
            UpdateManager.Unsubscribe(_owner, ref upload.Token);

            if (cancel && upload.File is File file && !file.Remote.IsUploadingCompleted)
            {
                _clientService.Send(new CancelPreliminaryUploadFile(file.Id));
            }
        }

        /// <summary>
        /// What <paramref name="source"/> is sent as: by the id of the file it is uploading to,
        /// or null, for its input as it was added.
        /// </summary>
        public Object GetInput(RichMediaSource source)
        {
            if (source == null || !_uploads.TryGetValue(source, out Upload upload) || upload.File == null)
            {
                return null;
            }

            var file = new InputFileId(upload.File.Id);

            // A copy: the source is shared with every undo step, and a draft saved later still
            // has to name the file on the device.
            return source.Input switch
            {
                InputPhoto x => new InputPhoto(file, x.Thumbnail, x.Video, x.AddedStickerFileIds, x.Width, x.Height),
                InputVideo x => new InputVideo(file, x.Thumbnail, x.Cover, x.StartTimestamp, x.AddedStickerFileIds, x.Duration, x.Width, x.Height, x.SupportsStreaming),
                InputAnimation x => new InputAnimation(file, x.Thumbnail, x.AddedStickerFileIds, x.Duration, x.Width, x.Height),
                InputAudio x => new InputAudio(file, x.AlbumCoverThumbnail, x.Duration, x.Title, x.Performer),
                InputVoiceNote x => new InputVoiceNote(file, x.Duration, x.Waveform),
                InputDocument x => new InputDocument(file, x.Thumbnail, x.DisableContentTypeDetection),
                _ => null
            };
        }

        private static InputFile GetFile(Object input)
        {
            return input switch
            {
                InputPhoto x => x.Photo,
                InputVideo x => x.Video,
                InputAnimation x => x.Animation,
                InputAudio x => x.Audio,
                InputVoiceNote x => x.VoiceNote,
                InputDocument x => x.Document,
                _ => null
            };
        }

        private static FileType ToFileType(RichMediaKind kind)
        {
            return kind switch
            {
                RichMediaKind.Photo => new FileTypePhoto(),
                RichMediaKind.Video => new FileTypeVideo(),
                RichMediaKind.Animation => new FileTypeAnimation(),
                RichMediaKind.Audio => new FileTypeAudio(),
                RichMediaKind.VoiceNote => new FileTypeVoiceNote(),
                _ => new FileTypeDocument()
            };
        }

        private sealed class Upload
        {
            private readonly RichMediaUploads _uploads;

            public Upload(RichMediaUploads uploads, RichMediaSource source)
            {
                _uploads = uploads;
                Source = source;
            }

            public RichMediaSource Source { get; }

            public File File;

            public long Token;

            // On the editor's thread: the editor is the subscriber.
            public void OnFileUpdated(File file)
            {
                File = file;
                _uploads._owner.OnUploadChanged(Source);
            }
        }
    }
}
