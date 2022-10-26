using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Telegram.Td;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Messages;
using Unigram.Converters;
using Unigram.Navigation;
using Unigram.Views;
using Windows.ApplicationModel.AppService;
using Windows.Data.Xml.Dom;
using Windows.Foundation.Collections;
using Windows.Networking.PushNotifications;
using Windows.Storage;
using Windows.UI.Notifications;
using Windows.UI.Xaml.Controls;

namespace Unigram.Services
{
    public interface INotificationsService
    {
        void Register();
        Task CloseAsync();

        Task ProcessAsync(Dictionary<string, string> data);

        void PlaySound();

        #region Chats related

        void SetMuteFor(Chat chat, int muteFor);

        #endregion
    }

    public class NotificationsService : INotificationsService
        //IHandle<UpdateUnreadMessageCount>,
        //IHandle<UpdateUnreadChatCount>,
        //IHandle<UpdateChatReadInbox>,
        //IHandle<UpdateSuggestedActions>,
        //IHandle<UpdateServiceNotification>,
        //IHandle<UpdateTermsOfService>,
        //IHandle<UpdateAuthorizationState>,
        //IHandle<UpdateUser>,
        //IHandle<UpdateNotification>,
        //IHandle<UpdateNotificationGroup>,
        //IHandle<UpdateHavePendingNotifications>,
        //IHandle<UpdateActiveNotifications>
    {
        private readonly IClientService _clientService;
        private readonly ISessionService _sessionService;
        private readonly ISettingsService _settings;
        private readonly IEventAggregator _aggregator;

        private readonly DisposableMutex _registrationLock;
        private bool _alreadyRegistered;

        private readonly bool? _suppress;

        public NotificationsService(IClientService clientService, ISettingsService settingsService, ISessionService sessionService, IEventAggregator aggregator)
        {
            _clientService = clientService;
            _settings = settingsService;
            _sessionService = sessionService;
            _aggregator = aggregator;

            _registrationLock = new DisposableMutex();

            Subscribe();

            var unreadCount = _clientService.GetUnreadCount(new ChatListMain());
            Handle(unreadCount.UnreadChatCount);
            Handle(unreadCount.UnreadMessageCount);
        }

        private void Subscribe()
        {
            _aggregator.Subscribe<UpdateUnreadMessageCount>(this, Handle)
                .Subscribe<UpdateUnreadChatCount>(Handle)
                .Subscribe<UpdateChatReadInbox>(Handle)
                .Subscribe<UpdateSuggestedActions>(Handle)
                .Subscribe<UpdateServiceNotification>(Handle)
                .Subscribe<UpdateTermsOfService>(Handle)
                .Subscribe<UpdateAuthorizationState>(Handle)
                .Subscribe<UpdateUser>(Handle)
                .Subscribe<UpdateNotification>(Handle)
                .Subscribe<UpdateNotificationGroup>(Handle)
                .Subscribe<UpdateHavePendingNotifications>(Handle)
                .Subscribe<UpdateActiveNotifications>(Handle);
        }

        private void UpdateBadge(int count)
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

        public async void Handle(UpdateTermsOfService update)
        {
            var terms = update.TermsOfService;
            if (terms == null)
            {
                return;
            }

            if (terms.ShowPopup)
            {
                async void DeleteAccount()
                {
                    var decline = await MessagePopup.ShowAsync(Strings.Resources.TosUpdateDecline, Strings.Resources.TermsOfService, Strings.Resources.DeclineDeactivate, Strings.Resources.Back);
                    if (decline != ContentDialogResult.Primary)
                    {
                        Handle(update);
                        return;
                    }

                    var delete = await MessagePopup.ShowAsync(Strings.Resources.TosDeclineDeleteAccount, Strings.Resources.AppName, Strings.Resources.Deactivate, Strings.Resources.Cancel);
                    if (delete != ContentDialogResult.Primary)
                    {
                        Handle(update);
                        return;
                    }

                    _clientService.Send(new DeleteAccount("Decline ToS update", string.Empty));
                }

                await Task.Delay(2000);
                BeginOnUIThread(async () =>
                {
                    var confirm = await MessagePopup.ShowAsync(terms.Text, Strings.Resources.PrivacyPolicyAndTerms, Strings.Resources.Agree, Strings.Resources.Cancel);
                    if (confirm != ContentDialogResult.Primary)
                    {
                        DeleteAccount();
                        return;
                    }

                    if (terms.MinUserAge > 0)
                    {
                        var age = await MessagePopup.ShowAsync(string.Format(Strings.Resources.TosAgeText, terms.MinUserAge), Strings.Resources.TosAgeTitle, Strings.Resources.Agree, Strings.Resources.Cancel);
                        if (age != ContentDialogResult.Primary)
                        {
                            DeleteAccount();
                            return;
                        }
                    }

                    _clientService.Send(new AcceptTermsOfService(update.TermsOfServiceId));
                });
            }
        }

        public void Handle(UpdateSuggestedActions update)
        {
            BeginOnUIThread(async () =>
            {
                foreach (var action in update.AddedActions)
                {
                    if (action is SuggestedActionEnableArchiveAndMuteNewChats)
                    {
                        var confirm = await MessagePopup.ShowAsync(Strings.Resources.HideNewChatsAlertText, Strings.Resources.HideNewChatsAlertTitle, Strings.Resources.OK, Strings.Resources.Cancel);
                        if (confirm == ContentDialogResult.Primary)
                        {
                            _clientService.Options.ArchiveAndMuteNewChatsFromUnknownUsers = true;
                        }

                        _clientService.Send(new HideSuggestedAction(action));
                    }
                }
            });
        }

        public void Handle(UpdateServiceNotification update)
        {
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

            BeginOnUIThread(async () =>
            {
                if (update.Type.StartsWith("AUTH_KEY_DROP_"))
                {
                    var confirm = await MessagePopup.ShowAsync(text, Strings.Resources.AppName, Strings.Resources.LogOut, Strings.Resources.Cancel);
                    if (confirm == ContentDialogResult.Primary)
                    {
                        _clientService.Send(new Destroy());
                    }
                }
                else
                {
                    await MessagePopup.ShowAsync(text, Strings.Resources.AppName, Strings.Resources.OK);
                }
            });
        }

        public async void Handle(UpdateChatReadInbox update)
        {
            if (update.UnreadCount == 0)
            {
                var chat = _clientService.GetChat(update.ChatId);
                if (chat == null)
                {
                    return;
                }

                try
                {
                    // Notifications APIs like to crash
                    var collectionHistory = await GetCollectionHistoryAsync();
                    collectionHistory.RemoveGroup(GetGroup(chat));
                }
                catch { }
            }
        }

        public async void Handle(UpdateUnreadMessageCount update)
        {
            if (!_settings.Notifications.CountUnreadMessages || !_sessionService.IsActive)
            {
                return;
            }

            if (update.ChatList is ChatListMain)
            {
                if (_settings.Notifications.IncludeMutedChats)
                {
                    UpdateBadge(update.UnreadCount);
                }
                else
                {
                    UpdateBadge(update.UnreadUnmutedCount);
                }

                if (App.Connection is AppServiceConnection connection)
                {
                    await connection.SendMessageAsync(new ValueSet { { "UnreadCount", _settings.Notifications.IncludeMutedChats ? update.UnreadCount : 0 }, { "UnreadUnmutedCount", update.UnreadUnmutedCount } });
                }
            }
        }

        public async void Handle(UpdateUnreadChatCount update)
        {
            if (_settings.Notifications.CountUnreadMessages || !_sessionService.IsActive)
            {
                return;
            }

            if (update.ChatList is ChatListMain)
            {
                if (_settings.Notifications.IncludeMutedChats)
                {
                    UpdateBadge(update.UnreadCount);
                }
                else
                {
                    UpdateBadge(update.UnreadUnmutedCount);
                }

                if (App.Connection is AppServiceConnection connection)
                {
                    await connection.SendMessageAsync(new ValueSet { { "UnreadCount", _settings.Notifications.IncludeMutedChats ? update.UnreadCount : 0 }, { "UnreadUnmutedCount", update.UnreadUnmutedCount } });
                }
            }
        }

        public void Handle(UpdateUser update)
        {
            if (update.User.Id == _clientService.Options.MyId)
            {
                CreateToastCollection(update.User);
            }
        }

        public void PlaySound()
        {
            if (!_settings.Notifications.InAppSounds)
            {
                return;
            }

            Task.Run(() => SoundEffects.Play(SoundEffect.Sent));
        }

        public async void Handle(UpdateActiveNotifications update)
        {
            try
            {
                var manager = await GetCollectionHistoryAsync();
                var history = manager.GetHistory();

                var hash = new HashSet<string>();

                foreach (var item in history)
                {
                    hash.Add($"{item.Tag}_{item.Group}");
                }

                foreach (var group in update.Groups)
                {
                    foreach (var notification in group.Notifications)
                    {
                        if (hash.Contains($"{notification.Id}_{group.Id}"))
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
            if (_suppress == true)
            {
                // This is an unsynced message, we don't want to show a notification for it as it has been probably pushed already by WNS
                return;
            }

            var connectionState = _clientService.GetConnectionState();
            if (connectionState is ConnectionStateUpdating)
            {
                // This is an unsynced message, we don't want to show a notification for it as it has been probably pushed already by WNS
                return;
            }

            if (!_sessionService.IsActive && !SettingsService.Current.IsAllAccountsNotifications)
            {
                return;
            }

            try
            {
                var collectionHistory = await GetCollectionHistoryAsync();
                foreach (var removed in update.RemovedNotificationIds)
                {
                    collectionHistory.Remove($"{removed}", $"{update.NotificationGroupId}");
                }
            }
            catch { }

            foreach (var notification in update.AddedNotifications)
            {
                ProcessNotification(update.NotificationGroupId, update.NotificationSoundId, update.ChatId, notification);
                //_clientService.Send(new RemoveNotification(update.NotificationGroupId, notification.Id));
            }
        }

        public void Handle(UpdateNotification update)
        {
            var connectionState = _clientService.GetConnectionState();
            if (connectionState is ConnectionStateUpdating)
            {
                // This is an unsynced message, we don't want to show a notification for it as it has been probably pushed already by WNS
                return;
            }

            //ProcessNotification(update.NotificationGroupId, 0, update.Notification);
        }

        private void ProcessNotification(int group, long soundId, long chatId, Telegram.Td.Api.Notification notification)
        {
            switch (notification.Type)
            {
                case NotificationTypeNewCall:
                    break;
                case NotificationTypeNewMessage newMessage:
                    ProcessNewMessage(group, notification.Id, newMessage.Message, notification.Date, soundId, notification.IsSilent);
                    break;
                case NotificationTypeNewPushMessage newPushMessage:
                    ProcessNewPushMessage(group, notification.Id, chatId, newPushMessage, notification.Date, soundId, notification.IsSilent);
                    break;
                case NotificationTypeNewSecretChat:
                    break;
            }
        }

        private async void ProcessNewPushMessage(int groupId, int id, long chatId, NotificationTypeNewPushMessage message, int date, long soundId, bool silent)
        {
            var chat = _clientService.GetChat(chatId);
            if (chat == null)
            {
                return;
            }

            var caption = GetCaption(chat);
            var content = GetContent(chat, message);
            var sound = silent ? "silent" : string.Empty;
            var launch = GetLaunch(chat, message);
            var picture = GetPhoto(chat);
            var dateTime = Converter.DateTime(date).ToUniversalTime().ToString("s") + "Z";
            var canReply = !(chat.Type is ChatTypeSupergroup super && super.IsChannel);

            if (soundId != 0 && !silent)
            {
                var response = await _clientService.SendAsync(new GetSavedNotificationSound(soundId));
                if (response is NotificationSound notificationSound)
                {
                    if (notificationSound.Sound.Local.IsDownloadingCompleted)
                    {
                        sound = "ms-appdata:///local/" + Path.GetRelativePath(ApplicationData.Current.LocalFolder.Path, notificationSound.Sound.Local.Path);
                    }
                    else
                    {
                        _clientService.DownloadFile(notificationSound.Sound.Id, 32);
                    }
                }
            }

            var user = _clientService.GetUser(_clientService.Options.MyId);
            var attribution = user?.FullName() ?? string.Empty;

            var showPreview = _settings.Notifications.GetShowPreview(chat);

            if (chat.Type is ChatTypeSecret || !showPreview || TLContainer.Current.Passcode.IsLockscreenRequired)
            {
                caption = Strings.Resources.AppName;
                content = Strings.Resources.YouHaveNewMessage;
                picture = string.Empty;

                canReply = false;
            }

            await UpdateAsync(chat, async () =>
            {
                await UpdateToast(caption, content, $"{_sessionService.Id}", sound, launch, $"{id}", $"{groupId}", picture, dateTime, canReply);
            });
        }

        private async void ProcessNewMessage(int groupId, int id, Message message, int date, long soundId, bool silent)
        {
            var chat = _clientService.GetChat(message.ChatId);
            if (chat == null)
            {
                return;
            }

            var caption = GetCaption(chat);
            var content = GetContent(chat, message);
            var sound = silent ? "silent" : string.Empty;
            var launch = GetLaunch(chat, message);
            var picture = GetPhoto(chat);
            var dateTime = Converter.DateTime(date).ToUniversalTime().ToString("s") + "Z";
            var canReply = !(chat.Type is ChatTypeSupergroup super && super.IsChannel);

            if (soundId != 0 && !silent)
            {
                var response = await _clientService.SendAsync(new GetSavedNotificationSound(soundId));
                if (response is NotificationSound notificationSound)
                {
                    if (notificationSound.Sound.Local.IsDownloadingCompleted)
                    {
                        sound = "ms-appdata:///local/" + Path.GetRelativePath(ApplicationData.Current.LocalFolder.Path, notificationSound.Sound.Local.Path);
                    }
                    else
                    {
                        _clientService.DownloadFile(notificationSound.Sound.Id, 32);
                    }
                }
            }

            var user = _clientService.GetUser(_clientService.Options.MyId);
            var attribution = user?.FullName() ?? string.Empty;

            var showPreview = _settings.Notifications.GetShowPreview(chat);

            if (chat.Type is ChatTypeSecret || !showPreview || TLContainer.Current.Passcode.IsLockscreenRequired)
            {
                caption = Strings.Resources.AppName;
                content = Strings.Resources.YouHaveNewMessage;
                picture = string.Empty;

                canReply = false;
            }

            await UpdateAsync(chat, () => UpdateToast(caption, content, $"{_sessionService.Id}", sound, launch, $"{id}", $"{groupId}", picture, dateTime, canReply));

            if (App.Connection is AppServiceConnection connection && _settings.Notifications.InAppFlash)
            {
                await connection.SendMessageAsync(new ValueSet { { "FlashWindow", string.Empty } });
            }
        }

        private async Task UpdateAsync(Chat chat, Func<Task> action)
        {
            try
            {
                var active = TLWindowContext.ActiveWindow;
                if (active == null)
                {
                    await action();
                    return;
                }

                var service = active.NavigationServices?.GetByFrameId($"Main{_clientService.SessionId}");
                if (service == null)
                {
                    await action();
                    return;
                }

                if (service.CurrentPageType == typeof(ChatPage) && (long)service.CurrentPageParam == chat.Id)
                {
                    return;
                }

                await action();
            }
            catch
            {
                await action();
            }
        }

        private async Task UpdateToast(string caption, string message, string account, string sound, string launch, string tag, string group, string picture, string date, bool canReply)
        {
            string actions = "";
            if (!string.IsNullOrEmpty(group) && canReply)
            {
                actions = string.Format("<actions><input id='input' type='text' placeHolderContent='{0}' /><action activationType='background' arguments='action=markAsRead&amp;", Strings.Resources.Reply);
                actions += launch;
                //actions += L"' hint-inputId='QuickMessage' content='ms-resource:Send' imageUri='ms-appx:///Assets/Icons/Toast/Send.png'/></actions>";
                actions += string.Format("' content='{0}'/><action activationType='background' arguments='action=reply&amp;", Strings.Resources.MarkAsRead);
                actions += launch;
                actions += string.Format("' hint-inputId='input' content='{0}'/></actions>", Strings.Resources.Send);
            }

            string audio = "";
            if (string.Equals(sound, "silent", StringComparison.OrdinalIgnoreCase))
            {
                audio = "<audio silent='true'/>";
            }

            string xml = "<toast launch='";
            xml += launch;
            xml += "' displayTimestamp='";
            xml += date;
            //xml += L"' hint-people='remoteid:";
            //xml += group->Data();
            xml += "'>";
            //xml += L"<header id='";
            //xml += account->Data();
            //xml += L"' title='Camping!!' arguments='action = openConversation & amp; id = 6289'/>";
            xml += "<visual><binding template='ToastGeneric'>";

            if (!string.IsNullOrEmpty(picture))
            {
                xml += "<image placement='appLogoOverride' hint-crop='circle' src='";
                xml += picture;
                xml += "'/>";
            }

            xml += "<text><![CDATA[";
            xml += caption;
            xml += "]]></text><text><![CDATA[";
            xml += message;
            //xml += L"]]></text><text placement='attribution'>Unigram</text></binding></visual>";
            xml += "]]></text>";

            //xml += L"<text placement='attribution'><![CDATA[";
            //xml += attribution->Data();
            //xml += L"]]></text>";
            xml += "</binding></visual>";
            xml += actions;
            xml += audio;
            xml += "</toast>";

            try
            {
                //auto notifier = ToastNotificationManager::CreateToastNotifier(L"App");
                ToastNotifier notifier = await ToastNotificationManager.GetDefault().GetToastNotifierForToastCollectionIdAsync(account);

                if (notifier == null)
                {
                    notifier = ToastNotificationManager.CreateToastNotifier("App");
                }

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
                    notification.Group = group;

                    if (!string.IsNullOrEmpty(group))
                    {
                        notification.RemoteId += "_";
                    }

                    notification.RemoteId += group;
                }

                notifier.Show(notification);
            }
            catch (Exception) { }
        }


        private string GetTag(Message message)
        {
            return $"{message.Id >> 20}";
        }

        private string GetGroup(Chat chat)
        {
            var group = string.Empty;
            if (chat.Type is ChatTypePrivate privata)
            {
                group = "u" + privata.UserId;
            }
            else if (chat.Type is ChatTypeSecret secret)
            {
                group = "s" + secret.SecretChatId;
            }
            else if (chat.Type is ChatTypeSupergroup supergroup)
            {
                group = "c" + supergroup.SupergroupId;
            }
            else if (chat.Type is ChatTypeBasicGroup basicGroup)
            {
                group = "c" + basicGroup.BasicGroupId;
            }

            return group;
        }

        public string GetLaunch(Chat chat, Message message)
        {
            var launch = string.Format(CultureInfo.InvariantCulture, "msg_id={0}", message.Id >> 20);

            if (chat.Type is ChatTypePrivate privata)
            {
                launch = string.Format(CultureInfo.InvariantCulture, "{0}&amp;from_id={1}", launch, privata.UserId);
            }
            else if (chat.Type is ChatTypeSecret secret)
            {
                launch = string.Format(CultureInfo.InvariantCulture, "{0}&amp;secret_id={1}", launch, secret.SecretChatId);
            }
            else if (chat.Type is ChatTypeSupergroup supergroup)
            {
                launch = string.Format(CultureInfo.InvariantCulture, "{0}&amp;channel_id={1}", launch, supergroup.SupergroupId);
            }
            else if (chat.Type is ChatTypeBasicGroup basicGroup)
            {
                launch = string.Format(CultureInfo.InvariantCulture, "{0}&amp;chat_id={1}", launch, basicGroup.BasicGroupId);
            }

            return string.Format(CultureInfo.InvariantCulture, "{0}&amp;session={1}", launch, _clientService.SessionId);
        }

        public string GetLaunch(Chat chat, NotificationTypeNewPushMessage message)
        {
            var launch = string.Format(CultureInfo.InvariantCulture, "msg_id={0}", message.MessageId >> 20);

            if (chat.Type is ChatTypePrivate privata)
            {
                launch = string.Format(CultureInfo.InvariantCulture, "{0}&amp;from_id={1}", launch, privata.UserId);
            }
            else if (chat.Type is ChatTypeSecret secret)
            {
                launch = string.Format(CultureInfo.InvariantCulture, "{0}&amp;secret_id={1}", launch, secret.SecretChatId);
            }
            else if (chat.Type is ChatTypeSupergroup supergroup)
            {
                launch = string.Format(CultureInfo.InvariantCulture, "{0}&amp;channel_id={1}", launch, supergroup.SupergroupId);
            }
            else if (chat.Type is ChatTypeBasicGroup basicGroup)
            {
                launch = string.Format(CultureInfo.InvariantCulture, "{0}&amp;chat_id={1}", launch, basicGroup.BasicGroupId);
            }

            return string.Format(CultureInfo.InvariantCulture, "{0}&amp;session={1}", launch, _clientService.SessionId);
        }

        public void Register()
        {
            var userId = _clientService.Options.MyId;
            if (userId == 0)
            {
                return;
            }

            if (_alreadyRegistered)
            {
                return;
            }

            _alreadyRegistered = true;

            try
            {
                if (_settings.PushToken != null)
                {
                    _clientService.Send(new RegisterDevice(new DeviceTokenWindowsPush(string.Empty), new long[0]));
                    _settings.PushReceiverId = 0;
                    _settings.PushToken = null;
                }
            }
            catch (Exception)
            {
                _alreadyRegistered = false;
                _settings.PushToken = null;
            }
        }

        private async void CreateToastCollection(User user)
        {
            try
            {
                var displayName = user.FullName();
                var launchArg = $"session={_sessionService.Id}&user_id={user.Id}";
                var icon = new Uri("ms-appx:///Assets/Logos/Square44x44Logo.png");

#if DEBUG
                displayName += " BETA";
#endif

                var collection = new ToastCollection($"{_sessionService.Id}", displayName, launchArg, icon);
                await ToastNotificationManager.GetDefault().GetToastCollectionManager().SaveToastCollectionAsync(collection);
            }
            catch { }
        }

        private async Task<ToastNotificationHistory> GetCollectionHistoryAsync()
        {
            try
            {
                var collectionHistory = await ToastNotificationManager.GetDefault().GetHistoryForToastCollectionIdAsync($"{_sessionService.Id}");
                if (collectionHistory == null)
                {
                    collectionHistory = ToastNotificationManager.History;
                }

                return collectionHistory;
            }
            catch
            {
                return ToastNotificationManager.History;
            }
        }

        public async Task CloseAsync()
        {
            try
            {
                var channel = await PushNotificationChannelManager.CreatePushNotificationChannelForApplicationAsync();
                channel.Close();
            }
            catch (Exception)
            {
                Debugger.Break();
            }
        }

        private readonly TaskCompletionSource<AuthorizationState> _authorizationStateTask = new TaskCompletionSource<AuthorizationState>();

        public void Handle(UpdateAuthorizationState update)
        {
            switch (update.AuthorizationState)
            {
                case AuthorizationStateWaitTdlibParameters:
                    break;
                default:
                    _authorizationStateTask.TrySetResult(update.AuthorizationState);
                    break;
            }
        }

        public async Task ProcessAsync(Dictionary<string, string> data)
        {
            var state = _clientService.GetAuthorizationState();
            if (state is not AuthorizationStateReady)
            {
                state = await _authorizationStateTask.Task;
            }

            if (state is not AuthorizationStateReady)
            {
                return;
            }

            if (data.TryGetValue("action", out string action))
            {
                var chat = default(Chat);
                if (data.TryGetValue("from_id", out string from_id) && int.TryParse(from_id, out int fromId))
                {
                    chat = await _clientService.SendAsync(new CreatePrivateChat(fromId, false)) as Chat;
                }
                else if (data.TryGetValue("channel_id", out string channel_id) && int.TryParse(channel_id, out int channelId))
                {
                    chat = await _clientService.SendAsync(new CreateSupergroupChat(channelId, false)) as Chat;
                }
                else if (data.TryGetValue("chat_id", out string chat_id) && int.TryParse(chat_id, out int chatId))
                {
                    chat = await _clientService.SendAsync(new CreateBasicGroupChat(chatId, false)) as Chat;
                }

                if (chat == null)
                {
                    return;
                }

                if (string.Equals(action, "reply", StringComparison.OrdinalIgnoreCase) && data.TryGetValue("input", out string text))
                {
                    var messageText = text.Replace("\r\n", "\n").Replace('\v', '\n').Replace('\r', '\n');
                    var formatted = Client.Execute(new ParseMarkdown(new FormattedText(messageText, new TextEntity[0]))) as FormattedText;

                    var replyToMsgId = data.ContainsKey("msg_id") ? long.Parse(data["msg_id"]) << 20 : 0;
                    var response = await _clientService.SendAsync(new SendMessage(chat.Id, 0, replyToMsgId, new MessageSendOptions(false, true, false, false, null), null, new InputMessageText(formatted, false, false)));

                    if (chat.Type is ChatTypePrivate && chat.LastMessage != null)
                    {
                        await _clientService.SendAsync(new ViewMessages(chat.Id, 0, new long[] { chat.LastMessage.Id }, true));
                    }
                }
                else if (string.Equals(action, "markasread", StringComparison.OrdinalIgnoreCase))
                {
                    if (chat.LastMessage == null)
                    {
                        return;
                    }

                    await _clientService.SendAsync(new ViewMessages(chat.Id, 0, new long[] { chat.LastMessage.Id }, true));
                }
            }
        }

















        private string GetCaption(Chat chat)
        {
            if (chat.Type is ChatTypeSecret)
            {
                return Strings.Resources.AppName;
            }

            return _clientService.GetTitle(chat);
        }

        private string GetContent(Chat chat, Message message)
        {
            if (chat.Type is ChatTypeSecret)
            {
                return Strings.Resources.YouHaveNewMessage;
            }

            return UpdateFromLabel(chat, message) + GetBriefLabel(chat, message);
        }

        private string GetContent(Chat chat, NotificationTypeNewPushMessage message)
        {
            if (chat.Type is ChatTypeSecret)
            {
                return Strings.Resources.YouHaveNewMessage;
            }

            return UpdateFromLabel(chat, message) + GetBriefLabel(chat, message);
        }

        private string GetPhoto(Chat chat)
        {
            if (chat.Photo != null && chat.Photo.Small.Local.IsDownloadingCompleted)
            {
                var relative = Path.GetRelativePath(ApplicationData.Current.LocalFolder.Path, chat.Photo.Small.Local.Path);
                return "ms-appdata:///local/" + relative.Replace('\\', '/');
            }

            return string.Empty;
        }



        private string GetBriefLabel(Chat chat, Message value)
        {
            switch (value.Content)
            {
                case MessageAnimation animation:
                    return animation.Caption.Text;
                case MessageAudio audio:
                    return audio.Caption.Text;
                case MessageDocument document:
                    return document.Caption.Text;
                case MessagePhoto photo:
                    return photo.Caption.Text;
                case MessageVideo video:
                    return video.Caption.Text;
                case MessageVoiceNote voiceNote:
                    return voiceNote.Caption.Text;

                case MessageText text:
                    return text.Text.Text;

                case MessageAnimatedEmoji animatedEmoji:
                    return animatedEmoji.Emoji;
                case MessageDice dice:
                    return dice.Emoji;
            }

            return string.Empty;
        }

        private string GetBriefLabel(Chat chat, NotificationTypeNewPushMessage value)
        {
            switch (value.Content)
            {
                case PushMessageContentAnimation animation:
                    return animation.Caption;
                case PushMessageContentPhoto photo:
                    return photo.Caption;
                case PushMessageContentVideo video:
                    return video.Caption;

                case PushMessageContentText text:
                    return text.Text;
            }

            return string.Empty;
        }

        private string UpdateFromLabel(Chat chat, Message message)
        {
            if (message.IsService())
            {
                return MessageService.GetText(new ViewModels.MessageViewModel(_clientService, null, null, message));
            }

            var result = string.Empty;

            if (ShowFrom(_clientService, chat, message, out User from))
            {
                if (!string.IsNullOrEmpty(from.FirstName))
                {
                    result = $"{from.FirstName.Trim()}: ";
                }
                else if (!string.IsNullOrEmpty(from.LastName))
                {
                    result = $"{from.LastName.Trim()}: ";
                }
                else if (from.Type is UserTypeDeleted)
                {
                    result = $"{Strings.Resources.HiddenName}: ";
                }
                else
                {
                    result = $"{from.Id}: ";
                }
            }

            if (message.Content is MessageGame gameMedia)
            {
                return result + "\uD83C\uDFAE " + gameMedia.Game.Title;
            }
            if (message.Content is MessageExpiredVideo)
            {
                return result + Strings.Resources.AttachVideoExpired;
            }
            else if (message.Content is MessageExpiredPhoto)
            {
                return result + Strings.Resources.AttachPhotoExpired;
            }
            else if (message.Content is MessageVideoNote)
            {
                return result + Strings.Resources.AttachRound;
            }
            else if (message.Content is MessageSticker sticker)
            {
                if (string.IsNullOrEmpty(sticker.Sticker.Emoji))
                {
                    return result + Strings.Resources.AttachSticker;
                }

                return result + $"{sticker.Sticker.Emoji} {Strings.Resources.AttachSticker}";
            }

            string GetCaption(string caption)
            {
                return string.IsNullOrEmpty(caption) ? string.Empty : ", ";
            }

            if (message.Content is MessageVoiceNote voiceNote)
            {
                return result + Strings.Resources.AttachAudio + GetCaption(voiceNote.Caption.Text);
            }
            else if (message.Content is MessageVideo video)
            {
                return result + (video.IsSecret ? Strings.Resources.AttachDestructingVideo : Strings.Resources.AttachVideo) + GetCaption(video.Caption.Text);
            }
            else if (message.Content is MessageAnimation animation)
            {
                return result + Strings.Resources.AttachGif + GetCaption(animation.Caption.Text);
            }
            else if (message.Content is MessageAudio audio)
            {
                var performer = string.IsNullOrEmpty(audio.Audio.Performer) ? null : audio.Audio.Performer;
                var title = string.IsNullOrEmpty(audio.Audio.Title) ? null : audio.Audio.Title;

                if (performer == null && title == null)
                {
                    return result + Strings.Resources.AttachMusic + GetCaption(audio.Caption.Text);
                }
                else
                {
                    return $"{result}{performer ?? Strings.Resources.AudioUnknownArtist} - {title ?? Strings.Resources.AudioUnknownTitle}" + GetCaption(audio.Caption.Text);
                }
            }
            else if (message.Content is MessageDocument document)
            {
                if (string.IsNullOrEmpty(document.Document.FileName))
                {
                    return result + Strings.Resources.AttachDocument + GetCaption(document.Caption.Text);
                }

                return result + document.Document.FileName + GetCaption(document.Caption.Text);
            }
            else if (message.Content is MessageInvoice invoice)
            {
                return result + invoice.Title;
            }
            else if (message.Content is MessageContact)
            {
                return result + Strings.Resources.AttachContact;
            }
            else if (message.Content is MessageLocation location)
            {
                return result + (location.LivePeriod > 0 ? Strings.Resources.AttachLiveLocation : Strings.Resources.AttachLocation);
            }
            else if (message.Content is MessageVenue)
            {
                return result + Strings.Resources.AttachLocation;
            }
            else if (message.Content is MessagePhoto photo)
            {
                return result + (photo.IsSecret ? Strings.Resources.AttachDestructingPhoto : Strings.Resources.AttachPhoto) + GetCaption(photo.Caption.Text);
            }
            else if (message.Content is MessagePoll poll)
            {
                return result + "\uD83D\uDCCA " + poll.Poll.Question;
            }
            else if (message.Content is MessageCall call)
            {
                return result + call.ToOutcomeText(message.IsOutgoing);
            }
            else if (message.Content is MessageUnsupported)
            {
                return result + Strings.Resources.UnsupportedAttachment;
            }

            return result;
        }

        private string UpdateFromLabel(Chat chat, NotificationTypeNewPushMessage message)
        {
            //if (message.IsService())
            //{
            //return MessageService.GetText(new ViewModels.MessageViewModel(_clientService, null, null, message));
            //}

            var result = string.Empty;

            if (_clientService.TryGetUser(message.SenderId, out User senderUser))
            {
                if (ShowFrom(chat))
                {
                    if (!string.IsNullOrEmpty(senderUser.FirstName))
                    {
                        result = $"{senderUser.FirstName.Trim()}: ";
                    }
                    else if (!string.IsNullOrEmpty(senderUser.LastName))
                    {
                        result = $"{senderUser.LastName.Trim()}: ";
                    }
                    else if (senderUser.Type is UserTypeDeleted)
                    {
                        result = $"{Strings.Resources.HiddenName}: ";
                    }
                    else
                    {
                        result = $"{senderUser.Id}: ";
                    }
                }
            }
            else if (ShowFrom(chat))
            {
                result = $"{message.SenderName}: ";
            }

            string FormatPinned(string key)
            {
                if (chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel)
                {
                    return string.Format(key, string.Empty).Trim(' ');
                }

                return string.Format(key, message.SenderName);
            }

            if (message.Content is PushMessageContentGame gameMedia)
            {
                if (gameMedia.IsPinned)
                {
                    return FormatPinned(Strings.Resources.NotificationActionPinnedGameChannel);
                }

                return result + "\uD83C\uDFAE " + gameMedia.Title;
            }
            else if (message.Content is PushMessageContentVideoNote videoNote)
            {
                if (videoNote.IsPinned)
                {
                    return FormatPinned(Strings.Resources.NotificationActionPinnedRoundChannel);
                }

                return result + Strings.Resources.AttachRound;
            }
            else if (message.Content is PushMessageContentSticker sticker)
            {
                if (sticker.IsPinned)
                {
                    if (string.IsNullOrEmpty(sticker.Sticker?.Emoji))
                    {
                        return FormatPinned(Strings.Resources.NotificationActionPinnedStickerChannel);
                    }

                    return FormatPinned(string.Format(Strings.Resources.NotificationActionPinnedStickerEmojiChannel, "{0}", sticker.Emoji));
                }

                if (string.IsNullOrEmpty(sticker.Sticker?.Emoji))
                {
                    return result + Strings.Resources.AttachSticker;
                }

                return result + $"{sticker.Sticker.Emoji} {Strings.Resources.AttachSticker}";
            }

            string GetCaption(string caption)
            {
                return string.IsNullOrEmpty(caption) ? string.Empty : $", {caption}";
            }

            if (message.Content is PushMessageContentVoiceNote voiceNote)
            {
                if (voiceNote.IsPinned)
                {
                    return FormatPinned(Strings.Resources.NotificationActionPinnedVoiceChannel);
                }

                return result + Strings.Resources.AttachAudio;
            }
            else if (message.Content is PushMessageContentVideo video)
            {
                if (video.IsPinned)
                {
                    return FormatPinned(Strings.Resources.NotificationActionPinnedVideoChannel);
                }

                return result + (video.IsSecret ? Strings.Resources.AttachDestructingVideo : Strings.Resources.AttachVideo) + GetCaption(video.Caption);
            }
            else if (message.Content is PushMessageContentAnimation animation)
            {
                if (animation.IsPinned)
                {
                    return FormatPinned(Strings.Resources.NotificationActionPinnedGifChannel);
                }

                return result + Strings.Resources.AttachGif + GetCaption(animation.Caption);
            }
            else if (message.Content is PushMessageContentAudio audio)
            {
                if (audio.IsPinned)
                {
                    return FormatPinned(Strings.Resources.NotificationActionPinnedMusicChannel);
                }

                var performer = string.IsNullOrEmpty(audio.Audio?.Performer) ? null : audio.Audio.Performer;
                var title = string.IsNullOrEmpty(audio.Audio?.Title) ? null : audio.Audio.Title;

                if (performer == null || title == null)
                {
                    return result + Strings.Resources.AttachMusic;
                }
                else
                {
                    return $"{result}{performer} - {title}";
                }
            }
            else if (message.Content is PushMessageContentDocument document)
            {
                if (document.IsPinned)
                {
                    return FormatPinned(Strings.Resources.NotificationActionPinnedFileChannel);
                }

                if (string.IsNullOrEmpty(document.Document?.FileName))
                {
                    return result + Strings.Resources.AttachDocument;
                }

                return result + document.Document.FileName;
            }
            else if (message.Content is PushMessageContentInvoice invoice)
            {
                if (invoice.IsPinned)
                {
                    return FormatPinned(Strings.Resources.NotificationActionPinnedInvoiceChannel);
                }

                return result + invoice.Price;
            }
            else if (message.Content is PushMessageContentContact contact)
            {
                if (contact.IsPinned)
                {
                    return FormatPinned(Strings.Resources.NotificationActionPinnedContactChannel);
                }

                return result + Strings.Resources.AttachContact;
            }
            else if (message.Content is PushMessageContentLocation location)
            {
                if (location.IsPinned)
                {
                    return FormatPinned(location.IsLive ? Strings.Resources.NotificationActionPinnedGeoLiveChannel : Strings.Resources.NotificationActionPinnedGeoChannel);
                }

                return result + (location.IsLive ? Strings.Resources.AttachLiveLocation : Strings.Resources.AttachLocation);
            }
            else if (message.Content is PushMessageContentPhoto photo)
            {
                if (photo.IsPinned)
                {
                    return FormatPinned(Strings.Resources.NotificationActionPinnedPhotoChannel);
                }

                return result + (photo.IsSecret ? Strings.Resources.AttachDestructingPhoto : Strings.Resources.AttachPhoto) + GetCaption(photo.Caption);
            }
            else if (message.Content is PushMessageContentPoll poll)
            {
                if (poll.IsPinned)
                {
                    return FormatPinned(Strings.Resources.NotificationActionPinnedPollChannel);
                }

                return result + "\uD83D\uDCCA " + poll.Question;
            }
            // Service messages
            else if (message.Content is PushMessageContentBasicGroupChatCreate)
            {
                return Strings.Resources.NotificationInvitedToGroup;
            }
            else if (message.Content is PushMessageContentChatAddMembers)
            {

            }
            else if (message.Content is PushMessageContentChatChangePhoto)
            {
                if (chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel)
                {
                    return Strings.Resources.ActionChannelChangedPhoto;
                }

                //Strings.Resources.NotificationMessageAlbum
                //return Strings.Resources.ActionChangedPhoto.Replace("un1", from.GetFullName());
            }
            else if (message.Content is PushMessageContentChatChangeTitle chatChangeTitle)
            {
                if (chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel)
                {
                    return Strings.Resources.ActionChannelChangedTitle.Replace("un2", chatChangeTitle.Title);
                }

                //return Strings.Resources.ActionChangedTitle.Replace("un1", from.GetFullName()).Replace("un2", chatChangeTitle.Title);
            }
            else if (message.Content is PushMessageContentChatDeleteMember)
            {

            }
            else if (message.Content is PushMessageContentChatJoinByLink)
            {
                //return Strings.Resources.ActionInviteUser.Replace("un1", from.GetFullName());
            }
            else if (message.Content is PushMessageContentContactRegistered)
            {
                //return string.Format(Strings.Resources.NotificationContactJoined, from.GetFullName());
            }
            else if (message.Content is PushMessageContentGameScore)
            {
                return Strings.Resources.NotificationMessageGame;
            }
            else if (message.Content is PushMessageContentHidden)
            {
                return Strings.Resources.YouHaveNewMessage;
            }
            else if (message.Content is PushMessageContentMediaAlbum)
            {
                return Strings.Resources.NotificationMessageAlbum;
            }
            else if (message.Content is PushMessageContentMessageForwards)
            {
                //return string.Format(Strings.Resources.NotificationMessageForwardFew, Locale.Declension("messages", forwards.TotalCount));
            }
            else if (message.Content is PushMessageContentScreenshotTaken)
            {
                return Strings.Resources.ActionTakeScreenshoot;
            }

            return result;
        }

        private bool ShowFrom(IClientService clientService, Chat chat, Message message, out User senderUser)
        {
            if (message.IsService())
            {
                senderUser = null;
                return false;
            }

            if (message.IsOutgoing)
            {
                return clientService.TryGetUser(message.SenderId, out senderUser);
            }

            if (chat.Type is ChatTypeBasicGroup)
            {
                return clientService.TryGetUser(message.SenderId, out senderUser);
            }

            if (chat.Type is ChatTypeSupergroup supergroup)
            {
                senderUser = null;
                return !supergroup.IsChannel && clientService.TryGetUser(message.SenderId, out senderUser);
            }

            senderUser = null;
            return false;
        }

        private bool ShowFrom(Chat chat)
        {
            if (chat.Type is ChatTypeBasicGroup)
            {
                return true;
            }

            if (chat.Type is ChatTypeSupergroup supergroup)
            {
                return !supergroup.IsChannel;
            }

            return false;
        }

        private void BeginOnUIThread(Windows.System.DispatcherQueueHandler action, Action fallback = null)
        {
            var dispatcher = WindowContext.Default()?.Dispatcher;
            if (dispatcher != null)
            {
                dispatcher.Dispatch(action);
            }
            else if (fallback != null)
            {
                fallback();
            }
            else
            {
                //try
                //{
                //    action();
                //}
                //catch { }
            }
        }



        public void SetMuteFor(Chat chat, int muteFor)
        {
            if (_settings.Notifications.TryGetScope(chat, out ScopeNotificationSettings scope))
            {
                var settings = chat.NotificationSettings;

                var useDefault = muteFor == scope.MuteFor || muteFor > 366 * 24 * 60 * 60 && scope.MuteFor > 366 * 24 * 60 * 60;
                if (useDefault)
                {
                    muteFor = scope.MuteFor;
                }

                _clientService.Send(new SetChatNotificationSettings(chat.Id,
                    new ChatNotificationSettings(
                        useDefault, muteFor,
                        settings.UseDefaultSound, settings.SoundId,
                        settings.UseDefaultShowPreview, settings.ShowPreview,
                        settings.UseDefaultDisablePinnedMessageNotifications, settings.DisablePinnedMessageNotifications,
                        settings.UseDefaultDisableMentionNotifications, settings.DisableMentionNotifications)));
            }
        }
    }
}
