//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;

namespace Telegram.Td.Api
{
    /// <summary>
    /// A rough managed size for the object graph behind a cache, walked by hand.
    /// </summary>
    /// <remarks>
    /// x64 layouts: 16 bytes of header, 8 per reference, fields packed and the object aligned to
    /// 8. The shallow size of each type walked here was read off GC.GetAllocatedBytesForCurrentThread
    /// one type at a time, so they are exact as of the schema they were taken against; a schema that
    /// gains a field costs a few bytes an object and changes no decision. The strings, byte arrays
    /// and files are where the answer really is, and those are measured rather than assumed.
    /// <para/>
    /// Every object is counted once, by reference, so a photo shared between a chat and its last
    /// message is not paid for twice, a shared <c>string.Empty</c> is paid for once for the whole
    /// account, and a walk cannot loop. That also means order matters: what a chat reaches is
    /// charged to the chat, and walking the files afterwards adds only the ones nothing reached.
    /// <para/>
    /// Where the walk stops - a field whose type carries nothing worth chasing, or a message
    /// content it does not recognise - the object is counted flat and not descended into.
    /// <see cref="Opaque"/> counts the ones it did not recognise, which is the honest measure of
    /// how much of the answer is guessed rather than walked.
    /// </remarks>
    public sealed partial class ObjectSize
    {
        // Header plus the smallest useful set of fields. What lands here is a leaf as far as this
        // is concerned, so the number only has to be closer than zero.
        private const int OpaqueSize = 64;

        private readonly HashSet<object> _visited = new(ReferenceComparer.Instance);

        /// <summary>
        /// Everything walked except the files, which are shared by everything and reported apart.
        /// </summary>
        public long Bytes { get; private set; }

        /// <summary>
        /// The part of <see cref="Bytes"/> that is chat last messages, the one field of a chat
        /// that is a whole message rather than a fixed size.
        /// </summary>
        public long LastMessageBytes { get; private set; }

        public long FileBytes { get; private set; }

        /// <summary>
        /// The part of <see cref="FileBytes"/> that is the persistent remote id, which the app
        /// reads in one place.
        /// </summary>
        public long RemoteIdBytes { get; private set; }

        public int FileCount { get; private set; }

        /// <summary>
        /// Objects counted as a bare object because this does not know the type.
        /// </summary>
        public int Opaque { get; private set; }

        public void Add(Chat chat)
        {
            if (Seen(chat))
            {
                return;
            }

            // 21 references, 6 longs, 7 ints, 10 bools.
            Bytes += 272;
            Bytes += Of(chat.Title) + Of(chat.ClientData);

            AddOpaque(chat.Type);
            AddOpaque(chat.Permissions);
            AddOpaque(chat.NotificationSettings);
            AddOpaque(chat.AvailableReactions);
            AddOpaque(chat.MessageSenderId);
            AddOpaque(chat.EmojiStatus);
            AddOpaque(chat.Background);
            AddOpaque(chat.Theme);
            AddOpaque(chat.ActionBar);
            AddOpaque(chat.VideoChat);
            AddOpaque(chat.BusinessBotManageBar);
            AddOpaque(chat.PendingJoinRequests);
            AddOpaque(chat.UpgradedGiftColors);
            AddOpaque(chat.BlockList);

            AddChatPhotoInfo(chat.Photo);
            AddPositions(chat.Positions);
            AddVector(chat.ChatLists, 0);
            AddDraft(chat.DraftMessage);

            var before = Bytes;
            AddMessage(chat.LastMessage);
            LastMessageBytes += Bytes - before;
        }

        public void Add(File file)
        {
            if (Seen(file))
            {
                return;
            }

            // File 56, LocalFile 56, RemoteFile 48.
            var bytes = 56L;

            if (file.Local != null && _visited.Add(file.Local))
            {
                bytes += 56 + Of(file.Local.Path);
            }

            if (file.Remote != null && _visited.Add(file.Remote))
            {
                var id = Of(file.Remote.Id);

                bytes += 48 + id + Of(file.Remote.UniqueId);
                RemoteIdBytes += id;
            }

            FileBytes += bytes;
            FileCount++;
        }

        private void AddChatPhotoInfo(ChatPhotoInfo photo)
        {
            if (Seen(photo))
            {
                return;
            }

            Bytes += 48;

            Add(photo.Small);
            Add(photo.Big);
            AddMinithumbnail(photo.Minithumbnail);
        }

        private void AddMinithumbnail(Minithumbnail thumbnail)
        {
            if (Seen(thumbnail))
            {
                return;
            }

            Bytes += 32 + Of(thumbnail.Data);
        }

        private void AddPositions(Vector<ChatPosition> positions)
        {
            if (Seen(positions))
            {
                return;
            }

            Bytes += VectorSize(positions.Count);

            foreach (var position in positions)
            {
                if (Seen(position))
                {
                    continue;
                }

                Bytes += 48;

                AddOpaque(position.List);
                AddOpaque(position.Source);
            }
        }

        private void AddDraft(DraftMessage draft)
        {
            if (Seen(draft))
            {
                return;
            }

            Bytes += 56;

            AddOpaque(draft.ReplyTo);
            AddOpaque(draft.SuggestedPostInfo);

            if (draft.Content is DraftMessageContentText text)
            {
                Bytes += OpaqueSize;
                AddFormattedText(text.Text);
            }
            else
            {
                AddUnknown(draft.Content);
            }
        }

        private void AddMessage(Message message)
        {
            if (Seen(message))
            {
                return;
            }

            // 21 references, 7 longs, 2 doubles, 3 ints, 10 bools.
            Bytes += 280;
            Bytes += Of(message.SenderTag) + Of(message.AuthorSignature) + Of(message.SummaryLanguageCode);

            AddOpaque(message.SenderId);
            AddOpaque(message.ReceiverId);
            AddOpaque(message.SendingState);
            AddOpaque(message.SchedulingState);
            AddOpaque(message.ForwardInfo);
            AddOpaque(message.ImportInfo);
            AddOpaque(message.InteractionInfo);
            AddOpaque(message.FactCheck);
            AddOpaque(message.SuggestedPostInfo);
            AddOpaque(message.ReplyTo);
            AddOpaque(message.TopicId);
            AddOpaque(message.SelfDestructType);
            AddOpaque(message.RestrictionInfo);
            AddOpaque(message.ReplyMarkup);
            AddOpaque(message.EphemeralContent);

            AddVector(message.UnreadReactions, OpaqueSize);
            AddContent(message.Content);
        }

        private void AddContent(MessageContent content)
        {
            if (Seen(content))
            {
                return;
            }

            switch (content)
            {
                case MessageText text:
                    Bytes += 40;
                    AddFormattedText(text.Text);
                    AddOpaque(text.LinkPreview);
                    AddOpaque(text.LinkPreviewOptions);
                    break;
                case MessagePhoto photo:
                    Bytes += 48;
                    AddPhoto(photo.Photo);
                    AddVideo(photo.Video);
                    AddFormattedText(photo.Caption);
                    break;
                case MessageVideo video:
                    Bytes += 64;
                    AddVideo(video.Video);
                    AddPhoto(video.Cover);
                    AddVector(video.AlternativeVideos, OpaqueSize);
                    AddVector(video.Storyboards, OpaqueSize);
                    AddFormattedText(video.Caption);
                    break;
                case MessageDocument document:
                    Bytes += 32;
                    AddDocument(document.Document);
                    AddFormattedText(document.Caption);
                    break;
                case MessageAnimation animation:
                    Bytes += 40;
                    AddAnimation(animation.Animation);
                    AddFormattedText(animation.Caption);
                    break;
                case MessageAudio audio:
                    Bytes += 32;
                    AddAudio(audio.Audio);
                    AddFormattedText(audio.Caption);
                    break;
                case MessageVoiceNote voice:
                    Bytes += 40;
                    AddVoiceNote(voice.VoiceNote);
                    AddFormattedText(voice.Caption);
                    break;
                case MessageSticker sticker:
                    Bytes += 32;
                    AddSticker(sticker.Sticker);
                    break;
                default:
                    Bytes += OpaqueSize;
                    Opaque++;
                    break;
            }
        }

        private void AddPhoto(Photo photo)
        {
            if (Seen(photo))
            {
                return;
            }

            Bytes += 40;

            AddMinithumbnail(photo.Minithumbnail);

            if (Seen(photo.Sizes))
            {
                return;
            }

            Bytes += VectorSize(photo.Sizes.Count);

            foreach (var size in photo.Sizes)
            {
                if (Seen(size))
                {
                    continue;
                }

                // The type is a one or two character string, and a fresh one per size.
                Bytes += 48 + Of(size.Type);

                Add(size.Photo);
                AddIntVector(size.ProgressiveSizes);
            }
        }

        private void AddVideo(Video video)
        {
            if (Seen(video))
            {
                return;
            }

            Bytes += 72 + Of(video.FileName) + Of(video.MimeType);

            AddMinithumbnail(video.Minithumbnail);
            AddThumbnail(video.Thumbnail);
            Add(video.VideoValue);
        }

        private void AddDocument(Document document)
        {
            if (Seen(document))
            {
                return;
            }

            Bytes += 56 + Of(document.FileName) + Of(document.MimeType);

            AddMinithumbnail(document.Minithumbnail);
            AddThumbnail(document.Thumbnail);
            Add(document.DocumentValue);
        }

        private void AddAnimation(Animation animation)
        {
            if (Seen(animation))
            {
                return;
            }

            Bytes += 72 + Of(animation.FileName) + Of(animation.MimeType);

            AddMinithumbnail(animation.Minithumbnail);
            AddThumbnail(animation.Thumbnail);
            Add(animation.AnimationValue);
        }

        private void AddAudio(Audio audio)
        {
            if (Seen(audio))
            {
                return;
            }

            Bytes += 88 + Of(audio.Title) + Of(audio.Performer) + Of(audio.FileName) + Of(audio.MimeType);

            AddMinithumbnail(audio.AlbumCoverMinithumbnail);
            AddThumbnail(audio.AlbumCoverThumbnail);
            Add(audio.AudioValue);

            if (Seen(audio.ExternalAlbumCovers))
            {
                return;
            }

            Bytes += VectorSize(audio.ExternalAlbumCovers.Count);

            foreach (var cover in audio.ExternalAlbumCovers)
            {
                AddThumbnail(cover);
            }
        }

        private void AddVoiceNote(VoiceNote voice)
        {
            if (Seen(voice))
            {
                return;
            }

            Bytes += 56 + Of(voice.Waveform) + Of(voice.MimeType);

            AddOpaque(voice.SpeechRecognitionResult);
            Add(voice.Voice);
        }

        private void AddSticker(Sticker sticker)
        {
            if (Seen(sticker))
            {
                return;
            }

            Bytes += 80 + Of(sticker.Emoji);

            AddOpaque(sticker.Format);
            AddOpaque(sticker.FullType);
            AddThumbnail(sticker.Thumbnail);
            Add(sticker.StickerValue);
        }

        private void AddThumbnail(Thumbnail thumbnail)
        {
            if (Seen(thumbnail))
            {
                return;
            }

            Bytes += 40;

            AddOpaque(thumbnail.Format);
            Add(thumbnail.File);
        }

        private void AddFormattedText(FormattedText text)
        {
            if (Seen(text))
            {
                return;
            }

            Bytes += 32 + Of(text.Text);

            if (Seen(text.Entities))
            {
                return;
            }

            Bytes += VectorSize(text.Entities.Count);

            foreach (var entity in text.Entities)
            {
                if (Seen(entity))
                {
                    continue;
                }

                Bytes += 32;
                AddOpaque(entity.Type);
            }
        }

        private void AddVector<T>(Vector<T> vector, int element) where T : class
        {
            if (Seen(vector))
            {
                return;
            }

            Bytes += VectorSize(vector.Count);

            if (element == 0)
            {
                return;
            }

            foreach (var item in vector)
            {
                if (item != null && _visited.Add(item))
                {
                    Bytes += element;
                    Opaque++;
                }
            }
        }

        private void AddIntVector(Vector<int> vector)
        {
            if (Seen(vector))
            {
                return;
            }

            // 32 for the wrapper, 24 for the array header and 4 an element.
            Bytes += 32 + Align(24 + vector.Count * 4);
        }

        private void AddOpaque(object obj)
        {
            if (obj != null && _visited.Add(obj))
            {
                Bytes += OpaqueSize;
            }
        }

        private void AddUnknown(object obj)
        {
            if (obj != null && _visited.Add(obj))
            {
                Bytes += OpaqueSize;
                Opaque++;
            }
        }

        private bool Seen(object obj)
        {
            return obj == null || !_visited.Add(obj);
        }

        /// <summary>
        /// The wrapper and its array of references.
        /// </summary>
        private static int VectorSize(int count)
        {
            return 32 + Align(24 + count * 8);
        }

        /// <summary>
        /// 22 bytes of header, length and terminator, and two per character. An empty one is the
        /// shared <c>string.Empty</c>, which an account pays for once rather than per field.
        /// </summary>
        private int Of(string value)
        {
            if (value == null || value.Length == 0 || !_visited.Add(value))
            {
                return 0;
            }

            return Align(22 + value.Length * 2);
        }

        private int Of(byte[] value)
        {
            if (value == null || !_visited.Add(value))
            {
                return 0;
            }

            return Align(24 + value.Length);
        }

        private static int Align(int size)
        {
            return (size + 7) & ~7;
        }

        private sealed partial class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new();

            bool IEqualityComparer<object>.Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            int IEqualityComparer<object>.GetHashCode(object obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
