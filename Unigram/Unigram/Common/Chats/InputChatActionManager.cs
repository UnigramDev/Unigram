using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Td.Api;

namespace Unigram.Common.Chats
{
    public class InputChatActionManager
    {
        public static string GetTypingString(Chat chat, IDictionary<int, ChatAction> typingUsers, Func<int, User> getUser, out ChatAction commonAction)
        {
            if (chat.Type is ChatTypePrivate || chat.Type is ChatTypeSecret)
            {
                var tuple = typingUsers.FirstOrDefault();
                //if (tuple != null)
                {
                    var action = tuple.Value;
                    switch (action)
                    {
                        //case TLSendMessageChooseContactAction chooseContact:
                        //    return "";
                        case ChatActionStartPlayingGame gamePlay:
                            commonAction = gamePlay;
                            return Strings.Resources.SendingGame;
                        //case TLSendMessageGeoLocationAction geoLocation:
                        //    return "";
                        case ChatActionRecordingVoiceNote recordAudio:
                            commonAction = recordAudio;
                            return Strings.Resources.RecordingAudio;
                        case ChatActionRecordingVideoNote recordRound:
                        case ChatActionUploadingVideoNote uploadRound:
                            commonAction = new ChatActionRecordingVideoNote();
                            return Strings.Resources.RecordingRound;
                        //case TLSendMessageTypingAction typing:
                        //    return Strings.Resources.Typing;
                        case ChatActionUploadingVoiceNote uploadAudio:
                            commonAction = uploadAudio;
                            return Strings.Resources.SendingAudio;
                        case ChatActionUploadingDocument uploadDocument:
                            commonAction = uploadDocument;
                            return Strings.Resources.SendingFile;
                        case ChatActionUploadingPhoto uploadPhoto:
                            commonAction = uploadPhoto;
                            return Strings.Resources.SendingPhoto;
                        case ChatActionRecordingVideo recordVideo:
                        case ChatActionUploadingVideo uploadVideo:
                            commonAction = new ChatActionUploadingVideo();
                            return Strings.Resources.SendingVideoStatus;
                    }
                }

                commonAction = new ChatActionTyping();
                return Strings.Resources.Typing;
            }

            if (typingUsers.Count == 1)
            {
                var tuple = typingUsers.FirstOrDefault();

                var user = getUser.Invoke(tuple.Key);
                if (user == null)
                {
                    commonAction = null;
                    return string.Empty;
                }

                var userName = string.IsNullOrEmpty(user.FirstName) ? user.LastName : user.FirstName;

                //if (tuple != null)
                {
                    var action = tuple.Value;
                    switch (action)
                    {
                        //case TLSendMessageChooseContactAction chooseContact:
                        //    return "";
                        case ChatActionStartPlayingGame gamePlay:
                            commonAction = gamePlay;
                            return string.Format(Strings.Resources.IsSendingGame, userName);
                        //case TLSendMessageGeoLocationAction geoLocation:
                        //    return "";
                        case ChatActionRecordingVoiceNote recordAudio:
                            commonAction = recordAudio;
                            return string.Format(Strings.Resources.IsRecordingAudio, userName);
                        case ChatActionRecordingVideoNote recordRound:
                        case ChatActionUploadingVideoNote uploadRound:
                            commonAction = new ChatActionRecordingVideoNote();
                            return string.Format(Strings.Resources.IsSendingVideo, userName);
                        //case TLSendMessageTypingAction typing:
                        //    return string.Format(Strings.Resources.IsTyping, userName);
                        case ChatActionUploadingVoiceNote uploadAudio:
                            commonAction = uploadAudio;
                            return string.Format(Strings.Resources.IsSendingAudio, userName);
                        case ChatActionUploadingDocument uploadDocument:
                            commonAction = uploadDocument;
                            return string.Format(Strings.Resources.IsSendingFile, userName);
                        case ChatActionUploadingPhoto uploadPhoto:
                            commonAction = uploadPhoto;
                            return string.Format(Strings.Resources.IsSendingPhoto, userName);
                        case ChatActionRecordingVideo recordVideo:
                        case ChatActionUploadingVideo uploadVideo:
                            commonAction = new ChatActionUploadingVideo();
                            return string.Format(Strings.Resources.IsSendingVideo, userName);
                    }
                }

                commonAction = new ChatActionTyping();
                return string.Format("{0} {1}", userName, Strings.Resources.IsTyping);
            }
            else
            {

                var count = 0;
                var label = string.Empty;
                foreach (var pu in typingUsers)
                {
                    var user = getUser.Invoke(pu.Key);
                    if (user == null)
                    {

                    }

                    if (user != null)
                    {
                        if (label.Length > 0)
                        {
                            label += ", ";
                        }
                        label += string.IsNullOrEmpty(user.FirstName) ? user.LastName : user.FirstName;
                        count++;
                    }
                    if (count == 2)
                    {
                        break;
                    }
                }

                if (label.Length > 0)
                {
                    if (count == 1)
                    {
                        commonAction = new ChatActionTyping();
                        return string.Format("{0} {1}", label, Strings.Resources.IsTyping);
                    }
                    else
                    {
                        if (typingUsers.Count > 2)
                        {
                            commonAction = new ChatActionTyping();
                            return string.Format("{0} {1}", label, Locale.Declension("AndMoreTyping", typingUsers.Count - 2));
                        }
                        else
                        {
                            commonAction = new ChatActionTyping();
                            return string.Format("{0} {1}", label, Strings.Resources.AreTyping);
                        }
                    }
                }

                commonAction = null;
                return string.Empty;
            }
        }
    }
}
