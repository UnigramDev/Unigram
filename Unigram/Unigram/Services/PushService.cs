using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Template10.Common;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Native.Tasks;
using Unigram.Views;
using Windows.ApplicationModel;
using Windows.Data.Json;
using Windows.Networking.PushNotifications;

namespace Unigram.Core.Services
{
    public interface IPushService
    {
        Task RegisterAsync();
        Task UnregisterAsync();
        string GetGroup(ITLDialogWith with);
        string GetPicture(ITLDialogWith with, string group);
        string GetTitle(ITLDialogWith with);
        string GetLaunch(ITLDialogWith with);

        void Notify(TLMessageCommonBase commonMessage);
    }

    public class PushService : IPushService
    {
        private readonly IMTProtoService _protoService;
        private readonly ICacheService _cacheService;

        private readonly DisposableMutex _registrationLock;
        private bool _alreadyRegistered;

        public PushService(IMTProtoService protoService, ICacheService cacheService)
        {
            _protoService = protoService;
            _cacheService = cacheService;

            _registrationLock = new DisposableMutex();
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

                        var result = await _protoService.RegisterDeviceAsync(8, channel.Uri);
                        if (result.IsSucceeded)
                        {
                            SettingsHelper.ChannelUri = channel.Uri;
                        }

                        if (Uri.TryCreate(oldUri, UriKind.Absolute, out Uri unregister))
                        {
                            await _protoService.UnregisterDeviceAsync(8, oldUri);
                        }
                    }

                    channel.PushNotificationReceived += OnPushNotificationReceived;
                }
                catch (Exception ex)
                {
                    _alreadyRegistered = false;
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

                        TLPeerBase peer = null;
                        if (custom.ContainsKey("chat_id") && int.TryParse(custom.GetNamedString("chat_id"), out int chat_id))
                        {
                            peer = new TLPeerChat { ChatId = chat_id };
                        }
                        else if (custom.ContainsKey("channel_id") && int.TryParse(custom.GetNamedString("channel_id"), out int channel_id))
                        {
                            peer = new TLPeerChannel { ChannelId = channel_id };
                        }
                        else if (custom.ContainsKey("from_id") && int.TryParse(custom.GetNamedString("from_id"), out int from_id))
                        {
                            peer = new TLPeerUser { UserId = from_id };
                        }
                        else if (custom.ContainsKey("contact_id") && int.TryParse(custom.GetNamedString("contact_id"), out int contact_id))
                        {
                            peer = new TLPeerUser { UserId = contact_id };
                        }

                        if (peer == null)
                        {
                            return;
                        }

                        var service = WindowWrapper.Current().NavigationServices.GetByFrameId("Main");
                        if (service == null)
                        {
                            return;
                        }

                        Execute.BeginOnUIThread(() =>
                        {
                            if (service.Frame.Content is DialogPage page && peer.Equals(service.CurrentPageParam))
                            {
                                if (!page.ViewModel.IsActive || !App.IsActive || !App.IsVisible)
                                {
                                    return;
                                }

                                args.Cancel = true;
                            }
                        });
                    }
                }
            }
        }

        public async Task UnregisterAsync()
        {
            var channel = SettingsHelper.ChannelUri;
            var response = await _protoService.UnregisterDeviceAsync(8, channel);
            if (response.IsSucceeded)
            {
            }

            SettingsHelper.ChannelUri = null;
        }

        public void Notify(TLMessageCommonBase commonMessage)
        {
            var caption = commonMessage.Parent.DisplayName;
            var content = GetFromLabel(commonMessage) + GetBriefLabel(commonMessage, true);
            var sound = "";
            var launch = GetLaunch(commonMessage);
            var tag = GetTag(commonMessage);
            var group = GetGroup(commonMessage);
            var picture = GetPicture(commonMessage, group);
            var date = BindConvert.Current.DateTime(commonMessage.Date).ToString("o");
            var loc_key = commonMessage.Parent is TLChannel channel && channel.IsBroadcast ? "CHANNEL" : string.Empty;

            NotificationTask.UpdateToast(caption, content, sound, launch, tag, group, picture, date, loc_key);
            NotificationTask.UpdatePrimaryTile(caption, content, picture);
        }

        private string GetLaunch(TLMessageCommonBase custom)
        {
            return GetLaunch(custom?.Parent);

            //launch += L"Action=";
            //launch += loc_key->Data();
        }

        public string GetLaunch(ITLDialogWith with)
        {
            var launch = string.Empty;

            if (with is TLChat chat)
            {
                launch += string.Format(CultureInfo.InvariantCulture, "chat_id={0}", chat.Id);
            }
            else if (with is TLChannel channel)
            {
                launch += string.Format(CultureInfo.InvariantCulture, "channel_id={0}&amp;access_hash={1}", channel.Id, channel.AccessHash ?? 0);
            }
            else if (with is TLUser user)
            {
                launch += string.Format(CultureInfo.InvariantCulture, "from_id={0}&amp;access_hash={1}", user.Id, user.AccessHash ?? 0);
            }

            return launch;
        }

        private string GetTag(TLMessageCommonBase custom)
        {
            return custom.Id.ToString(CultureInfo.InvariantCulture);
        }

        private string GetGroup(TLMessageCommonBase custom)
        {
            return GetGroup(custom?.Parent);
        }

        public string GetGroup(ITLDialogWith with)
        {
            if (with == null)
            {
                return null;
            }

            if (with is TLChat chat)
            {
                return string.Format(CultureInfo.InvariantCulture, "c{0}", chat.Id);
            }
            else if (with is TLChannel channel)
            {
                return string.Format(CultureInfo.InvariantCulture, "c{0}", channel.Id);
            }
            else if (with is TLUser user)
            {
                return string.Format(CultureInfo.InvariantCulture, "u{0}", user.Id);
            }

            return null;
        }

        private string GetPicture(TLMessageCommonBase custom, string group)
        {
            return GetPicture(custom?.Parent, group);
        }

        public string GetPicture(ITLDialogWith with, string group)
        {
            TLFileLocation location = null;
            if (with is TLUser user && user.Photo is TLUserProfilePhoto userPhoto)
            {
                location = userPhoto.PhotoSmall as TLFileLocation;
            }
            else if (with is TLChat chat && chat.Photo is TLChatPhoto chatPhoto)
            {
                location = chatPhoto.PhotoSmall as TLFileLocation;
            }
            else if (with is TLChannel channel && channel.Photo is TLChatPhoto channelPhoto)
            {
                location = channelPhoto.PhotoSmall as TLFileLocation;
            }

            if (location != null)
            {
                var fileName = string.Format("{0}_{1}_{2}.jpg", location.VolumeId, location.LocalId, location.Secret);
                if (File.Exists(FileUtils.GetTempFileName(fileName)))
                {
                    return FileUtils.GetTempFileUri(fileName).ToString();
                }
            }

            return FileUtils.GetTempFileUri("placeholders/" + group + "_placeholder.png").ToString();
        }

        public string GetTitle(ITLDialogWith with)
        {
            if (with == null)
            {
                return null;
            }

            if (with is TLChat chat)
            {
                return chat.Title;
            }
            else if (with is TLChannel channel)
            {
                return channel.Title;
            }
            else if (with is TLUser user)
            {
                return user.DisplayName;
            }

            return null;
        }

        #region Brief

        private string GetBriefLabel(TLMessageBase value, bool showContent)
        {
            if (value is TLMessageEmpty messageEmpty)
            {
                return string.Empty;
            }

            if (value is TLMessageService messageService)
            {
                return string.Empty;
            }

            if (value is TLMessage message)
            {
                var result = string.Empty;
                if (message.Media != null)
                {
                    if (message.Media is TLMessageMediaDocument documentMedia)
                    {
                        if (string.IsNullOrEmpty(documentMedia.Caption) || message.IsRoundVideo())
                        {
                            return result;
                        }

                        return result + documentMedia.Caption.Replace("\r\n", "\n").Replace("\n", " ");
                    }
                    else if (message.Media is TLMessageMediaPhoto photoMedia)
                    {
                        if (string.IsNullOrEmpty(photoMedia.Caption))
                        {
                            return result;
                        }

                        return result + photoMedia.Caption.Replace("\r\n", "\n").Replace("\n", " ");
                    }
                    else if (message.Media is TLMessageMediaVenue venueMedia)
                    {
                        return result + venueMedia.Title;
                    }
                    else if (message.Media is TLMessageMediaGame || message.Media is TLMessageMediaGeoLive)
                    {
                        return string.Empty;
                    }
                }

                if (message.Message != null)
                {
                    if (showContent)
                    {
                        return result + message.Message.Replace("\r\n", "\n").Replace("\n", " ");
                    }

                    return result + Strings.Android.Message;
                }
            }

            return string.Empty;
        }

        private string GetFromLabel(TLMessageCommonBase commonMessage)
        {
            if (commonMessage is TLMessage message)
            {
                var result = string.Empty;

                if (message.ShowFrom)
                {
                    var from = message.FromId;
                    if (from != null)
                    {
                        if (message.IsOut)
                        {
                            if (message.Parent.ToPeer().Id != from && !message.IsPost)
                            {
                                result = $"{Strings.Android.FromYou}: ";
                            }
                        }
                        else if (message.From is TLUser user)
                        {
                            if (user.HasFirstName)
                            {
                                result = $"{user.FirstName.Trim()}: ";
                            }
                            else if (user.HasLastName)
                            {
                                result = $"{user.LastName.Trim()}: ";
                            }
                            else if (user.HasUsername)
                            {
                                result = $"{user.Username.Trim()}: ";
                            }
                            else if (user.IsDeleted)
                            {
                                result = $"{Strings.Android.HiddenName}: ";
                            }
                            else
                            {
                                result = $"{user.Id}: ";
                            }
                        }
                    }
                }

                if (message.State == TLMessageState.Failed && message.IsOut)
                {
                    result = "Failed: ";
                }

                if (message.Media != null)
                {
                    if (message.Media is TLMessageMediaGame gameMedia)
                    {
                        return result + "\uD83C\uDFAE " + gameMedia.Game.Title;
                    }
                    else if (message.Media is TLMessageMediaDocument documentMedia)
                    {
                        if (documentMedia.HasTTLSeconds && (documentMedia.Document is TLDocumentEmpty || !documentMedia.HasDocument))
                        {
                            return result + Strings.Android.AttachVideoExpired;
                        }
                        else if (message.IsRoundVideo())
                        {
                            return result + Strings.Android.AttachRound;
                        }
                        else if (message.IsSticker())
                        {
                            if (documentMedia.Document is TLDocument documentSticker)
                            {
                                var attribute = documentSticker.Attributes.OfType<TLDocumentAttributeSticker>().FirstOrDefault();
                                if (attribute != null)
                                {
                                    return result + $"{attribute.Alt} {Strings.Android.AttachSticker}";
                                }
                            }

                            return result + Strings.Android.AttachSticker;
                        }

                        var caption = string.Empty;
                        if (!string.IsNullOrEmpty(documentMedia.Caption))
                        {
                            caption = ", ";
                        }

                        if (message.IsVoice())
                        {
                            return result + Strings.Android.AttachAudio + caption;
                        }
                        else if (message.IsVideo())
                        {
                            return result + Strings.Android.AttachVideo + caption;
                        }
                        else if (message.IsGif())
                        {
                            return result + Strings.Android.AttachGif + caption;
                        }
                        else if (message.IsAudio())
                        {
                            if (documentMedia.Document is TLDocument documentAudio)
                            {
                                var audioAttribute = documentAudio.Attributes.OfType<TLDocumentAttributeAudio>().FirstOrDefault();
                                if (audioAttribute != null)
                                {
                                    if (audioAttribute.HasPerformer && audioAttribute.HasTitle)
                                    {
                                        return $"{result}{audioAttribute.Performer} - {audioAttribute.Title}" + caption;
                                    }
                                    else if (audioAttribute.HasPerformer && !audioAttribute.HasTitle)
                                    {
                                        return $"{result}{audioAttribute.Performer} - {Strings.Android.AudioUnknownTitle}" + caption;
                                    }
                                    else if (audioAttribute.HasTitle && !audioAttribute.HasPerformer)
                                    {
                                        return $"{result}{Strings.Android.AudioUnknownArtist} - {audioAttribute.Title}" + caption;
                                    }
                                }
                            }
                        }

                        if (documentMedia.Document is TLDocument document)
                        {
                            var attribute = document.Attributes.OfType<TLDocumentAttributeFilename>().FirstOrDefault();
                            if (attribute != null)
                            {
                                //return $"{text}{attribute.Alt} ({Resources.Sticker.ToLower()})";
                                return result + document.FileName + caption;
                            }
                        }

                        return result + Strings.Android.AttachDocument + caption;
                    }
                    else if (message.Media is TLMessageMediaInvoice invoiceMedia)
                    {
                        return result + invoiceMedia.Title;
                    }
                    else if (message.Media is TLMessageMediaContact)
                    {
                        return result + Strings.Android.AttachContact;
                    }
                    else if (message.Media is TLMessageMediaGeo)
                    {
                        return result + Strings.Android.AttachLocation;
                    }
                    else if (message.Media is TLMessageMediaGeoLive)
                    {
                        return result + Strings.Android.AttachLiveLocation;
                    }
                    else if (message.Media is TLMessageMediaVenue)
                    {
                        return result + $"{Strings.Android.AttachLocation}, ";
                    }
                    else if (message.Media is TLMessageMediaPhoto photoMedia)
                    {
                        if (photoMedia.HasTTLSeconds && (photoMedia.Photo is TLPhotoEmpty || !photoMedia.HasPhoto))
                        {
                            return result + Strings.Android.AttachPhotoExpired;
                        }

                        if (string.IsNullOrEmpty(photoMedia.Caption))
                        {
                            return result + Strings.Android.AttachPhoto;
                        }

                        return result + $"{Strings.Android.AttachPhoto}, ";
                    }
                    else if (message.Media is TLMessageMediaUnsupported)
                    {
                        return result + Strings.Android.UnsupportedAttachment;
                    }
                }

                return result;
            }
            else if (commonMessage is TLMessageService messageService)
            {
                return ServiceHelper.Convert(messageService);
            }

            return string.Empty;
        }

        #endregion

    }
}
