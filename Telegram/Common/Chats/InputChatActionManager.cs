//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using System.Linq;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.Common.Chats
{
    public partial class InputChatActionManager
    {
        public static string GetTypingString(ChatType chatType, IDictionary<MessageSender, ChatAction> typingUsers, IClientService clientService, out ChatAction commonAction)
        {
            if (chatType is ChatTypePrivate or ChatTypeSecret)
            {
                var tuple = typingUsers.FirstOrDefault();
                var action = tuple.Value;
                switch (action)
                {
                    //case TLSendMessageChooseContactAction chooseContact:
                    //    return "";
                    case ChatActionStartPlayingGame gamePlay:
                        commonAction = gamePlay;
                        return Strings.SendingGame.TrimEnd('.');
                    //case TLSendMessageGeoLocationAction geoLocation:
                    //    return "";
                    case ChatActionRecordingVoiceNote recordAudio:
                        commonAction = recordAudio;
                        return Strings.RecordingAudio.TrimEnd('.');
                    case ChatActionRecordingVideoNote:
                    case ChatActionUploadingVideoNote:
                        commonAction = new ChatActionRecordingVideoNote();
                        return Strings.RecordingRound.TrimEnd('.');
                    //case TLSendMessageTypingAction typing:
                    //    return Strings.Typing;
                    case ChatActionUploadingVoiceNote uploadAudio:
                        commonAction = uploadAudio;
                        return Strings.SendingAudio.TrimEnd('.');
                    case ChatActionUploadingDocument uploadDocument:
                        commonAction = uploadDocument;
                        return Strings.SendingFile.TrimEnd('.');
                    case ChatActionUploadingPhoto uploadPhoto:
                        commonAction = uploadPhoto;
                        return Strings.SendingPhoto.TrimEnd('.');
                    case ChatActionRecordingVideo:
                    case ChatActionUploadingVideo:
                        commonAction = new ChatActionUploadingVideo();
                        return Strings.SendingVideoStatus.TrimEnd('.');
                    case ChatActionChoosingSticker choosingSticker:
                        commonAction = choosingSticker;
                        return Strings.ChoosingSticker.Replace("**", "").TrimEnd('.');
                    case ChatActionWatchingAnimations watchingAnimations:
                        commonAction = watchingAnimations;
                        return string.Format(Strings.EnjoyngAnimations.Replace("**oo**", string.Empty).Trim(' '), watchingAnimations.Emoji);
                }

                commonAction = new ChatActionTyping();
                return Strings.Typing.TrimEnd('.');
            }

            if (typingUsers.Count == 1)
            {
                var tuple = typingUsers.FirstOrDefault();

                string userName = null;
                if (clientService.TryGetUser(tuple.Key, out User senderUser))
                {
                    userName = senderUser.FirstName;
                }
                else if (clientService.TryGetChat(tuple.Key, out Chat senderChat))
                {
                    userName = senderChat.Title;
                }
                else
                {
                    commonAction = null;
                    return string.Empty;
                }

                var action = tuple.Value;
                switch (action)
                {
                    //case TLSendMessageChooseContactAction chooseContact:
                    //    return "";
                    case ChatActionStartPlayingGame gamePlay:
                        commonAction = gamePlay;
                        return string.Format(Strings.IsSendingGame.TrimEnd('.'), userName);
                    //case TLSendMessageGeoLocationAction geoLocation:
                    //    return "";
                    case ChatActionRecordingVoiceNote recordAudio:
                        commonAction = recordAudio;
                        return string.Format(Strings.IsRecordingAudio.TrimEnd('.'), userName);
                    case ChatActionRecordingVideoNote:
                    case ChatActionUploadingVideoNote:
                        commonAction = new ChatActionRecordingVideoNote();
                        return string.Format(Strings.IsSendingVideo.TrimEnd('.'), userName);
                    //case TLSendMessageTypingAction typing:
                    //    return string.Format(Strings.IsTyping, userName);
                    case ChatActionUploadingVoiceNote uploadAudio:
                        commonAction = uploadAudio;
                        return string.Format(Strings.IsSendingAudio.TrimEnd('.'), userName);
                    case ChatActionUploadingDocument uploadDocument:
                        commonAction = uploadDocument;
                        return string.Format(Strings.IsSendingFile.TrimEnd('.'), userName);
                    case ChatActionUploadingPhoto uploadPhoto:
                        commonAction = uploadPhoto;
                        return string.Format(Strings.IsSendingPhoto.TrimEnd('.'), userName);
                    case ChatActionRecordingVideo:
                    case ChatActionUploadingVideo:
                        commonAction = new ChatActionUploadingVideo();
                        return string.Format(Strings.IsSendingVideo.TrimEnd('.'), userName);
                    case ChatActionChoosingSticker choosingSticker:
                        commonAction = choosingSticker;
                        return string.Format(Strings.IsChoosingSticker.Replace("**", "").TrimEnd('.'), userName);
                }

                commonAction = new ChatActionTyping();
                return string.Format("{0} {1}", userName, Strings.IsTyping.TrimEnd('.'));
            }
            else
            {
                var count = 0;
                var label = string.Empty;
                foreach (var pu in typingUsers)
                {
                    if (clientService.TryGetUser(pu.Key, out User senderUser))
                    {
                        if (label.Length > 0)
                        {
                            label += ", ";
                        }
                        label += senderUser.FirstName;
                        count++;
                    }
                    else if (clientService.TryGetChat(pu.Key, out Chat senderChat))
                    {
                        if (label.Length > 0)
                        {
                            label += ", ";
                        }
                        label += senderChat.Title;
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
                        return string.Format("{0} {1}", label, Strings.IsTyping);
                    }
                    else
                    {
                        if (typingUsers.Count > 2)
                        {
                            commonAction = new ChatActionTyping();
                            return string.Format("{0} {1}", label, Locale.Declension(Strings.R.AndMoreTyping, typingUsers.Count - 2).TrimEnd('.'));
                        }
                        else
                        {
                            commonAction = new ChatActionTyping();
                            return string.Format("{0} {1}", label, Strings.AreTyping.TrimEnd('.'));
                        }
                    }
                }

                commonAction = null;
                return string.Empty;
            }
        }
    }
}
