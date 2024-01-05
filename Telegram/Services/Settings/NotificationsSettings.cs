//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using Telegram.Td.Api;
using Windows.Storage;

namespace Telegram.Services.Settings
{
    public class NotificationsSettings : SettingsServiceBase
    {
        private readonly Dictionary<Type, ScopeNotificationSettings> _scopeNotificationSettings = new();

        public NotificationsSettings(ApplicationDataContainer container)
            : base(container)
        {

        }

        public Dictionary<Type, ScopeNotificationSettings> Scope => _scopeNotificationSettings;

        public int GetMutedFor(Chat chat, ForumTopic topic)
        {
            if (topic.NotificationSettings.UseDefaultMuteFor)
            {
                return GetMutedFor(chat);
            }

            return topic.NotificationSettings.MuteFor;
        }

        public int GetMutedFor(Chat chat)
        {
            if (chat.NotificationSettings.UseDefaultMuteFor && TryGetScope(chat, out var scope))
            {
                return scope.MuteFor;
            }

            return chat.NotificationSettings.MuteFor;
        }

        public bool GetShowPreview(Chat chat)
        {
            if (chat.NotificationSettings.UseDefaultShowPreview && TryGetScope(chat, out var scope))
            {
                return scope.ShowPreview;
            }

            return chat.NotificationSettings.ShowPreview;
        }

        public bool GetMuteStories(Chat chat)
        {
            if (chat.NotificationSettings.UseDefaultMuteStories && TryGetScope(chat, out var scope))
            {
                return scope.MuteStories;
            }

            return chat.NotificationSettings.MuteStories;
        }

        public ScopeNotificationSettings GetScope(Chat chat)
        {
            TryGetScope(chat, out var scope);
            return scope;
        }

        public bool TryGetScope(Chat chat, out ScopeNotificationSettings value)
        {
            Type scope = null;
            switch (chat.Type)
            {
                case ChatTypePrivate:
                case ChatTypeSecret:
                    scope = typeof(NotificationSettingsScopePrivateChats);
                    break;
                case ChatTypeBasicGroup:
                    scope = typeof(NotificationSettingsScopeGroupChats);
                    break;
                case ChatTypeSupergroup supergroup:
                    scope = supergroup.IsChannel ? typeof(NotificationSettingsScopeChannelChats) : typeof(NotificationSettingsScopeGroupChats);
                    break;
            }

            if (scope != null && _scopeNotificationSettings.TryGetValue(scope, out value))
            {
                return true;
            }

            value = null;
            return false;
        }

        private bool? _inAppPreview;
        public bool InAppPreview
        {
            get => _inAppPreview ??= GetValueOrDefault("InAppPreview", true);
            set => AddOrUpdateValue(ref _inAppPreview, "InAppPreview", value);
        }

        private bool? _inAppVibrate;
        public bool InAppVibrate
        {
            get => _inAppVibrate ??= GetValueOrDefault("InAppVibrate", true);
            set => AddOrUpdateValue(ref _inAppVibrate, "InAppVibrate", value);
        }

        private bool? _inAppFlash;
        public bool InAppFlash
        {
            get => _inAppFlash ??= GetValueOrDefault("InAppFlash", true);
            set => AddOrUpdateValue(ref _inAppFlash, "InAppFlash", value);
        }

        private bool? _inAppSounds;
        public bool InAppSounds
        {
            get => _inAppSounds ??= GetValueOrDefault("InAppSounds", true);
            set => AddOrUpdateValue(ref _inAppSounds, "InAppSounds", value);
        }

        private bool? _includeMutedChats;
        public bool IncludeMutedChats
        {
            get => _includeMutedChats ??= GetValueOrDefault("IncludeMutedChats", false);
            set => AddOrUpdateValue(ref _includeMutedChats, "IncludeMutedChats", value);
        }

        private bool? _countUnreadMessages;
        public bool CountUnreadMessages
        {
            get => _countUnreadMessages ??= GetValueOrDefault("CountUnreadMessages", true);
            set => AddOrUpdateValue(ref _countUnreadMessages, "CountUnreadMessages", value);
        }
    }
}
