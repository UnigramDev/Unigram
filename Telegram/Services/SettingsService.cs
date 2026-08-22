//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Services.Settings;
using Telegram.Td.Api;
using AutoDownloadSettings = Telegram.Services.Settings.AutoDownloadSettings;

namespace Telegram.Services
{
    public interface ISettingsService
    {
        int Session { get; }
        long UserId { get; set; }
        bool UseTestDC { get; set; }

        ChatSettingsBase Chats { get; }
        NotificationsSettings Notifications { get; }
        TranslateSettings Translate { get; }
        RecentEmojiSettings RecentEmoji { get; }
        AutoDownloadSettings AutoDownload { get; set; }
        VideoSettings Video { get; }

        bool HideArchivedChats { get; set; }
        bool IsSecretPreviewsEnabled { get; set; }
        int LastMessageTtl { get; set; }

        void SetChatPinnedMessage(long chatId, long messageId);
        long GetChatPinnedMessage(long chatId);

        void Clear();
    }

    public partial class SettingsService : SettingsServiceBase, ISettingsService
    {
        private readonly int _session;
        private readonly ISettingsStore _own;

        public SettingsService(int session)
            : base(ApplicationDataSettingsStore.Local.GetContainer($"{session}"))
        {
            _session = session;
            _own = _container;
        }

        // LifetimeService discovers sessions from the folders on disk, and creates them, before
        // any SettingsService exists for them: ClientService reads UseTestDC while it is being
        // constructed, so the value has to be in the container first.
        public static bool IsAuthorized(int session)
        {
            return ApplicationDataSettingsStore.Local.TryGetContainer($"{session}", out var container)
                && container.ContainsKey("UserId");
        }

        // Proxies were per account because TDLib keeps no shared state; they now live in the
        // app's own database, and this is the last of that arrangement -- ProxyService.Migrate
        // reads it once for accounts upgrading from before the move. Absent means true, which is
        // what the setting defaulted to. Delete it with the migration.
        public static bool ConsumeUseSystemProxy(int session)
        {
            var container = ApplicationDataSettingsStore.Local.GetContainer($"{session}");
            var value = container.GetValueOrDefault("UseSystemProxy", true);

            container.Remove("UseSystemProxy");
            return value;
        }

        public static void SetUseTestDC(int session, bool value)
        {
            ApplicationDataSettingsStore.Local.GetContainer($"{session}").SetValue("UseTestDC", value);
        }

        public int Session => _session;

        private ChatSettingsBase _chats;
        public ChatSettingsBase Chats => _chats ??= new ChatSettingsBase(_own);

        private bool? _hideArchivedChats;
        public bool HideArchivedChats
        {
            get => _hideArchivedChats ??= GetValueOrDefault("HideArchivedChats", false);
            set => AddOrUpdateValue(ref _hideArchivedChats, "HideArchivedChats", value);
        }

        private RecentEmojiSettings _recentEmoji;
        public RecentEmojiSettings RecentEmoji => _recentEmoji ??= new RecentEmojiSettings(_container);

        private TranslateSettings _translate;
        public TranslateSettings Translate => _translate ??= new TranslateSettings(_container);

        private NotificationsSettings _notifications;
        public NotificationsSettings Notifications => _notifications ??= new NotificationsSettings(_container);

        private ISettingsStore _autoDownloadStore;
        private ISettingsStore AutoDownloadStore => _autoDownloadStore ??= _own.GetContainer("AutoDownload");

        private AutoDownloadSettings _autoDownload;
        public AutoDownloadSettings AutoDownload
        {
            get => _autoDownload ??= new AutoDownloadSettings(AutoDownloadStore);
            set
            {
                _autoDownload = value ?? AutoDownloadSettings.Default;
                _autoDownload.Save(AutoDownloadStore);
            }
        }

        private VideoSettings _video;
        public VideoSettings Video => _video ??= new VideoSettings(_own);

        private bool? _useTestDC;
        public bool UseTestDC
        {
            get => _useTestDC ??= GetValueOrDefault(_own, "UseTestDC", false);
            set => AddOrUpdateValue(ref _useTestDC, _own, "UseTestDC", value);
        }

        private long? _userId;
        public long UserId
        {
            get => _userId ??= GetValueOrDefault(_own, "UserId", 0L);
            set => AddOrUpdateValue(ref _userId, _own, "UserId", value);
        }

        private bool? _isSecretPreviewsEnabled;
        public bool IsSecretPreviewsEnabled
        {
            get => _isSecretPreviewsEnabled ??= GetValueOrDefault("IsSecretPreviewsEnabled", false);
            set => AddOrUpdateValue(ref _isSecretPreviewsEnabled, "IsSecretPreviewsEnabled", value);
        }

        private int? _lastMessageTtl;
        public int LastMessageTtl
        {
            get => _lastMessageTtl ??= GetValueOrDefault("LastMessageTtl", 7);
            set => AddOrUpdateValue(ref _lastMessageTtl, "LastMessageTtl", value);
        }

        private ISettingsStore _pinnedMessages;
        private ISettingsStore PinnedMessages => _pinnedMessages ??= _own.GetContainer("PinnedMessages");

        public void SetChatPinnedMessage(long chatId, long messageId)
        {
            AddOrUpdateValue(PinnedMessages, $"{chatId}", messageId);
        }

        public long GetChatPinnedMessage(long chatId)
        {
            return GetValueOrDefault(PinnedMessages, $"{chatId}", 0L);
        }

        public new void Clear()
        {
            var useTestDC = UseTestDC;

            // Values.Clear() leaves the sub-containers behind, so auto-download, video positions
            // and pinned messages would outlive the account they belong to.
            _own.DeleteContainer("AutoDownload");
            _own.DeleteContainer("Video");
            _own.DeleteContainer("PinnedMessages");
            _own.DeleteContainer("Emoji");
            _own.Clear();

            ResetCache();

            UseTestDC = useTestDC;
            _own.Flush();
        }

        // Every value is cached on first read, and a log out is followed by a log in on the same
        // session id, so emptying the container is only half of it.
        private void ResetCache()
        {
            _chats = null;
            _notifications = null;
            _translate = null;
            _recentEmoji = null;
            _hideArchivedChats = null;
            _autoDownload = null;
            _video = null;

            // Both point at containers Clear has just deleted.
            _autoDownloadStore = null;
            _pinnedMessages = null;

            _useTestDC = null;
            _userId = null;
            _isSecretPreviewsEnabled = null;
            _lastMessageTtl = null;
        }
    }

    public partial class ChatSettingsBase : SettingsServiceBase
    {
        public ChatSettingsBase(ISettingsStore container = null)
            : base(container)
        {
        }

        public object this[long chatId, MessageTopic topicId, ChatSetting key]
        {
            //get => GetValueOrDefault<object>(chatId + key, null);
            set => AddOrUpdateValue(ConvertToKey(chatId, topicId, key), value);
        }

        public bool TryRemove<T>(long chatId, MessageTopic topicId, ChatSetting key, out T value)
        {
            var setting = ConvertToKey(chatId, topicId, key);
            if (TryGetValue(_container, setting, out value))
            {
                _container.Remove(setting);
                return true;
            }

            value = default;
            return false;
        }

        public bool TryGet<T>(long chatId, MessageTopic topicId, ChatSetting key, out T value)
        {
            var setting = ConvertToKey(chatId, topicId, key);
            return TryGetValue(_container, setting, out value);
        }

        public T GetValueOrDefault<T>(long chatId, MessageTopic topicId, ChatSetting key, T defaultValue)
        {
            var setting = ConvertToKey(chatId, topicId, key);
            if (TryGetValue(_container, setting, out T value))
            {
                return value;
            }

            return defaultValue;
        }

        public void Clear(long chatId, MessageTopic topicId)
        {
            var setting1 = ConvertToKey(chatId, topicId, ChatSetting.ReadInboxMaxId);
            var setting2 = ConvertToKey(chatId, topicId, ChatSetting.Index);
            var setting3 = ConvertToKey(chatId, topicId, ChatSetting.Pixel);

            _container.Remove(setting1);
            _container.Remove(setting2);
            _container.Remove(setting3);
        }

        private string ConvertToKey(long chatId, MessageTopic topicId, ChatSetting setting)
        {
            return topicId switch
            {
                MessageTopicDirectMessages directMesages => $"{chatId}{directMesages.DirectMessagesChatTopicId}{setting}",
                MessageTopicForum forum => $"{chatId}{forum.ForumTopicId << 20}{setting}",
                MessageTopicSavedMessages savedMessages => $"{chatId}{savedMessages.SavedMessagesTopicId}{setting}",
                MessageTopicThread thread => $"{chatId}{thread.MessageThreadId}{setting}",
                _ => $"{chatId}{setting}"
            };
        }
    }

    public enum ChatSetting
    {
        Index,
        Pixel,
        ReadInboxMaxId,
        IsTranslating,
        PaidMessageStarCount
    }
}
