using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Template10.Common;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Messages;
using Unigram.Converters;
using Unigram.Native.Tasks;
using Unigram.Services;
using Unigram.Views;
using Windows.ApplicationModel;
using Windows.Data.Json;
using Windows.Networking.PushNotifications;
using Windows.System.Threading;

namespace Unigram.Services
{
    public interface INotificationsService
    {
        Task RegisterAsync();
        Task UnregisterAsync();
        Task CloseAsync();
    }

    public class NotificationsService : INotificationsService, IHandle<UpdateUnreadMessageCount>, IHandle<UpdateNewMessage>, IHandle<UpdateServiceNotification>
    {
        private readonly IProtoService _protoService;
        private readonly ICacheService _cacheService;
        private readonly ISettingsService _settings;
        private readonly IEventAggregator _aggregator;

        private readonly DisposableMutex _registrationLock;
        private bool _alreadyRegistered;

        public NotificationsService(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
        {
            _protoService = protoService;
            _cacheService = cacheService;
            _settings = settingsService;
            _aggregator = aggregator;

            _registrationLock = new DisposableMutex();

            _aggregator.Subscribe(this);

            Handle(new UpdateUnreadMessageCount(protoService.UnreadCount, protoService.UnreadUnmutedCount));
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

            Execute.BeginOnUIThread(async () =>
            {
                await TLMessageDialog.ShowAsync(text, Strings.Resources.AppName, Strings.Resources.OK);
            });
        }

        public void Handle(UpdateUnreadMessageCount update)
        {
            if (_settings.Notifications.IncludeMutedChats)
            {
                NotificationTask.UpdatePrimaryBadge(update.UnreadCount);
            }
            else
            {
                NotificationTask.UpdatePrimaryBadge(update.UnreadUnmutedCount);
            }
        }

        public void Handle(UpdateNewMessage update)
        {
            if (update.DisableNotification || !_settings.Notifications.InAppPreview)
            {
                return;
            }

            var difference = DateTime.Now.ToTimestamp() - update.Message.Date;
            if (difference > 180)
            {
                return;
            }

            // Adding some delay to be 110% the message hasn't been read already
            ThreadPoolTimer.CreateTimer(timer =>
            {
                var chat = _protoService.GetChat(update.Message.ChatId);
                if (chat == null || chat.LastReadInboxMessageId >= update.Message.Id)
                {
                    return;
                }

                var caption = GetCaption(chat);
                var content = GetContent(chat, update.Message);
                var sound = "";
                var launch = GetLaunch(chat);
                var tag = GetTag(update.Message);
                var group = GetGroup(update.Message, chat);
                var picture = GetPhoto(chat);
                var date = BindConvert.Current.DateTime(update.Message.Date).ToString("o");
                var loc_key = chat.Type is ChatTypeSupergroup super && super.IsChannel ? "CHANNEL" : string.Empty;

                Execute.BeginOnUIThread(() =>
                {
                    var service = WindowWrapper.Current().NavigationServices.GetByFrameId("Main");
                    if (service == null)
                    {
                        return;
                    }

                    if (WindowContext.GetForCurrentView().ActivationState != Windows.UI.Core.CoreWindowActivationState.Deactivated && service.CurrentPageType == typeof(ChatPage) && (long)service.CurrentPageParam == chat.Id)
                    {
                        return;
                    }

                    NotificationTask.UpdateToast(caption, content, sound, launch, tag, group, picture, date, loc_key);
                    NotificationTask.UpdatePrimaryTile(caption, content, picture);
                });
            }, TimeSpan.FromSeconds(3));
        }

        private string GetTag(Message message)
        {
            return (message.Id << 20).ToString();
        }

        private string GetGroup(Message message, Chat chat)
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

        public string GetLaunch(Chat chat)
        {
            var launch = string.Empty;
            if (chat.Type is ChatTypePrivate privata)
            {
                launch += string.Format(CultureInfo.InvariantCulture, "from_id={0}", privata.UserId);
            }
            else if (chat.Type is ChatTypeSecret secret)
            {
                launch += string.Format(CultureInfo.InvariantCulture, "secret_id={0}", secret.SecretChatId);
            }
            else if (chat.Type is ChatTypeSupergroup supergroup)
            {
                launch += string.Format(CultureInfo.InvariantCulture, "channel_id={0}", supergroup.SupergroupId);
            }
            else if (chat.Type is ChatTypeBasicGroup basicGroup)
            {
                launch += string.Format(CultureInfo.InvariantCulture, "chat_id={0}", basicGroup.BasicGroupId);
            }

            return launch;
        }

        public async Task RegisterAsync()
        {
            using (await _registrationLock.WaitAsync())
            {
                if (_alreadyRegistered) return;
                _alreadyRegistered = true;

                try
                {
                    var oldUri = ApplicationSettings.Current.NotificationsToken;

                    var channel = await PushNotificationChannelManager.CreatePushNotificationChannelForApplicationAsync();
                    if (channel.Uri != oldUri)
                    {
                        var result = await _protoService.SendAsync(new RegisterDevice(new DeviceTokenWindowsPush(channel.Uri), new int[0]));
                        if (result is Ok)
                        {
                            _settings.NotificationsToken = channel.Uri;
                        }
                        else
                        {
                            _settings.NotificationsToken = null;
                        }
                    }

                    channel.PushNotificationReceived += OnPushNotificationReceived;
                }
                catch (Exception ex)
                {
                    _alreadyRegistered = false;
                    _settings.NotificationsToken = null;

                    Debugger.Break();
                }
            }
        }

        private void OnPushNotificationReceived(PushNotificationChannel sender, PushNotificationReceivedEventArgs args)
        {
            if (args.NotificationType == PushNotificationType.Raw)
            {
                args.Cancel = true;
                return;

                if (JsonValue.TryParse(args.RawNotification.Content, out JsonValue node))
                {
                    var notification = node.GetObject();
                    var data = notification.GetNamedObject("data");

                    if (data.ContainsKey("loc_key"))
                    {
                        var muted = data.GetNamedString("mute", "0") == "1";
                        if (muted)
                        {
                            return;
                        }

                        var custom = data.GetNamedObject("custom", null);
                        if (custom == null)
                        {
                            return;
                        }
                    }
                }
            }
        }

        public async Task UnregisterAsync()
        {
            var channel = _settings.NotificationsToken;
            //var response = await _protoService.UnregisterDeviceAsync(8, channel);
            //if (response.IsSucceeded)
            //{
            //}

            _settings.NotificationsToken = null;
        }

        public async Task CloseAsync()
        {
            try
            {
                var channel = await PushNotificationChannelManager.CreatePushNotificationChannelForApplicationAsync();
                channel.Close();
            }
            catch (Exception ex)
            {
                Debugger.Break();
            }
        }



















        private string GetCaption(Chat chat)
        {
            if (chat.Type is ChatTypeSecret)
            {
                return Strings.Resources.AppName;
            }

            return _protoService.GetTitle(chat);
        }

        private string GetContent(Chat chat, Message message)
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
                return "ms-appdata:///local/0/profile_photos/" + Path.GetFileName(chat.Photo.Small.Local.Path);
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
            }

            return string.Empty;
        }

        private string UpdateFromLabel(Chat chat, Message message)
        {
            if (message.IsService())
            {
                return MessageService.GetText(new ViewModels.MessageViewModel(_protoService, null, message));
            }

            var result = string.Empty;

            if (ShowFrom(chat, message))
            {
                var from = _protoService.GetUser(message.SenderUserId);
                if (from != null)
                {
                    if (!string.IsNullOrEmpty(from.FirstName))
                    {
                        result = $"{from.FirstName.Trim()}: ";
                    }
                    else if (!string.IsNullOrEmpty(from.LastName))
                    {
                        result = $"{from.LastName.Trim()}: ";
                    }
                    else if (!string.IsNullOrEmpty(from.Username))
                    {
                        result = $"{from.Username.Trim()}: ";
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
                return result + Strings.Resources.AttachVideo + GetCaption(video.Caption.Text);
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
            else if (message.Content is MessageVenue vanue)
            {
                return result + $"{Strings.Resources.AttachLocation}, ";
            }
            else if (message.Content is MessagePhoto photo)
            {
                if (string.IsNullOrEmpty(photo.Caption.Text))
                {
                    return result + Strings.Resources.AttachPhoto;
                }

                return result + $"{Strings.Resources.AttachPhoto}, ";
            }
            else if (message.Content is MessageUnsupported)
            {
                return result + Strings.Resources.UnsupportedAttachment;
            }

            return result;
        }

        private bool ShowFrom(Chat chat, Message message)
        {
            if (message.IsService())
            {
                return false;
            }

            if (message.IsOutgoing)
            {
                return true;
            }

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
    }
}
