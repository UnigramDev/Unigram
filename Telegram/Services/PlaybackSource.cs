//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;

namespace Telegram.Services
{
    /// <summary>
    /// Pages the items of one playback session in, in either direction.
    /// </summary>
    /// <remarks>
    /// A source owns its cursors, so whoever holds it is the only one who may page it: a
    /// caller handing its own source to <see cref="IPlaybackService"/> is giving it away,
    /// not sharing it. It is deliberately not a collection - the playlist is mirrored into
    /// per-window collections through PlaylistChanged, and an observable shared between
    /// windows would raise CollectionChanged on the wrong thread.
    /// </remarks>
    public abstract class PlaybackSource
    {
        protected PlaybackSource(IClientService clientService)
        {
            ClientService = clientService;
        }

        public IClientService ClientService { get; }

        /// <summary>
        /// Whether more items may exist past the given end of the playlist. False here is
        /// final; true only means the end has not been reached yet.
        /// </summary>
        /// <param name="forward">Towards the end of the playlist rather than its start.</param>
        public abstract bool HasMore(bool forward);

        /// <summary>
        /// Loads the next page, or an empty list once there is none. Items come back in
        /// playlist order, ready to be inserted at the matching end.
        /// </summary>
        public abstract Task<IList<PlaybackItem>> LoadMoreAsync(bool forward);
    }

    /// <summary>
    /// The audio, or the voice and video notes, of one chat.
    /// </summary>
    public partial class ChatPlaybackSource : PlaybackSource
    {
        private readonly XamlRoot _xamlRoot;

        private readonly long _chatId;
        private readonly MessageTopic _topic;
        private readonly SearchMessagesFilter _filter;

        // Audio plays newest first, so the end of the playlist is older messages; voice and
        // video notes play in the order they were sent, so it is the newer ones. Everything
        // below talks in playlist directions and converts here, once.
        private readonly bool _newestFirst;

        // Message ids the playlist currently reaches, and what searching past them found.
        private long _startId;
        private long _endId;

        private bool _hasMoreStart = true;
        private bool _hasMoreEnd = true;

        public ChatPlaybackSource(IClientService clientService, XamlRoot xamlRoot, long chatId, MessageTopic topic, bool audio)
            : base(clientService)
        {
            _xamlRoot = xamlRoot;

            _chatId = chatId;
            _topic = topic;
            _filter = audio
                ? new SearchMessagesFilterAudio()
                : new SearchMessagesFilterVoiceAndVideoNote();

            _newestFirst = audio;
        }

        public long ChatId => _chatId;

        public MessageTopic Topic => _topic;

        /// <summary>
        /// Whether the newest message is the start of the playlist rather than its end.
        /// </summary>
        public bool NewestFirst => _newestFirst;

        /// <summary>
        /// Whether a message newer than everything else can be added straight away. Until
        /// the newest end has actually been reached there are messages between it and the
        /// playlist, and adding past them would move the cursor over the gap.
        /// </summary>
        public bool CanAddNewest => !HasMore(!_newestFirst);

        /// <summary>
        /// Whether a message belongs in this playlist at all.
        /// </summary>
        public bool Accepts(Message message)
        {
            if (message.ChatId != _chatId)
            {
                return false;
            }

            // No topic means the whole chat. Comparing anyway would reject everything, since
            // in a forum every message carries a topic and AreTheSame(null, x) is false.
            if (_topic != null && !_topic.AreTheSame(message.TopicId))
            {
                return false;
            }

            return Accepts(message.Content);
        }

        /// <summary>
        /// Whether a content is one this playlist plays. An expired voice note, or media
        /// edited into something else, is not.
        /// </summary>
        public bool Accepts(MessageContent content)
        {
            return _filter is SearchMessagesFilterAudio
                ? content is MessageAudio
                : content is MessageVoiceNote or MessageVideoNote;
        }

        public PlaybackItem Create(Message message)
        {
            return new PlaybackItemMessage(_xamlRoot, new MessageWithOwner(ClientService, message), _topic);
        }

        /// <summary>
        /// Records the message the session starts from, so the first page in either
        /// direction is searched from it rather than from the end of the chat.
        /// </summary>
        public void Seed(long messageId)
        {
            _startId = messageId;
            _endId = messageId;
        }

        /// <summary>
        /// Widens the cursors after items were added from somewhere other than a page - a
        /// message arriving while the session is open, or the seed page itself.
        /// </summary>
        public void Extend(long messageId)
        {
            // The start of the playlist is the highest message id when playing audio and the
            // lowest when playing voice notes, which is why neither of these is a plain min
            // or max on its own.
            if (_newestFirst ? messageId > _startId : messageId < _startId)
            {
                _startId = messageId;
            }

            if (_newestFirst ? messageId < _endId : messageId > _endId)
            {
                _endId = messageId;
            }
        }

        public override bool HasMore(bool forward)
        {
            return forward ? _hasMoreEnd : _hasMoreStart;
        }

        public override async Task<IList<PlaybackItem>> LoadMoreAsync(bool forward)
        {
            // Towards the end of the playlist is towards older messages for audio, and
            // towards newer ones for voice and video notes.
            var older = forward == _newestFirst;
            var from = forward ? _endId : _startId;

            // searchChatMessages returns in decreasing message id. A zero offset starts at
            // from_message_id and walks down; a negative one walks up instead, and the API
            // requires the limit to exceed it, hence the extra one.
            var offset = older ? 0 : -Limit;
            var limit = older ? Limit : Limit + 1;

            var response = await ClientService.SendAsync(new SearchChatMessages(_chatId, _topic, string.Empty, null, from, offset, limit, _filter));
            if (response is not FoundChatMessages messages)
            {
                // An error is not proof there is nothing left, but retrying on every move
                // would hammer the server, so treat it as the end.
                SetHasMore(forward, false);
                return Array.Empty<PlaybackItem>();
            }

            var result = new List<PlaybackItem>(messages.Messages.Count);

            // The search always answers in decreasing message id, which is playlist order
            // only when the playlist is newest first. Direction does not come into it: what
            // changes with direction is which end the caller puts the page on.
            foreach (var message in _newestFirst ? messages.Messages : Reversed(messages.Messages))
            {
                // from_message_id comes back with the page it anchors, and it is already in
                // the playlist.
                if (older ? message.Id >= from : message.Id <= from)
                {
                    continue;
                }

                result.Add(new PlaybackItemMessage(_xamlRoot, new MessageWithOwner(ClientService, message), _topic));
            }

            // Nothing new past the anchor means the end, whatever total_count claims: the
            // filtered search can legitimately return a page made only of the anchor.
            SetHasMore(forward, result.Count > 0);

            if (result.Count > 0)
            {
                // The item furthest from the anchor is the one the next page continues from,
                // and it sits at the far end of the page in playlist order - which is the
                // last item going forward and the first one going back.
                var furthest = (PlaybackItemMessage)result[forward ? result.Count - 1 : 0];

                if (forward)
                {
                    _endId = furthest.Id;
                }
                else
                {
                    _startId = furthest.Id;
                }
            }

            return result;
        }

        private void SetHasMore(bool forward, bool value)
        {
            if (forward)
            {
                _hasMoreEnd = value;
            }
            else
            {
                _hasMoreStart = value;
            }
        }

        private static IEnumerable<Message> Reversed(Vector<Message> messages)
        {
            for (int i = messages.Count - 1; i >= 0; i--)
            {
                yield return messages[i];
            }
        }

        private const int Limit = 50;
    }

    /// <summary>
    /// The profile audio of one user.
    /// </summary>
    /// <remarks>
    /// getUserProfileAudios pages by offset from the start of the list, so there is nothing
    /// to load before what the session started with, and a caller that already paged some in
    /// hands over this object rather than making the service start again from zero.
    /// </remarks>
    public partial class UserProfileAudioPlaybackSource : PlaybackSource
    {
        private readonly XamlRoot _xamlRoot;
        private readonly long _userId;

        private int _offset;
        private bool _hasMore = true;

        public UserProfileAudioPlaybackSource(IClientService clientService, XamlRoot xamlRoot, long userId)
            : base(clientService)
        {
            _xamlRoot = xamlRoot;
            _userId = userId;
        }

        public long UserId => _userId;

        /// <summary>
        /// Moves the cursor past items the caller already loaded and is handing over.
        /// </summary>
        /// <remarks>
        /// Paging here is by position, so those items have to be the start of the profile
        /// list and in its order: an offset that does not match what the caller holds does
        /// not just repeat items, it skips the ones in between. Which is also why adding or
        /// removing an audio at the front has to move the cursor with it - hence negative
        /// counts.
        /// </remarks>
        public void Skip(int count)
        {
            _offset = Math.Max(0, _offset + count);
        }

        public override bool HasMore(bool forward)
        {
            return forward && _hasMore;
        }

        public override async Task<IList<PlaybackItem>> LoadMoreAsync(bool forward)
        {
            if (!forward)
            {
                return Array.Empty<PlaybackItem>();
            }

            var response = await ClientService.SendAsync(new GetUserProfileAudios(_userId, _offset, Limit));
            if (response is not Audios audios || audios.AudiosValue.Count == 0)
            {
                _hasMore = false;
                return Array.Empty<PlaybackItem>();
            }

            var result = new List<PlaybackItem>(audios.AudiosValue.Count);

            foreach (var audio in audios.AudiosValue)
            {
                result.Add(new PlaybackItemProfileAudio(_xamlRoot, new AudioWithOwner(ClientService, _userId, audio)));
            }

            _offset += audios.AudiosValue.Count;
            _hasMore = _offset < audios.TotalCount;

            return result;
        }

        private const int Limit = 50;
    }
}
