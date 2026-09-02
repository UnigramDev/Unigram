//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Telegram.Td.Api;
using Telegram.Td.Api;

namespace Telegram.Services
{
    public partial interface ICacheService
    {
        CacheCounts GetCacheCounts();

        CacheSize GetCacheSize();

        void ResetInflationCounters();
    }

    /// <summary>
    /// What the chats and the files behind them weigh, walked object by object.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="CacheCounts"/> because it costs a walk of the whole graph, so it
    /// is taken when asked for rather than every time the page is opened.
    /// </remarks>
    public partial class CacheSize
    {
        public int Chats { get; init; }

        public long ChatBytes { get; init; }

        /// <summary>
        /// The part of <see cref="ChatBytes"/> that is last messages.
        /// </summary>
        public long LastMessageBytes { get; init; }

        public int Files { get; init; }

        /// <summary>
        /// Files a chat or its last message reaches, which is what would go with them.
        /// </summary>
        public int FilesFromChats { get; init; }

        public long FileBytes { get; init; }

        /// <summary>
        /// The part of <see cref="FileBytes"/> that is the persistent remote id.
        /// </summary>
        public long RemoteIdBytes { get; init; }

        /// <summary>
        /// Objects the walk did not know and counted flat, which is how much it is missing.
        /// </summary>
        public int Opaque { get; init; }

        public double Seconds { get; init; }
    }

    /// <summary>
    /// What one cache paid to fetch back the objects it did not keep.
    /// </summary>
    public partial class CacheInflation
    {
        public string Name { get; init; }

        /// <summary>
        /// Objects fetched back because something read an id that was registered but not held.
        /// </summary>
        public int Count { get; init; }

        /// <summary>
        /// Reads that gave up: TDLib answered with an error, or did not answer in time.
        /// </summary>
        public int Failures { get; init; }

        public double Seconds { get; init; }

        public double Slowest { get; init; }
    }

    /// <summary>
    /// How many objects each of the caches holds, at one instant.
    /// </summary>
    /// <remarks>
    /// Counts and not bytes: what an entry costs depends on what is inside it, and nothing here
    /// can be measured without walking the graph. Nothing grows except by holding more entries
    /// though, so the counts are what says where the heap went.
    /// </remarks>
    public partial class CacheCounts
    {
        public int Chats { get; init; }

        /// <summary>
        /// Ids TDLib has mentioned, of which <see cref="Chats"/> are the ones something read.
        /// </summary>
        public int ChatsKnown { get; init; }

        /// <summary>
        /// Chats the app has read by identifier at least once, which is what making them lazy
        /// would be worth. Zero unless ChatReadsDebug is on.
        /// </summary>
        public int ChatsRead { get; init; }

        /// <summary>
        /// Chats holding a last message, which is a whole <see cref="Td.Api.Message"/> with its
        /// content: the part of the chat cache that is not a fixed size per entry.
        /// </summary>
        public int ChatLastMessages { get; init; }

        public int ChatLists { get; init; }
        public int ChatPositions { get; init; }
        public int PendingDeletes { get; init; }

        public int Users { get; init; }
        public int UsersKnown { get; init; }
        public int UsersFull { get; init; }
        public int UsersFullKnown { get; init; }
        public int BasicGroups { get; init; }
        public int BasicGroupsKnown { get; init; }
        public int BasicGroupsFull { get; init; }
        public int BasicGroupsFullKnown { get; init; }
        public int Supergroups { get; init; }
        public int SupergroupsKnown { get; init; }
        public int SupergroupsFull { get; init; }
        public int SupergroupsFullKnown { get; init; }
        public int Communities { get; init; }
        public int CommunitiesFull { get; init; }
        public int SecretChats { get; init; }
        public int UsersToChats { get; init; }
        public int ChatsAccessibleUntil { get; init; }

        public int Forums { get; init; }
        public int ForumTopics { get; init; }
        public int SavedMessagesTopics { get; init; }
        public int DirectMessagesChats { get; init; }
        public int DirectMessagesTopics { get; init; }

        public int StoryLists { get; init; }
        public int StoryPositions { get; init; }
        public int ActiveStories { get; init; }

        public int Files { get; init; }
        public int UnverifiedFiles { get; init; }

        /// <summary>
        /// Files parsed and not kept, because nothing was holding the identifier they arrived
        /// under. What the cache is no longer paying for.
        /// </summary>
        public int FilesDropped { get; init; }
        public int CompletedDownloads { get; init; }
        public int CanceledDownloads { get; init; }
        public int ExplicitDownloads { get; init; }
        public int StreamingFiles { get; init; }

        public int ChatActions { get; init; }
        public int TopicActions { get; init; }
        public int GroupCalls { get; init; }
        public int MessageAlbums { get; init; }
        public int UnreadCounts { get; init; }

        public int ChatFolders { get; init; }
        public int MessageEffects { get; init; }
        public int Reactions { get; init; }
        public int SavedMessagesTags { get; init; }
        public int WelcomeMessages { get; init; }
        public int AttachmentMenuBots { get; init; }
        public int TimeZones { get; init; }
        public int RecentChats { get; init; }

        public int StickerSets { get; init; }
        public int MaskSets { get; init; }
        public int EmojiSets { get; init; }
        public int RecentStickers { get; init; }
        public int FavoriteStickers { get; init; }
        public int SavedAnimations { get; init; }

        /// <summary>
        /// One entry per cache that fetches what it does not hold.
        /// </summary>
        public IList<CacheInflation> Inflations { get; init; }

        /// <summary>
        /// Entries held across every cache, which is the one number worth watching over a
        /// session. <see cref="ChatLastMessages"/> is left out of it: those chats are counted
        /// already, and this counts containers rather than what is reachable from them.
        /// </summary>
        public int Total =>
            Chats + ChatPositions + PendingDeletes +
            Users + UsersFull + BasicGroups + BasicGroupsFull + Supergroups + SupergroupsFull +
            Communities + CommunitiesFull + SecretChats + UsersToChats + ChatsAccessibleUntil +
            Forums + ForumTopics + SavedMessagesTopics + DirectMessagesChats + DirectMessagesTopics +
            StoryLists + StoryPositions + ActiveStories +
            Files + UnverifiedFiles + CompletedDownloads + CanceledDownloads + ExplicitDownloads + StreamingFiles +
            ChatActions + TopicActions + GroupCalls + MessageAlbums + UnreadCounts +
            ChatFolders + MessageEffects + Reactions + SavedMessagesTags + WelcomeMessages +
            AttachmentMenuBots + TimeZones + RecentChats +
            StickerSets + MaskSets + EmojiSets + RecentStickers + FavoriteStickers + SavedAnimations;
    }

    public partial class ClientService
    {
        /// <summary>
        /// Walks the chats and the files, adding up what they weigh.
        /// </summary>
        /// <remarks>
        /// Chats first and files after, so that a file a chat reaches is charged to the chats and
        /// the second pass adds only the ones nothing reached. Each chat is walked under its own
        /// lock, the one the update handlers take to mutate it.
        /// </remarks>
        public CacheSize GetCacheSize()
        {
            var timestamp = Stopwatch.GetTimestamp();
            var size = new ObjectSize();

            var chats = _chats.Values;

            foreach (var chat in chats)
            {
                lock (chat)
                {
                    size.Add(chat);
                }
            }

            var fromChats = size.FileCount;

            foreach (var file in SnapshotFiles())
            {
                if (file != null)
                {
                    size.Add(file);
                }
            }

            return new CacheSize
            {
                Chats = chats.Count,
                ChatBytes = size.Bytes,
                LastMessageBytes = size.LastMessageBytes,
                Files = size.FileCount,
                FilesFromChats = fromChats,
                FileBytes = size.FileBytes,
                RemoteIdBytes = size.RemoteIdBytes,
                Opaque = size.Opaque,
                Seconds = (Stopwatch.GetTimestamp() - timestamp) / (double)Stopwatch.Frequency
            };
        }

        /// <summary>
        /// A copy of the file cache to walk over.
        /// </summary>
        /// <remarks>
        /// It is written on the TDLib thread without a lock, so a copy taken while it grows can
        /// catch a resize halfway. This answers a diagnostic, which is worth a retry and not a
        /// lock on the path every parsed file goes through.
        /// </remarks>
        private File[] SnapshotFiles()
        {
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    return _files.Values.ToArray();
                }
                catch
                {
                    // Torn by a resize. Try again.
                }
            }

            return Array.Empty<File>();
        }

        public void ResetInflationCounters()
        {
            //_chats.ResetInflationCounters();
            _users.ResetInflationCounters();
            _usersFull.ResetInflationCounters();
            _basicGroups.ResetInflationCounters();
            _basicGroupsFull.ResetInflationCounters();
            _supergroups.ResetInflationCounters();
            _supergroupsFull.ResetInflationCounters();
        }

        /// <summary>
        /// A snapshot of everything the session is holding on to, for the diagnostics page.
        /// </summary>
        /// <remarks>
        /// Taken on demand and off every hot path, so it can afford to walk what it has to. The
        /// caches that are written from the TDLib thread without a lock are only asked for their
        /// Count, which is a field read: a row can be an entry stale, and that is all.
        /// </remarks>
        public CacheCounts GetCacheCounts()
        {
            static CacheInflation Inflation<TKey, TValue>(string name, CacheDictionary<TKey, TValue> cache) where TValue : class
            {
                return new CacheInflation
                {
                    Name = name,
                    Count = cache.InflationCount,
                    Failures = cache.InflationFailureCount,
                    Seconds = cache.InflationSeconds,
                    Slowest = cache.SlowestInflationSeconds
                };
            }

            var lastMessages = 0;
            _chats.ForEach(chat =>
            {
                if (chat.LastMessage != null)
                {
                    lastMessages++;
                }
            });

            var chatPositions = 0;
            int chatLists;
            int pendingDeletes;

            // _chatLists then the service's own lock, the order ClearChatLists takes them in.
            lock (_chatLists)
            {
                chatLists = _chatLists.Count;
                pendingDeletes = _pendingDeleteChats.Count;

                foreach (var service in _chatLists.Values)
                {
                    chatPositions += service.ItemCount;
                }
            }

            // Over a snapshot rather than under the dictionary's read lock: each count takes
            // the service's own lock, and nothing else here nests one lock inside another.
            var forumTopics = 0;
            foreach (var forum in _forums.Values)
            {
                forumTopics += forum.TopicCount;
            }

            var directMessagesTopics = 0;
            foreach (var chat in _directMessagesChats.Values)
            {
                directMessagesTopics += chat.TopicCount;
            }

            var storyPositions = 0;
            int storyLists;

            lock (_storyList)
            {
                storyLists = _storyList.Count;

                foreach (var list in _storyList.Values)
                {
                    storyPositions += list.Count;
                }
            }

            var chatActions = 0;
            foreach (var actions in _chatActions.Values)
            {
                chatActions += actions.Count;
            }

            var topicActions = 0;
            foreach (var actions in _topicActions.Values)
            {
                topicActions += actions.Count;
            }

            int completedDownloads;
            int canceledDownloads;
            int explicitDownloads;
            int streamingFiles;

            lock (_downloadsLock)
            {
                completedDownloads = _completedDownloads.Count;
                canceledDownloads = _canceledDownloads.Count;
                explicitDownloads = _explicitDownloads.Count;
                streamingFiles = _streamingFiles.Count;
            }

            int recentChats;

            lock (_recentChatsLock)
            {
                recentChats = _recentChats.Count;
            }

            int chatFolders;

            lock (_chatFoldersLock)
            {
                chatFolders = _chatFolders2.Count;
            }

            return new CacheCounts
            {
                Chats = _chats.Count,
                ChatsRead = ChatsRead,
                //ChatsKnown = _chats.KnownCount,
                ChatLastMessages = lastMessages,
                ChatLists = chatLists,
                ChatPositions = chatPositions,
                PendingDeletes = pendingDeletes,

                Users = _users.Count,
                UsersKnown = _users.KnownCount,
                UsersFull = _usersFull.Count,
                UsersFullKnown = _usersFull.KnownCount,
                BasicGroups = _basicGroups.Count,
                BasicGroupsKnown = _basicGroups.KnownCount,
                BasicGroupsFull = _basicGroupsFull.Count,
                BasicGroupsFullKnown = _basicGroupsFull.KnownCount,
                Supergroups = _supergroups.Count,
                SupergroupsKnown = _supergroups.KnownCount,
                SupergroupsFull = _supergroupsFull.Count,
                SupergroupsFullKnown = _supergroupsFull.KnownCount,
                Communities = _communities.Count,
                CommunitiesFull = _communitiesFull.Count,
                SecretChats = _secretChats.Count,
                UsersToChats = _usersToChats.Count,
                ChatsAccessibleUntil = _chatAccessibleUntil.Count,

                Forums = _forums.Count,
                ForumTopics = forumTopics,
                SavedMessagesTopics = _savedMessages.TopicCount,
                DirectMessagesChats = _directMessagesChats.Count,
                DirectMessagesTopics = directMessagesTopics,

                StoryLists = storyLists,
                StoryPositions = storyPositions,
                ActiveStories = _activeStories.Count,

                Files = _files.Count,
                UnverifiedFiles = _unverifiedFiles.Count,
                FilesDropped = _filesDropped,
                CompletedDownloads = completedDownloads,
                CanceledDownloads = canceledDownloads,
                ExplicitDownloads = explicitDownloads,
                StreamingFiles = streamingFiles,

                ChatActions = chatActions,
                TopicActions = topicActions,
                GroupCalls = _groupCalls.Count,
                MessageAlbums = _lastMessageAlbums.Count,
                UnreadCounts = _unreadCounts.Count,

                ChatFolders = chatFolders,
                MessageEffects = _effects.Count,
                Reactions = _cachedReactions.Count,
                SavedMessagesTags = _savedMessagesTags.Count,
                WelcomeMessages = _welcomeMessages.Count,
                AttachmentMenuBots = _attachmentMenuBots?.Count ?? 0,
                TimeZones = _timezones.Count,
                RecentChats = recentChats,

                StickerSets = _installedStickerSets?.Count ?? 0,
                MaskSets = _installedMaskSets?.Count ?? 0,
                EmojiSets = _installedEmojiSets?.Count ?? 0,
                RecentStickers = _recentStickers?.Count ?? 0,
                FavoriteStickers = _favoriteStickers?.Count ?? 0,
                SavedAnimations = _savedAnimations?.Count ?? 0,

                Inflations = new List<CacheInflation>
                {
                    //Inflation("Chats", _chats),
                    Inflation("Users", _users),
                    Inflation("Users full", _usersFull),
                    Inflation("Basic groups", _basicGroups),
                    Inflation("Basic groups full", _basicGroupsFull),
                    Inflation("Supergroups", _supergroups),
                    Inflation("Supergroups full", _supergroupsFull),
                },
            };
        }
    }
}
