//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Td;
using Telegram.Td.Api;
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

        void PlaySound();

        #region Chats related

        void SetMuteFor(Chat chat, int muteFor, XamlRoot xamlRoot);

        #endregion
    }

    public partial class NotificationsService : INotificationsService
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

        private readonly DebouncedProperty<int> _unreadCount;

        private readonly bool? _suppress;

        public NotificationsService(IClientService clientService, ISettingsService settingsService, ISessionService sessionService, IEventAggregator aggregator)
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

        private void Subscribe()
        {
            _aggregator.Subscribe<UpdateUnreadMessageCount>(this, Handle)
                .Subscribe<UpdateUnreadChatCount>(Handle)
                .Subscribe<UpdateChatReadInbox>(Handle)
                .Subscribe<UpdateSuggestedActions>(Handle)
                .Subscribe<UpdateServiceNotification>(Handle)
                .Subscribe<UpdateTermsOfService>(Handle)
                .Subscribe<UpdateUser>(Handle)
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
                    var xamlRoot = window.Content?.XamlRoot;
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

            await ViewService.WaitForMainWindowAsync();

            var window = WindowContext.Active ?? WindowContext.Main;
            var dispatcher = window?.Dispatcher;

            dispatcher?.Dispatch(async () =>
            {
                foreach (var action in update.AddedActions)
                {
                    var xamlRoot = window.Content?.XamlRoot;
                    if (xamlRoot == null)
                    {
                        return;
                    }

                    if (action is SuggestedActionEnableArchiveAndMuteNewChats)
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

                        _clientService.Send(new HideSuggestedAction(action));
                    }
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
                var xamlRoot = window.Content?.XamlRoot;
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

                NotifyIcon.SendUnreadCount(unreadCount, unreadUnmutedCount);
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
            try
            {
                var collectionHistory = await GetCollectionHistoryAsync();
                foreach (var removed in update.RemovedNotificationIds)
                {
                    collectionHistory.Remove($"{removed}", $"{update.NotificationGroupId}");
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
                ProcessNotification(update.NotificationGroupId, update.NotificationSoundId, update.ChatId, notification);
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

        private void ProcessNotification(int group, long soundId, long chatId, Td.Api.Notification notification)
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
                    ProcessNewMessage(group, notification.Id, newMessage.Message, time, soundId, notification.IsSilent);
                    break;
                case NotificationTypeNewSecretChat:
                    break;
            }
        }

        private async void ProcessNewMessage(int groupId, int id, Message message, DateTime date, long soundId, bool silent)
        {
            var chat = _clientService.GetChat(message.ChatId);
            if (chat == null)
            {
                Logger.Info("Chat is null");
                return;
            }

            if (UpdateAsync(chat))
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

                if (chat.Type is ChatTypeSecret || !showPreview || TypeResolver.Current.Passcode.IsLockscreenRequired)
                {
                    caption = Strings.AppName;
                    content = Strings.YouHaveNewMessage;
                    picture = string.Empty;

                    canReply = false;
                }

                await UpdateToast(caption, content, $"{_sessionService.Id}", silent, silent || soundId == 0, soundFile, launch, $"{id}", $"{groupId}", picture, dateTime, canReply);
            }
        }

        private bool UpdateAsync(Chat chat)
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

                if (service.CurrentPageType == typeof(ChatPage) && (long)service.CurrentPageParam == chat.Id)
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

        private async Task UpdateToast(string caption, string message, string account, bool suppressPopup, bool silent, Td.Api.File soundFile, string launch, string tag, string group, string picture, string date, bool canReply)
        {
            var xml = $"<toast launch='{launch}' displayTimestamp='{date}'>";
            xml += "<visual><binding template='ToastGeneric'>";

            if (!string.IsNullOrEmpty(picture))
            {
                xml += $"<image placement='appLogoOverride' hint-crop='circle' src='{picture}'/>";
            }

            xml += $"<text><![CDATA[{caption}]]></text><text><![CDATA[{message}]]></text>";
            xml += "</binding></visual>";

            if (!string.IsNullOrEmpty(group) && canReply)
            {
                xml += string.Format("<actions><input id='input' type='text' placeHolderContent='{0}' /><action activationType='background' arguments='action=markAsRead&amp;", Strings.Reply);
                xml += launch;
                xml += string.Format("' content='{0}'/><action activationType='background' arguments='action=reply&amp;", Strings.MarkAsRead);
                xml += launch;
                xml += string.Format("' hint-inputId='input' content='{0}'/></actions>", Strings.Send);
            }

            if (silent || soundFile != null)
            {
                xml += "<audio silent='true'/>";
            }

            xml += "</toast>";

            try
            {
                ToastNotifier notifier = await ToastNotificationManager
                    .GetDefault()
                    .GetToastNotifierForToastCollectionIdAsync(account);

                notifier ??= ToastNotificationManager.CreateToastNotifier("App");

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
                    notification.RemoteId += "_";
                    notification.RemoteId += group;
                }

                notification.SuppressPopup = suppressPopup;

                notifier.Show(notification);

                if (soundFile != null && notifier.Setting == NotificationSetting.Enabled)
                {
                    SoundEffects.Play(soundFile);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
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
            var launch = string.Format(CultureInfo.InvariantCulture, "chat_id={0}", chat.Id);
            launch = string.Format(CultureInfo.InvariantCulture, "{0}&amp;session={1}", launch, _clientService.SessionId);

            if (chat.Type is not ChatTypePrivate and not ChatTypeSecret)
            {
                launch = string.Format(CultureInfo.InvariantCulture, "{0}&amp;msg_id={1}", launch, message.Id);
            }

            return launch;
        }

        private async void CreateToastCollection(User user)
        {
            try
            {
                var displayName = user.FullName();
                var launchArg = $"session={_sessionService.Id}&user_id={user.Id}";
                var icon = new Uri("ms-appx:///Assets/Logos/Square44x44Logo.png");

                if (Constants.DEBUG)
                {
                    displayName += " BETA";
                }

                var collection = new ToastCollection($"{_sessionService.Id}", displayName, launchArg, icon);
                await ToastNotificationManager.GetDefault().GetToastCollectionManager().SaveToastCollectionAsync(collection);
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
        }

        private async Task<ToastNotificationHistory> GetCollectionHistoryAsync()
        {
            try
            {
                var collectionHistory = await ToastNotificationManager.GetDefault().GetHistoryForToastCollectionIdAsync($"{_sessionService.Id}");
                collectionHistory ??= ToastNotificationManager.History;

                return collectionHistory;
            }
            catch
            {
                return ToastNotificationManager.History;
            }
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
                    if (_clientService.TryGetChat(chatId, out chat))
                    {

                    }
                    else
                    {
                        chat = await _clientService.SendAsync(new GetChat(chatId)) as Chat;
                    }
                }

                if (chat == null)
                {
                    return;
                }

                if (string.Equals(action, "reply", StringComparison.OrdinalIgnoreCase) && data.TryGetValue("input", out string text))
                {
                    var messageText = text.Replace("\r\n", "\n").Replace('\v', '\n').Replace('\r', '\n');
                    var formatted = ClientEx.ParseMarkdown(messageText);

                    var replyToMsgId = data.ContainsKey("msg_id") ? new InputMessageReplyToMessage(long.Parse(data["msg_id"]), null) : null;
                    var response = await _clientService.SendAsync(new SendMessage(chat.Id, 0, replyToMsgId, new MessageSendOptions(false, true, false, false, null, 0, 0, false), null, new InputMessageText(formatted, null, false)));

                    if (chat.Type is ChatTypePrivate && chat.LastMessage != null)
                    {
                        await _clientService.SendAsync(new ViewMessages(chat.Id, new long[] { chat.LastMessage.Id }, new MessageSourceNotification(), true));
                    }
                }
                else if (string.Equals(action, "markasread", StringComparison.OrdinalIgnoreCase))
                {
                    if (chat.LastMessage == null)
                    {
                        return;
                    }

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

            var brief = ChatCell.UpdateBriefLabel(chat, message.Content, message.IsOutgoing, false, true, out _);
            var clean = brief.ReplaceSpoilers();

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

        public void SetMuteFor(Chat chat, int value, XamlRoot xamlRoot)
        {
            if (_settings.Notifications.TryGetScope(chat, out ScopeNotificationSettings scope))
            {
                var settings = chat.NotificationSettings.Clone();

                var useDefault = value == scope.MuteFor || value > 366 * 24 * 60 * 60 && scope.MuteFor > 366 * 24 * 60 * 60;
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
                else if (value >= 632053052)
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
