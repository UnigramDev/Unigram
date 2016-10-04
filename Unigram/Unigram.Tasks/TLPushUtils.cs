using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Data.Xml.Dom;
using Windows.Networking.PushNotifications;
using Windows.UI.Notifications;

namespace Unigram.Tasks
{
    internal static class TLPushUtils
    {
        private static readonly Dictionary<string, string> _locKeys = new Dictionary<string, string>
        {
            { "MESSAGE_FWDS", "forwarded you {1} messages" },
            { "MESSAGE_TEXT", "{1}" },
            { "MESSAGE_NOTEXT", "sent you a message" },
            { "MESSAGE_PHOTO", "sent you a photo" },
            { "MESSAGE_VIDEO", "sent you a video" },
            { "MESSAGE_DOC", "sent you a document" },
            { "MESSAGE_AUDIO", "sent you a voice message" },
            { "MESSAGE_CONTACT", "shared a contact with you" },
            { "MESSAGE_GEO", "sent you a map" },
            { "MESSAGE_STICKER", "sent you a {1} sticker" },
            { "CHAT_MESSAGE_FWDS", "{0} forwarded {2} messages to the group" },
            { "CHAT_MESSAGE_TEXT", "{0}: {2}" },
            { "CHAT_MESSAGE_NOTEXT", "{0} sent a message to the group" },
            { "CHAT_MESSAGE_PHOTO", "{0} sent a photo to the group" },
            { "CHAT_MESSAGE_VIDEO", "{0} sent a video to the group" },
            { "CHAT_MESSAGE_DOC", "{0} sent a document to the group" },
            { "CHAT_MESSAGE_AUDIO", "{0} sent a voice message to the group" },
            { "CHAT_MESSAGE_CONTACT", "{0} shared a contact in the group" },
            { "CHAT_MESSAGE_GEO", "{0} sent a map to the group" },
            { "CHAT_MESSAGE_STICKER", "{0} sent a {2} sticker to the group" },
            { "CHAT_CREATED", "{0} invited you to the group" },
            { "CHAT_TITLE_EDITED", "{0} edited the group's name" },
            { "CHAT_PHOTO_EDITED", "{0} edited the group's photo" },
            { "CHAT_ADD_MEMBER", "{0} invited {2} to the group" },
            { "CHAT_ADD_YOU", "{0} invited you to the group" },
            { "CHAT_DELETE_MEMBER", "{0} kicked {2} from the group" },
            { "CHAT_DELETE_YOU", "{0} kicked you from the group" },
            { "CHAT_LEFT", "{0} has left the group" },
            { "CHAT_RETURNED", "{0} has returned to the group" },
            { "GEOCHAT_CHECKIN", "{0} has checked-in" },
            { "CONTACT_JOINED", "{0} joined the App!" },
            { "AUTH_UNKNOWN", "New login from unrecognized device {0}" },
            { "AUTH_REGION", "New login from unrecognized device {0}, location: {1}" },
            { "CONTACT_PHOTO", "updated profile photo" },
            { "ENCRYPTION_REQUEST", "You have a new message" },
            { "ENCRYPTION_ACCEPT", "You have a new message" },
            { "ENCRYPTED_MESSAGE", "You have a new message" },
            { "DC_UPDATE", "Open this notification to update app settings" }
        };

        public static void UpdateToastAndTiles(RawNotification rawNotification)
        {
            var payload = (rawNotification != null) ? rawNotification.Content : null;
            if (payload == null)
            {
                return;
            }

            var notification = GetRootObject(payload);
            if (notification == null)
            {
                return;
            }

            if (notification.data == null)
            {
                return;
            }

            if (notification.data.loc_key == null)
            {
                RemoveToastGroup(GetGroup(notification.data));
                return;
            }

            var caption = GetCaption(notification.data);
            var message = GetMessage(notification.data);
            var sound = GetSound(notification.data);
            var launch = GetLaunch(notification.data);
            var tag = GetTag(notification.data);
            var group = GetGroup(notification.data);

            if (!IsMuted(notification.data))
            {
                AddToast(caption, message, sound, launch, notification.data.custom, tag, group);
            }
            if (!IsMuted(notification.data) && !IsServiceNotification(notification.data))
            {
                UpdateTile(caption, message);
            }
            if (!IsMuted(notification.data))
            {
                UpdateBadge(notification.data.badge);
            }
        }

        private static bool IsMuted(TLPushData data)
        {
            return data.mute == "1";
        }

        private static bool IsServiceNotification(TLPushData data)
        {
            return data.loc_key == "DC_UPDATE";
        }

        public static TLPushNotification GetRootObject(string payload)
        {
            try
            {
                return JsonConvert.DeserializeObject<TLPushNotification>(payload);
            }
            catch { }

            return null;
        }

        private static string GetCaption(TLPushData data)
        {
            var locKey = data.loc_key;
            if (locKey == null)
            {
                return "locKey=null";
            }
            if (locKey.StartsWith("CHAT") || locKey.StartsWith("GEOCHAT"))
            {
                return data.loc_args[1];
            }
            if (locKey.StartsWith("MESSAGE"))
            {
                return data.loc_args[0];
            }
            if (locKey.StartsWith("AUTH") || locKey.StartsWith("CONTACT") || locKey.StartsWith("ENCRYPTION"))
            {
                return "Telegram";
            }

            return "Telegram";
        }

        private static string GetSound(TLPushData data)
        {
            return data.sound;
        }

        private static string GetGroup(TLPushData data)
        {
            return data.group;
        }

        private static string GetTag(TLPushData data)
        {
            return data.tag;
        }

        private static string GetLaunch(TLPushData data)
        {
            var loc_key = data.loc_key;
            if (loc_key == null)
            {
                return null;
            }

            var list = new List<string> { "Action=" + loc_key };
            list.AddRange(data.custom.GetParams());

            return string.Join("&", list);
        }

        private static string GetMessage(TLPushData data)
        {
            string loc_key = data.loc_key;
            if (loc_key == null)
            {
                return string.Empty;
            }

            string text;
            if (_locKeys.TryGetValue(loc_key, out text))
            {
                return string.Format(text, data.loc_args);
            }

            StringBuilder stringBuilder = new StringBuilder();
            if (data.loc_args != null)
            {
                stringBuilder.AppendLine("loc_args");
                string[] loc_args = data.loc_args;
                for (int i = 0; i < loc_args.Length; i++)
                {
                    string text2 = loc_args[i];
                    stringBuilder.AppendLine(text2);
                }
            }

            return string.Empty;
        }

        private static void UpdateBadge(int badgeNumber)
        {
            var updater = BadgeUpdateManager.CreateBadgeUpdaterForApplication();
            if (badgeNumber == 0)
            {
                updater.Clear();
                return;
            }

            var document = BadgeUpdateManager.GetTemplateContent(BadgeTemplateType.BadgeNumber);
            var element = (XmlElement)document.SelectSingleNode("/badge");
            element.SetAttribute("value", badgeNumber.ToString());

            try
            {
                updater.Update(new BadgeNotification(document));
            }
            catch { }
        }

        public static void AddToast(string caption, string message, string sound, string launch, TLPushCustom custom, string tag, string group)
        {
            var actions = string.Empty;
            if (custom?.from_id != null || custom?.channel_id != null || custom?.chat_id != null)
            {
                  //{string.Join("\r\n", custom.GetInputs())}
                actions = $@"
                <actions>
                  <input id='QuickMessage' type='text' placeHolderContent='Type a message...' />
                  <action activationType='background' arguments='{WebUtility.HtmlEncode(launch)}' hint-inputId='QuickMessage' content='Send' imageUri='ms-appx:///Assets/Icons/Toast/Send.png'/>
                </actions>";

            }

            var xml = $@"
                <toast launch='{WebUtility.HtmlEncode(launch)}'>
                    <visual>
                      <binding template='ToastGeneric'>
                        <image placement='appLogoOverride' hint-crop='circle' src='ms-appx:///Assets/Logos/Placeholder/Placeholder-2.png' />
                        <text>{WebUtility.HtmlEncode(caption) ?? string.Empty}</text>
                        <text>{WebUtility.HtmlEncode(message) ?? string.Empty}</text>
                        <text placement='attribution'>Unigram</text>
                      </binding>
                    </visual>
                    {actions}
               </toast>";

            var notifier = ToastNotificationManager.CreateToastNotifier();
            var document = new XmlDocument();
            document.LoadXml(xml);

            //SetText(document, caption, message);
            //SetLaunch(document, launch);

            //if (!string.IsNullOrEmpty(sound) && !string.Equals(sound, "default", StringComparison.OrdinalIgnoreCase))
            //{
            //    SetSound(document, sound);
            //}

            try
            {
                var notification = new ToastNotification(document);

                if (tag != null) notification.Tag = tag;
                if (group != null) notification.Group = group;

                notifier.Show(notification);
            }
            catch { }
        }

        private static void RemoveToastGroup(string groupname)
        {
            ToastNotificationManager.History.RemoveGroup(groupname);
        }

        private static void UpdateTile(string caption, string message)
        {
            var body = string.Empty;
            if (caption != null)
            {
                body += $"<text hint-style='body'>{WebUtility.HtmlEncode(caption)}</text>";
            }
            if (message != null)
            {
                body += $"<text hint-style='captionSubtle' hint-wrap='true'>{WebUtility.HtmlEncode(message)}</text>";
            }

            var xml = $@"
                <tile>
                    <visual>
                        <binding template='TileMedium' branding='nameAndLogo'>
                            {body}
                        </binding>
                        <binding template='TileWide' branding='nameAndLogo'>
                            {body}
                        </binding>
                        <binding template='TileLarge' branding='nameAndLogo'>
                            {body}
                        </binding>
                    </visual>
                </tile>";

            var updater = TileUpdateManager.CreateTileUpdaterForApplication();
            updater.EnableNotificationQueue(false);
            updater.EnableNotificationQueueForSquare150x150(false);

            var document = new XmlDocument();
            document.LoadXml(xml);

            //var tileWide = TileUpdateManager.GetTemplateContent(TileTemplateType.TileWide310x150IconWithBadgeAndText);
            //SetImage(tileWide, "IconicSmall110.png");
            //SetText(tileWide, caption, message);

            //var tileMedium = TileUpdateManager.GetTemplateContent(TileTemplateType.TileSquare150x150IconWithBadge);
            //SetImage(tileMedium, "IconicTileMedium202.png");
            //AppendTile(tileWide, tileMedium);

            //var tileSmall = TileUpdateManager.GetTemplateContent(TileTemplateType.TileSquare71x71IconWithBadge);
            //SetImage(tileSmall, "IconicSmall110.png");
            //AppendTile(tileWide, tileSmall);

            try
            {
                updater.Update(new TileNotification(document));
            }
            catch { }
        }

        private static void AppendTile(XmlDocument toTile, XmlDocument fromTile)
        {
            var binding = toTile.ImportNode(fromTile.GetElementsByTagName("binding").Item(0u), true);
            toTile.GetElementsByTagName("visual")[0].AppendChild(binding);
        }

        private static void SetText(XmlDocument document, string caption, string message)
        {
            var elements = document.GetElementsByTagName("text");
            elements[0].InnerText = caption ?? string.Empty;
            elements[1].InnerText = message ?? string.Empty;
        }

        private static void SetImage(XmlDocument document, string imageSource)
        {
            var elements = document.GetElementsByTagName("image");
            ((XmlElement)elements[0]).SetAttribute("src", imageSource);
        }

        private static void SetSound(XmlDocument document, string soundSource)
        {
            if (!Regex.IsMatch(soundSource, "^sound[1-6]$", RegexOptions.IgnoreCase | RegexOptions.Compiled))
            {
                return;
            }

            var toast = document.SelectSingleNode("/toast");
            ((XmlElement)toast).SetAttribute("duration", "long");

            var audio = document.CreateElement("audio");
            audio.SetAttribute("src", "ms-appx:///Sounds/" + soundSource + ".wav");
            audio.SetAttribute("loop", "false");
            toast.AppendChild(audio);
        }

        private static void SetLaunch(XmlDocument document, string launch)
        {
            if (string.IsNullOrEmpty(launch))
            {
                return;
            }

            var toast = document.SelectSingleNode("/toast");
            ((XmlElement)toast).SetAttribute("launch", launch);
        }
    }
}