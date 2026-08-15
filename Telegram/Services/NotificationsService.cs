//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.Views;
using Windows.Data.Xml.Dom;
using Windows.Storage;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Services
{
    public interface INotificationsService
    {
        Task ProcessAsync(Dictionary<string, string> data);

        void PlaySound(bool sent);

        #region Chats related

        void SetMuteFor(Chat chat, int muteFor, XamlRoot xamlRoot);
        void SetMuteFor(ForumTopic topic, int muteFor, XamlRoot xamlRoot);

        void SetSound(Chat chat, bool silent, XamlRoot xamlRoot);

        #endregion
    }

    public partial class NotificationsService : INotificationsService
    {
        private readonly IClientService _clientService;
        private readonly ISession _sessionService;
        private readonly ISettingsService _settings;
        private readonly IEventAggregator _aggregator;

        private readonly DebouncedProperty<int> _unreadCount;

        private readonly bool? _suppress;

        public NotificationsService(IClientService clientService, ISettingsService settingsService, ISession sessionService, IEventAggregator aggregator)
        {
            _clientService = clientService;
            _settings = settingsService;
            _sessionService = sessionService;
            _aggregator = aggregator;

            _unreadCount = new DebouncedProperty<int>(200, UpdateUnreadCount, useBackgroundThread: true);

            Subscribe();

            var unreadCount = _clientService.GetUnreadCount(new ChatListMain());
            Handle(unreadCount.UnreadChatCount);
            Handle(unreadCount.UnreadMessageCount);
        }

        static NotificationsService()
        {
            RemoveCollections();
        }

        private static async void RemoveCollections()
        {
            if (SettingsService.Current.Notifications.HasRemovedCollections)
            {
                return;
            }

            SettingsService.Current.Notifications.HasRemovedCollections = true;

            try
            {
                await ToastNotificationManager.GetDefault().GetToastCollectionManager().RemoveAllToastCollectionsAsync();
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        private void Subscribe()
        {
            _aggregator.Subscribe<UpdateUnreadMessageCount>(this, Handle)
                .Subscribe<UpdateUnreadChatCount>(Handle)
                .Subscribe<UpdateSuggestedActions>(Handle)
                .Subscribe<UpdateServiceNotification>(Handle)
                .Subscribe<UpdateTermsOfService>(Handle)
                .Subscribe<UpdateSpeedLimitNotification>(Handle)
                .Subscribe<UpdateNotification>(Handle)
                .Subscribe<UpdateNotificationGroup>(Handle)
                .Subscribe<UpdateHavePendingNotifications>(Handle)
                .Subscribe<UpdateActiveNotifications>(Handle);
        }

        private void UpdateUnreadCount(int count)
        {
            try
            {
                var updater = BadgeUpdateManager.CreateBadgeUpdaterForApplication("App");
                if (count == 0)
                {
                    updater.Clear();
                    return;
                }

                var document = BadgeUpdateManager.GetTemplateContent(BadgeTemplateType.BadgeNumber);
                var element = document.SelectSingleNode("/badge") as XmlElement;
                element.SetAttribute("value", count.ToString());

                updater.Update(new BadgeNotification(document));
            }
            catch { }
        }

        private async void Handle(UpdateSpeedLimitNotification update)
        {
            Logger.Info("UpdateSpeedLimitNotification");

            var text = update.IsUpload
                ? string.Format("**{0}**\n{1}", Strings.UploadSpeedLimited, string.Format(Strings.UploadSpeedLimitedMessage, _clientService.Options.PremiumUploadSpeedup))
                : string.Format("**{0}**\n{1}", Strings.DownloadSpeedLimited, string.Format(Strings.DownloadSpeedLimitedMessage, _clientService.Options.PremiumDownloadSpeedup));

            var markdown = ClientEx.ParseMarkdown(text);
            if (markdown.Entities.Count == 2)
            {
                markdown.Entities[1].Type = new TextEntityTypeTextUrl();
            }

            await ViewService.WaitForMainWindowAsync();

            var window = WindowContext.Active ?? WindowContext.Main;
            var dispatcher = window?.Dispatcher;

            dispatcher?.Dispatch(() =>
            {
                var navigationService = window.NavigationServices?.GetByFrameId($"Main{_clientService.SessionId}");
                if (navigationService == null)
                {
                    return;
                }

                var toast = ToastPopup.Show(navigationService.XamlRoot, markdown, ToastPopupIcon.SpeedLimit);
                void handler(object sender, TextUrlClickEventArgs e)
                {
                    toast.Click -= handler;
                    navigationService.ShowPromo(new PremiumSourceFeature(new PremiumFeatureImprovedDownloadSpeed()));
                }

                toast.Click += handler;
            });
        }

        public async void Handle(UpdateTermsOfService update)
        {
            Logger.Info("UpdateTermsOfService");

            if (update.TermsOfService.ShowPopup)
            {
                async void DeleteAccount(XamlRoot xamlRoot)
                {
                    var decline = await MessagePopup.ShowAsync(xamlRoot, Strings.TosUpdateDecline, Strings.TermsOfService, Strings.DeclineDeactivate, Strings.Back);
                    if (decline != ContentDialogResult.Primary)
                    {
                        Handle(update);
                        return;
                    }

                    var delete = await MessagePopup.ShowAsync(xamlRoot, Strings.TosDeclineDeleteAccount, Strings.AppName, Strings.Deactivate, Strings.Cancel);
                    if (delete != ContentDialogResult.Primary)
                    {
                        Handle(update);
                        return;
                    }

                    _clientService.Send(new DeleteAccount("Decline ToS update", string.Empty));
                }

                await ViewService.WaitForMainWindowAsync();

                var window = WindowContext.Active ?? WindowContext.Main;
                var dispatcher = window?.Dispatcher;

                dispatcher?.Dispatch(async () =>
                {
                    var xamlRoot = window.XamlRoot;
                    if (xamlRoot == null)
                    {
                        return;
                    }

                    var confirm = await MessagePopup.ShowAsync(xamlRoot, update.TermsOfService.Text, Strings.PrivacyPolicyAndTerms, Strings.Agree, Strings.Cancel);
                    if (confirm != ContentDialogResult.Primary)
                    {
                        DeleteAccount(xamlRoot);
                        return;
                    }

                    if (update.TermsOfService.MinUserAge > 0)
                    {
                        var age = await MessagePopup.ShowAsync(xamlRoot, string.Format(Strings.TosAgeText, update.TermsOfService.MinUserAge), Strings.TosAgeTitle, Strings.Agree, Strings.Cancel);
                        if (age != ContentDialogResult.Primary)
                        {
                            DeleteAccount(xamlRoot);
                            return;
                        }
                    }

                    _clientService.Send(new AcceptTermsOfService(update.TermsOfServiceId));
                });
            }
        }

        public async void Handle(UpdateSuggestedActions update)
        {
            Logger.Info("UpdateSuggestedActions");

            var enableArchiveAndMuteNewChats = update.AddedActions.FirstOrDefault(x => x is SuggestedActionEnableArchiveAndMuteNewChats);
            if (enableArchiveAndMuteNewChats == null)
            {
                return;
            }

            await ViewService.WaitForMainWindowAsync();

            var window = WindowContext.Active ?? WindowContext.Main;
            var dispatcher = window?.Dispatcher;

            dispatcher?.Dispatch(async () =>
            {
                var xamlRoot = window.XamlRoot;
                if (xamlRoot == null)
                {
                    return;
                }

                if (enableArchiveAndMuteNewChats is SuggestedActionEnableArchiveAndMuteNewChats)
                {
                    var confirm = await MessagePopup.ShowAsync(xamlRoot, Strings.HideNewChatsAlertText, Strings.HideNewChatsAlertTitle, Strings.OK, Strings.Cancel);
                    if (confirm == ContentDialogResult.Primary)
                    {
                        var response = await _clientService.SendAsync(new GetArchiveChatListSettings());
                        if (response is ArchiveChatListSettings settings)
                        {
                            settings.ArchiveAndMuteNewChatsFromUnknownUsers = true;
                            _clientService.Send(new SetArchiveChatListSettings(settings));
                        }
                    }

                    _clientService.Send(new HideSuggestedAction(enableArchiveAndMuteNewChats));
                }
            });
        }

        public async void Handle(UpdateServiceNotification update)
        {
            Logger.Info("UpdateServiceNotification");

            var caption = update.Content.GetCaption();
            if (caption == null)
            {
                return;
            }

            var text = caption.Text;
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            await ViewService.WaitForMainWindowAsync();

            var window = WindowContext.Active ?? WindowContext.Main;
            var dispatcher = window?.Dispatcher;

            dispatcher?.Dispatch(async () =>
            {
                var xamlRoot = window.XamlRoot;
                if (xamlRoot == null)
                {
                    return;
                }

                if (update.Type.StartsWith("AUTH_KEY_DROP_"))
                {
                    var confirm = await MessagePopup.ShowAsync(xamlRoot, text, Strings.AppName, Strings.LogOut, Strings.Cancel);
                    if (confirm == ContentDialogResult.Primary)
                    {
                        _clientService.Send(new Destroy());
                    }
                }
                else if (ContentPopup.IsAnyPopupOpen(xamlRoot))
                {
                    await MessagePopup.ShowAsync(xamlRoot, target: null, text, Strings.AppName, Strings.OK);
                }
                else
                {
                    await MessagePopup.ShowAsync(xamlRoot, text, Strings.AppName, Strings.OK);
                }
            });
        }

        public void Handle(UpdateUnreadMessageCount update)
        {
            if (update.ChatList is not ChatListMain || !_settings.Notifications.CountUnreadMessages || !_sessionService.IsActive)
            {
                return;
            }

            if (_settings.Notifications.IncludeMutedChats)
            {
                _unreadCount.Set(update.UnreadCount);
            }
            else
            {
                _unreadCount.Set(update.UnreadUnmutedCount);
            }

            SendUnreadCount(update.UnreadCount, update.UnreadUnmutedCount);
        }

        public void Handle(UpdateUnreadChatCount update)
        {
            if (update.ChatList is not ChatListMain || _settings.Notifications.CountUnreadMessages || !_sessionService.IsActive)
            {
                return;
            }

            if (_settings.Notifications.IncludeMutedChats)
            {
                _unreadCount.Set(update.UnreadCount);
            }
            else
            {
                _unreadCount.Set(update.UnreadUnmutedCount);
            }

            SendUnreadCount(update.UnreadCount, update.UnreadUnmutedCount);
        }

        private int _notifyIconUnreadCount;
        private int _notifyIconUnreadUnmutedCount;

        private void SendUnreadCount(int unreadCount, int unreadUnmutedCount)
        {
            unreadCount = Math.Min(_settings.Notifications.IncludeMutedChats ? unreadCount : 0, 1);
            unreadUnmutedCount = Math.Min(unreadUnmutedCount, 1);

            if (unreadCount != _notifyIconUnreadCount || unreadUnmutedCount != _notifyIconUnreadUnmutedCount)
            {
                _notifyIconUnreadCount = unreadCount;
                _notifyIconUnreadUnmutedCount = unreadUnmutedCount;

                BridgeApplicationContext.SendUnreadCount(unreadCount, unreadUnmutedCount);
            }
        }

        private ulong _lastPlayedSent;
        private ulong _lastPlayedReceived;

        public void PlaySound(bool sent)
        {
            if (!_settings.Notifications.InAppSounds)
            {
                return;
            }

            var now = Logger.TickCount;
            if (sent)
            {
                if (now - _lastPlayedSent < 500)
                {
                    return;
                }

                _lastPlayedSent = now;
            }
            else
            {
                if (now - _lastPlayedReceived < 500)
                {
                    return;
                }

                _lastPlayedReceived = now;
            }

            Task.Run(() => SoundEffects.Play(sent ? SoundEffect.Sent : SoundEffect.Received));
        }

        public void Handle(UpdateActiveNotifications update)
        {
            try
            {
                var history = ToastNotificationManager.History.GetHistory();
                var hash = new HashSet<string>();

                foreach (var item in history)
                {
                    hash.Add($"{item.Group}_{item.Tag}");
                }

                foreach (var group in update.Groups)
                {
                    foreach (var notification in group.Notifications)
                    {
                        if (hash.Contains($"{_clientService.SessionId}_{group.Id}_{notification.Id}"))
                        {
                            continue;
                        }

                        _clientService.Send(new RemoveNotification(group.Id, notification.Id));
                    }
                }
            }
            catch
            {
                foreach (var group in update.Groups)
                {
                    _clientService.Send(new RemoveNotificationGroup(group.Id, int.MaxValue));
                }
            }
        }

        public void Handle(UpdateHavePendingNotifications update)
        {
            // We want to ignore both delayed and unreceived notifications,
            // as they're the result of update difference on sync.
            if (_suppress == null && update.HaveDelayedNotifications && update.HaveUnreceivedNotifications)
            {
                //_suppress = true;
            }
            else if (_suppress == true && !update.HaveDelayedNotifications && !update.HaveUnreceivedNotifications)
            {
                //_suppress = false;
            }
        }

        public async void Handle(UpdateNotificationGroup update)
        {
            try
            {
                var history = ToastNotificationManager.History;

                foreach (var removed in update.RemovedNotificationIds)
                {
                    history.Remove($"{removed}", $"{_clientService.SessionId}_{update.NotificationGroupId}");
                }
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }

            if (_suppress == true)
            {
                Logger.Info("_suppress is true");

                // This is an unsynced message, we don't want to show a notification for it as it has been probably pushed already by WNS
                return;
            }

            if (_clientService.ConnectionState is ConnectionStateUpdating)
            {
                Logger.Info("ConnectionState is ConnectionStateUpdating");

                // This is an unsynced message, we don't want to show a notification for it as it has been probably pushed already by WNS
                return;
            }

            if (!_sessionService.IsActive && !SettingsService.Current.IsAllAccountsNotifications)
            {
                Logger.Info("Session is not active");

                return;
            }

            foreach (var notification in update.AddedNotifications)
            {
                await ProcessNotification(update.NotificationGroupId, update.NotificationSoundId, update.ChatId, notification);
                //_clientService.Send(new RemoveNotification(update.NotificationGroupId, notification.Id));
            }
        }

        public void Handle(UpdateNotification update)
        {
            if (_clientService.ConnectionState is ConnectionStateUpdating)
            {
                // This is an unsynced message, we don't want to show a notification for it as it has been probably pushed already by WNS
                return;
            }

            //ProcessNotification(update.NotificationGroupId, 0, update.Notification);
        }

        private async Task ProcessNotification(int group, long soundId, long chatId, Td.Api.Notification notification)
        {
            var time = Formatter.ToLocalTime(notification.Date);
            if (time < DateTime.Now.AddHours(-1))
            {
                _clientService.Send(new RemoveNotification(group, notification.Id));

                Logger.Info("Notification is too old");
                return;
            }

            switch (notification.Type)
            {
                case NotificationTypeNewCall:
                    break;
                case NotificationTypeNewMessage newMessage:
                    await ProcessNewMessage(group, notification.Id, newMessage.Message, time, soundId, notification.IsSilent);
                    break;
                case NotificationTypeNewSecretChat:
                    break;
            }
        }

        private async Task ProcessNewMessage(int groupId, int id, Message message, DateTime date, long soundId, bool silent)
        {
            var chat = _clientService.GetChat(message.ChatId);
            if (chat == null)
            {
                Logger.Info("Chat is null");
                return;
            }

            if (UpdateAsync(chat, message))
            {
                var caption = GetCaption(chat, silent);
                var content = GetContent(chat, message);
                var launch = GetLaunch(chat, message);
                var picture = GetPhoto(chat);
                var dateTime = date.ToUniversalTime().ToString("s") + "Z";
                var canReply = !(chat.Type is ChatTypeSupergroup super && super.IsChannel);

                Td.Api.File soundFile = null;
                if (soundId != -1 && soundId != 0 && !silent)
                {
                    Logger.Info("Custom notification sound");

                    var response = await _clientService.SendAsync(new GetSavedNotificationSound(soundId));
                    if (response is NotificationSound notificationSound)
                    {
                        if (notificationSound.Sound.Local.IsDownloadingCompleted)
                        {
                            soundFile = notificationSound.Sound;
                        }
                        else
                        {
                            // If notification sound is not yet available
                            // download it and show the notification as is.

                            _clientService.DownloadFile(notificationSound.Sound.Id, 32);
                        }
                    }
                }

                var showPreview = _settings.Notifications.GetShowPreview(chat);

                if (chat.Type is ChatTypeSecret || !showPreview || !_settings.Notifications.ShowName || LifetimeService.Current.Passcode.IsLockscreenRequired)
                {
                    picture = string.Empty;
                    caption = Strings.AppName;
                    content = Strings.YouHaveNewMessage;
                    canReply = false;
                }
                else if (!_settings.Notifications.ShowText)
                {
                    content = Strings.YouHaveNewMessage;
                    canReply = false;
                }
                else if (!_settings.Notifications.ShowReply)
                {
                    canReply = false;
                }

                UpdateToast(caption, content, $"{_sessionService.Id}", silent, silent || soundId == 0, soundFile, launch, $"{id}", $"{groupId}", picture, dateTime, canReply);
            }
        }

        private bool UpdateAsync(Chat chat, Message message)
        {
            try
            {
                var active = WindowContext.Active;
                if (active == null)
                {
                    return true;
                }

                var service = active.NavigationServices?.GetByFrameId($"Main{_clientService.SessionId}");
                if (service == null)
                {
                    return true;
                }

                if (chat.ViewAsTopics && service.CurrentPageType == typeof(ChatPage) && service.CurrentPageParam is ChatMessageTopic args)
                {
                    if (args.ChatId == chat.Id && args.MessageTopic.AreTheSame(message.TopicId))
                    {
                        Logger.Info("Topic is open");
                        return false;
                    }
                }
                else if (service.IsChatOpen(chat.Id, true))
                {
                    Logger.Info("Chat is open");
                    return false;
                }

                return true;
            }
            catch
            {
                return true;
            }
        }

        private void UpdateToast(string caption, string message, string account, bool suppressPopup, bool silent, Td.Api.File soundFile, string launch, string tag, string group, string picture, string date, bool canReply)
        {
            var xml = $"<toast launch='{launch}' displayTimestamp='{date}'>";
            xml += "<visual><binding template='ToastGeneric'>";

            if (!string.IsNullOrEmpty(picture))
            {
                xml += $"<image placement='appLogoOverride' hint-crop='circle' src='{picture}'/>";
            }

            if (LifetimeService.Current.Count > 1
                && SettingsService.Current.IsAllAccountsNotifications
                && _clientService.TryGetUser(_clientService.Options.MyId, out User user))
            {
                caption = string.Format("{0} \u2b62 {1}", caption, user.FullName());
            }

            xml += $"<text><![CDATA[{caption}]]></text><text><![CDATA[{message}]]></text>";
            xml += "</binding></visual>";

            if (!string.IsNullOrEmpty(group) && canReply)
            {
                xml += string.Format("<actions><input id='input' type='text' placeHolderContent='{0}' /><action activationType='background' placement='contextMenu' arguments='action=markAsRead&amp;", Strings.Reply);
                xml += launch;
                xml += string.Format("' content='{0}'/><action activationType='background' arguments='action=reply&amp;", Strings.MarkAsRead);
                xml += launch;
                xml += string.Format("' hint-inputId='input' content='{0}'/></actions>", Strings.Send);
            }

            /* Single notification with unread count:
<toast>
  <visual>
    <binding template="ToastGeneric">
      <text>Hello World</text>
      <text>This is a simple toast message</text>

      <group>
          <subgroup>
              <text hint-style="bodySubtle" hint-align="center">text</text>
          </subgroup>
      </group>
    </binding>
  </visual>
</toast>    */
            if (silent || soundFile != null)
            {
                xml += "<audio silent='true'/>";
            }

            xml += "</toast>";

            try
            {
                var notifier = ToastNotificationManager.CreateToastNotifier("App");
                var document = new XmlDocument();
                document.LoadXml(xml);

                var notification = new ToastNotification(document);

                if (!string.IsNullOrEmpty(tag))
                {
                    notification.Tag = tag;
                    notification.RemoteId = tag;
                }

                if (!string.IsNullOrEmpty(group))
                {
                    notification.Group = account + "_" + group;
                    notification.RemoteId += "_";
                    notification.RemoteId += group;
                }

                var ticks = Logger.TickCount;

                notification.SuppressPopup = suppressPopup || ticks - _lastShownToast <= 7000;
                notifier.Show(notification);

                if (ticks - _lastShownToast <= 7000)
                {
                    Logger.Info("Suppress popup");
                }

                if (soundFile != null && notifier.Setting == NotificationSetting.Enabled)
                {
                    SoundEffects.Play(soundFile);
                }

                if (_lastShownToast == 0 || ticks - _lastShownToast > 7000)
                {
                    _lastShownToast = ticks;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
        }

        private ulong _lastShownToast;

        public string GetLaunch(Chat chat, Message message)
        {
            var launch = string.Format(CultureInfo.InvariantCulture, "chat_id={0}", chat.Id);
            launch = string.Format(CultureInfo.InvariantCulture, "{0}&amp;session={1}", launch, _clientService.SessionId);

            if (chat.Type is not ChatTypePrivate and not ChatTypeSecret)
            {
                launch = string.Format(CultureInfo.InvariantCulture, "{0}&amp;msg_id={1}", launch, message.Id);
            }

            if (message.TopicId is MessageTopicForum messageTopicForum)
            {
                launch = string.Format(CultureInfo.InvariantCulture, "{0}&amp;forum_topic_id={1}", launch, messageTopicForum.ForumTopicId);
            }
            else if (message.TopicId is MessageTopicSavedMessages messageTopicSavedMessages)
            {
                launch = string.Format(CultureInfo.InvariantCulture, "{0}&amp;saved_messages_topic_id={1}", launch, messageTopicSavedMessages.SavedMessagesTopicId);
            }
            else if (message.TopicId is MessageTopicDirectMessages messageTopicDirectMessagesChat)
            {
                launch = string.Format(CultureInfo.InvariantCulture, "{0}&amp;feedback_chat_topic_id={1}", launch, messageTopicDirectMessagesChat.DirectMessagesChatTopicId);
            }
            else if (message.TopicId is MessageTopicThread messageTopicThread)
            {
                launch = string.Format(CultureInfo.InvariantCulture, "{0}&amp;thread_id={1}", launch, messageTopicThread.MessageThreadId);
            }

            return launch;
        }

        public async Task ProcessAsync(Dictionary<string, string> data)
        {
            var state = await _clientService.GetAuthorizationStateAsync();
            if (state is not AuthorizationStateReady)
            {
                return;
            }

            if (data.TryGetValue("action", out string action))
            {
                var chat = default(Chat);
                if (data.TryGetValue("chat_id", out string chat_id) && long.TryParse(chat_id, out long chatId))
                {
                    _clientService.TryGetChat(chatId, out chat);
                    chat ??= await _clientService.SendAsync(new GetChat(chatId)) as Chat;
                }

                if (chat == null)
                {
                    return;
                }

                if (string.Equals(action, "reply", StringComparison.OrdinalIgnoreCase) && data.TryGetValue("input", out string text))
                {
                    var messageText = text.Replace("\r\n", "\n").Replace('\v', '\n').Replace('\r', '\n');
                    var formatted = ClientEx.ParseMarkdown(messageText);

                    // TODO: topic id

                    var replyToMessage = data.TryGetValue("msg_id", out string msg_id) && long.TryParse(msg_id, out long messageId) ? new InputMessageReplyToMessage(messageId, null, 0, string.Empty) : null;
                    var response = await _clientService.SendAsync(new SendMessage(chat.Id, null, replyToMessage, new MessageSendOptions(null, false, true, 0, false, null, 0, 0, false), new InputMessageText(formatted, null, false)));

                    if (chat.Type is ChatTypePrivate && chat.LastMessage != null)
                    {
                        await _clientService.SendAsync(new ViewMessages(chat.Id, new long[] { chat.LastMessage.Id }, new MessageSourceNotification(), true));
                    }
                }
                else if (string.Equals(action, "markasread", StringComparison.OrdinalIgnoreCase) && chat.LastMessage != null)
                {
                    await _clientService.SendAsync(new ViewMessages(chat.Id, new long[] { chat.LastMessage.Id }, new MessageSourceNotification(), true));
                }
            }
        }

















        private string GetCaption(Chat chat, bool silent)
        {
            if (chat.Type is ChatTypeSecret)
            {
                return Strings.AppName;
            }

            var title = _clientService.GetTitle(chat);

            if (silent)
            {
                return string.Format("\U0001F515 {0}", title);
            }

            return title;
        }

        private string GetContent(Chat chat, Message message)
        {
            if (chat.Type is ChatTypeSecret)
            {
                return Strings.YouHaveNewMessage;
            }

            var brief = ChatCell.UpdateBriefLabel(message.Content, message.IsOutgoing, null, true, out _);
            var clean = brief.ReplaceSpoilers(false);

            var content = ChatCell.UpdateFromLabel(_clientService, chat, message) + clean.Text;

            if (message.SenderId.IsUser(_clientService.Options.MyId))
            {
                return string.Format("\U0001F4C5 {0}", content);
            }

            return content;
        }

        private string GetPhoto(Chat chat)
        {
            try
            {
                var photo = chat.Photo;
                if (photo != null && photo.Small.Local.IsDownloadingCompleted)
                {
                    var relative = Path.GetRelativePath(ApplicationData.Current.LocalFolder.Path, photo.Small.Local.Path);
                    return "ms-appdata:///local/" + relative.Replace('\\', '/');
                }
            }
            catch
            {
                // TODO: race condition
            }

            return string.Empty;
        }

        public void SetSound(Chat chat, bool silent, XamlRoot xamlRoot)
        {
            if (_settings.Notifications.TryGetScope(chat, out ScopeNotificationSettings scope))
            {
                var settings = chat.NotificationSettings.Clone();
                var value = silent ? 0L : -1;

                var useDefault = !silent;
                if (useDefault)
                {
                    value = scope.SoundId;
                }

                settings.UseDefaultSound = useDefault;
                settings.SoundId = value;

                _clientService.Send(new SetChatNotificationSettings(chat.Id, settings));

                if (silent)
                {
                    ToastPopup.Show(xamlRoot, Strings.SoundOffHint, ToastPopupIcon.SoundOff);
                }
                else
                {
                    ToastPopup.Show(xamlRoot, Strings.SoundOnHint, ToastPopupIcon.SoundOn);
                }
            }
        }

        public void SetMuteFor(Chat chat, int value, XamlRoot xamlRoot)
        {
            if (_settings.Notifications.TryGetScope(chat, out ScopeNotificationSettings scope))
            {
                var settings = chat.NotificationSettings.Clone();

                var useDefault = value == scope.MuteFor || (value >= 366 * 24 * 60 * 60 && scope.MuteFor >= 366 * 24 * 60 * 60);
                if (useDefault)
                {
                    value = scope.MuteFor;
                }

                settings.UseDefaultMuteFor = useDefault;
                settings.MuteFor = value;

                _clientService.Send(new SetChatNotificationSettings(chat.Id, settings));

                if (xamlRoot == null)
                {
                    return;
                }

                if (value == 0)
                {
                    ToastPopup.Show(xamlRoot, Strings.NotificationsUnmutedHint, ToastPopupIcon.Unmute);
                }
                else if (value >= 366 * 24 * 60 * 60)
                {
                    ToastPopup.Show(xamlRoot, Strings.NotificationsMutedHint, ToastPopupIcon.Mute);
                }
                else
                {
                    ToastPopup.Show(xamlRoot, string.Format(Strings.NotificationsMutedForHint, Locale.FormatMuteFor(value)), ToastPopupIcon.MuteFor);
                }
            }
        }

        public void SetMuteFor(ForumTopic topic, int value, XamlRoot xamlRoot)
        {
            if (_clientService.TryGetChat(topic.Info.ChatId, out Chat chat) && _settings.Notifications.TryGetScope(chat, out ScopeNotificationSettings scope))
            {
                var settings = topic.NotificationSettings.Clone();

                var useDefault = value == scope.MuteFor || (value >= 366 * 24 * 60 * 60 && scope.MuteFor >= 366 * 24 * 60 * 60);
                if (useDefault)
                {
                    value = scope.MuteFor;
                }

                settings.UseDefaultMuteFor = useDefault;
                settings.MuteFor = value;

                _clientService.Send(new SetForumTopicNotificationSettings(chat.Id, topic.Info.ForumTopicId, settings));

                if (xamlRoot == null)
                {
                    return;
                }

                if (value == 0)
                {
                    ToastPopup.Show(xamlRoot, Strings.NotificationsUnmutedHint, ToastPopupIcon.Unmute);
                }
                else if (value >= 366 * 24 * 60 * 60)
                {
                    ToastPopup.Show(xamlRoot, Strings.NotificationsMutedHint, ToastPopupIcon.Mute);
                }
                else
                {
                    ToastPopup.Show(xamlRoot, string.Format(Strings.NotificationsMutedForHint, Locale.FormatMuteFor(value)), ToastPopupIcon.MuteFor);
                }
            }
        }
    }
}
