using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TdWindows;
using Telegram.Api.Helpers;
using Template10.Common;
using Unigram.Common;
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
    }

    public class NotificationsService : INotificationsService, IHandle<UpdateNewMessage>
    {
        private readonly IProtoService _protoService;
        private readonly ICacheService _cacheService;
        private readonly IEventAggregator _aggregator;

        private readonly DisposableMutex _registrationLock;
        private bool _alreadyRegistered;

        public NotificationsService(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator)
        {
            _protoService = protoService;
            _cacheService = cacheService;
            _aggregator = aggregator;

            _registrationLock = new DisposableMutex();

            _aggregator.Subscribe(this);
        }

        public void Handle(UpdateNewMessage update)
        {
            if (update.DisableNotification)
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

                var caption = _protoService.GetTitle(chat);
                var content = UpdateFromLabel(chat, update.Message) + GetBriefLabel(update.Message);
                var sound = "";
                var launch = "GetLaunch(commonMessage)";
                var tag = update.Message.Id.ToString();
                var group = GetGroup(update.Message, chat);
                var picture = string.Empty;
                var date = BindConvert.Current.DateTime(update.Message.Date).ToString("o");
                var loc_key = "CHANNEL";
                //var loc_key = commonMessage.Parent is TLChannel channel && channel.IsBroadcast ? "CHANNEL" : string.Empty;

                Execute.BeginOnUIThread(() =>
                {
                    var service = WindowWrapper.Current().NavigationServices.GetByFrameId("Main");
                    if (service == null)
                    {
                        return;
                    }

                    if (WindowContext.GetForCurrentView().ActivationState != Windows.UI.Core.CoreWindowActivationState.Deactivated && service.CurrentPageType == typeof(DialogPage) && (long)service.CurrentPageParam == chat.Id)
                    {
                        return;
                    }

                    NotificationTask.UpdateToast(caption, content, sound, launch, tag, group, picture, date, loc_key);
                    NotificationTask.UpdatePrimaryTile(caption, content, picture);
                });
            }, TimeSpan.FromSeconds(2));
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

        public async Task RegisterAsync()
        {
            using (await _registrationLock.WaitAsync())
            {
                if (_alreadyRegistered) return;
                _alreadyRegistered = true;

                try
                {
                    var channel = await PushNotificationChannelManager.CreatePushNotificationChannelForApplicationAsync();
                    if (channel.Uri != SettingsHelper.ChannelUri)
                    {
                        var oldUri = SettingsHelper.ChannelUri;

                        var result = await _protoService.SendAsync(new RegisterDevice(new DeviceTokenWindowsPush(channel.Uri), new int[0]));
                        if (result is Ok)
                        {
                            SettingsHelper.ChannelUri = channel.Uri;
                        }
                        else
                        {
                            SettingsHelper.ChannelUri = null;
                        }
                    }

                    channel.PushNotificationReceived += OnPushNotificationReceived;
                }
                catch (Exception ex)
                {
                    _alreadyRegistered = false;
                    SettingsHelper.ChannelUri = null;

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
            var channel = SettingsHelper.ChannelUri;
            //var response = await _protoService.UnregisterDeviceAsync(8, channel);
            //if (response.IsSucceeded)
            //{
            //}

            SettingsHelper.ChannelUri = null;
        }























        private string GetBriefLabel(Message value)
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
                        result = $"{Strings.Android.HiddenName}: ";
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
                return result + Strings.Android.AttachVideoExpired;
            }
            else if (message.Content is MessageExpiredPhoto)
            {
                return result + Strings.Android.AttachPhotoExpired;
            }
            else if (message.Content is MessageVideoNote)
            {
                return result + Strings.Android.AttachRound;
            }
            else if (message.Content is MessageSticker sticker)
            {
                if (string.IsNullOrEmpty(sticker.Sticker.Emoji))
                {
                    return result + Strings.Android.AttachSticker;
                }

                return result + $"{sticker.Sticker.Emoji} {Strings.Android.AttachSticker}";
            }

            string GetCaption(string caption)
            {
                return string.IsNullOrEmpty(caption) ? string.Empty : ", ";
            }

            if (message.Content is MessageVoiceNote voiceNote)
            {
                return result + Strings.Android.AttachAudio + GetCaption(voiceNote.Caption.Text);
            }
            else if (message.Content is MessageVideo video)
            {
                return result + Strings.Android.AttachVideo + GetCaption(video.Caption.Text);
            }
            else if (message.Content is MessageAnimation animation)
            {
                return result + Strings.Android.AttachGif + GetCaption(animation.Caption.Text);
            }
            else if (message.Content is MessageAudio audio)
            {
                var performer = string.IsNullOrEmpty(audio.Audio.Performer) ? null : audio.Audio.Performer;
                var title = string.IsNullOrEmpty(audio.Audio.Title) ? null : audio.Audio.Title;

                if (performer == null && title == null)
                {
                    return result + Strings.Android.AttachMusic + GetCaption(audio.Caption.Text);
                }
                else
                {
                    return $"{result}{performer ?? Strings.Android.AudioUnknownArtist} - {title ?? Strings.Android.AudioUnknownTitle}" + GetCaption(audio.Caption.Text);
                }
            }
            else if (message.Content is MessageDocument document)
            {
                if (string.IsNullOrEmpty(document.Document.FileName))
                {
                    return result + Strings.Android.AttachDocument + GetCaption(document.Caption.Text);
                }

                return result + document.Document.FileName + GetCaption(document.Caption.Text);
            }
            else if (message.Content is MessageInvoice invoice)
            {
                return result + invoice.Title;
            }
            else if (message.Content is MessageContact)
            {
                return result + Strings.Android.AttachContact;
            }
            else if (message.Content is MessageLocation location)
            {
                return result + (location.LivePeriod > 0 ? Strings.Android.AttachLiveLocation : Strings.Android.AttachLocation);
            }
            else if (message.Content is MessageVenue vanue)
            {
                return result + $"{Strings.Android.AttachLocation}, ";
            }
            else if (message.Content is MessagePhoto photo)
            {
                if (string.IsNullOrEmpty(photo.Caption.Text))
                {
                    return result + Strings.Android.AttachPhoto;
                }

                return result + $"{Strings.Android.AttachPhoto}, ";
            }
            else if (message.Content is MessageUnsupported)
            {
                return result + Strings.Android.UnsupportedAttachment;
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
