//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Td.Api;

namespace Unigram.Common.Chats
{
    public class InputChatActionManager
    {
        public static string GetTypingString(Chat chat, IDictionary<MessageSender, ChatAction> typingUsers, Func<long, User> getUser, Func<long, Chat> getChat, out ChatAction commonAction)
        {
            if (chat?.Type is ChatTypePrivate or ChatTypeSecret)
            {
                var tuple = typingUsers.FirstOrDefault();
                var action = tuple.Value;
                switch (action)
                {
                    //case TLSendMessageChooseContactAction chooseContact:
                    //    return "";
                    case ChatActionStartPlayingGame gamePlay:
                        commonAction = gamePlay;
                        return Strings.Resources.SendingGame.TrimEnd('.');
                    //case TLSendMessageGeoLocationAction geoLocation:
                    //    return "";
                    case ChatActionRecordingVoiceNote recordAudio:
                        commonAction = recordAudio;
                        return Strings.Resources.RecordingAudio.TrimEnd('.');
                    case ChatActionRecordingVideoNote:
                    case ChatActionUploadingVideoNote:
                        commonAction = new ChatActionRecordingVideoNote();
                        return Strings.Resources.RecordingRound.TrimEnd('.');
                    //case TLSendMessageTypingAction typing:
                    //    return Strings.Resources.Typing;
                    case ChatActionUploadingVoiceNote uploadAudio:
                        commonAction = uploadAudio;
                        return Strings.Resources.SendingAudio.TrimEnd('.');
                    case ChatActionUploadingDocument uploadDocument:
                        commonAction = uploadDocument;
                        return Strings.Resources.SendingFile.TrimEnd('.');
                    case ChatActionUploadingPhoto uploadPhoto:
                        commonAction = uploadPhoto;
                        return Strings.Resources.SendingPhoto.TrimEnd('.');
                    case ChatActionRecordingVideo:
                    case ChatActionUploadingVideo:
                        commonAction = new ChatActionUploadingVideo();
                        return Strings.Resources.SendingVideoStatus.TrimEnd('.');
                    case ChatActionChoosingSticker choosingSticker:
                        commonAction = choosingSticker;
                        return Strings.Resources.ChoosingSticker.Replace("**", "").TrimEnd('.');
                    case ChatActionWatchingAnimations watchingAnimations:
                        commonAction = watchingAnimations;
                        return string.Format(Strings.Resources.EnjoyngAnimations.Replace("**oo**", string.Empty).Trim(' '), watchingAnimations.Emoji);
                }

                commonAction = new ChatActionTyping();
                return Strings.Resources.Typing.TrimEnd('.');
            }

            if (typingUsers.Count == 1)
            {
                var tuple = typingUsers.FirstOrDefault();

                string userName = null;
                if (tuple.Key is MessageSenderUser senderUser)
                {
                    var user = getUser.Invoke(senderUser.UserId);
                    if (user == null)
                    {
                        commonAction = null;
                        return string.Empty;
                    }

                    userName = string.IsNullOrEmpty(user.FirstName) ? user.LastName : user.FirstName;
                }
                else if (tuple.Key is MessageSenderChat senderChat)
                {
                    var user = getChat.Invoke(senderChat.ChatId);
                    if (user == null)
                    {
                        commonAction = null;
                        return string.Empty;
                    }

                    userName = chat.Title;
                }

                var action = tuple.Value;
                switch (action)
                {
                    //case TLSendMessageChooseContactAction chooseContact:
                    //    return "";
                    case ChatActionStartPlayingGame gamePlay:
                        commonAction = gamePlay;
                        return string.Format(Strings.Resources.IsSendingGame.TrimEnd('.'), userName);
                    //case TLSendMessageGeoLocationAction geoLocation:
                    //    return "";
                    case ChatActionRecordingVoiceNote recordAudio:
                        commonAction = recordAudio;
                        return string.Format(Strings.Resources.IsRecordingAudio.TrimEnd('.'), userName);
                    case ChatActionRecordingVideoNote:
                    case ChatActionUploadingVideoNote:
                        commonAction = new ChatActionRecordingVideoNote();
                        return string.Format(Strings.Resources.IsSendingVideo.TrimEnd('.'), userName);
                    //case TLSendMessageTypingAction typing:
                    //    return string.Format(Strings.Resources.IsTyping, userName);
                    case ChatActionUploadingVoiceNote uploadAudio:
                        commonAction = uploadAudio;
                        return string.Format(Strings.Resources.IsSendingAudio.TrimEnd('.'), userName);
                    case ChatActionUploadingDocument uploadDocument:
                        commonAction = uploadDocument;
                        return string.Format(Strings.Resources.IsSendingFile.TrimEnd('.'), userName);
                    case ChatActionUploadingPhoto uploadPhoto:
                        commonAction = uploadPhoto;
                        return string.Format(Strings.Resources.IsSendingPhoto.TrimEnd('.'), userName);
                    case ChatActionRecordingVideo:
                    case ChatActionUploadingVideo:
                        commonAction = new ChatActionUploadingVideo();
                        return string.Format(Strings.Resources.IsSendingVideo.TrimEnd('.'), userName);
                    case ChatActionChoosingSticker choosingSticker:
                        commonAction = choosingSticker;
                        return string.Format(Strings.Resources.IsChoosingSticker.Replace("**", "").TrimEnd('.'), userName);
                }

                commonAction = new ChatActionTyping();
                return string.Format("{0} {1}", userName, Strings.Resources.IsTyping.TrimEnd('.'));
            }
            else
            {
                var count = 0;
                var label = string.Empty;
                foreach (var pu in typingUsers)
                {
                    if (pu.Key is MessageSenderUser senderUser)
                    {
                        var user = getUser.Invoke(senderUser.UserId);
                        if (user != null)
                        {
                            if (label.Length > 0)
                            {
                                label += ", ";
                            }
                            label += string.IsNullOrEmpty(user.FirstName) ? user.LastName : user.FirstName;
                            count++;
                        }
                    }
                    else if (pu.Key is MessageSenderChat senderChat)
                    {
                        var user = getChat.Invoke(senderChat.ChatId);
                        if (user != null)
                        {
                            if (label.Length > 0)
                            {
                                label += ", ";
                            }
                            label += user.Title;
                            count++;
                        }
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
                            return string.Format("{0} {1}", label, Locale.Declension("AndMoreTyping", typingUsers.Count - 2).TrimEnd('.'));
                        }
                        else
                        {
                            commonAction = new ChatActionTyping();
                            return string.Format("{0} {1}", label, Strings.Resources.AreTyping.TrimEnd('.'));
                        }
                    }
                }

                commonAction = null;
                return string.Empty;
            }
        }
    }
}
