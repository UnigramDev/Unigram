using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Common.Dialogs;
using Unigram.Strings;

namespace Unigram.ViewModels
{
    public partial class DialogViewModel /*: IHandle<TLUpdateUserTyping>, IHandle<TLUpdateChatUserTyping>*/
    {
        private bool _isTyping;
		public bool IsTyping
        {
            get
            {
                return _isTyping;
            }
            set
            {
                Set(ref _isTyping, value);
            }
        }

        private string _typingSubtitle;
		public string TypingSubtitle
        {
            get
            {
                return _typingSubtitle;
            }
            set
            {
                Set(ref _typingSubtitle, value);
            }
        }

        private InputTypingManager _inputTypingManager;
        public InputTypingManager InputTypingManager
        {
            get
            {
                return _inputTypingManager = _inputTypingManager ?? new InputTypingManager(users =>
                {
                    BeginOnUIThread(() =>
                    {
                        TypingSubtitle = GetTypingSubtitle(users);
                        IsTyping = true;
                    });
                }, 
				() =>
                {
                    BeginOnUIThread(() =>
                    {
                        TypingSubtitle = null;
                        IsTyping = false;
                    });
                });
            }
        }

        private OutputTypingManager _outputTypingManager;
        public OutputTypingManager OutputTypingManager
        {
            get
            {
                return _outputTypingManager = _outputTypingManager ?? new OutputTypingManager(ProtoService, _chat);
            }
        }

        //public void Handle(TLUpdateUserTyping userTyping)
        //{
        //    var user = this.With as TLUser;
        //    if (user != null && !user.IsSelf && user.Id == userTyping.UserId)
        //    {
        //        var action = userTyping.Action;
        //        if (action is TLSendMessageCancelAction)
        //        {
        //            InputTypingManager.RemoveTypingUser(userTyping.UserId);
        //            return;
        //        }

        //        InputTypingManager.AddTypingUser(userTyping.UserId, action);
        //    }
        //}

        //public void Handle(TLUpdateChatUserTyping chatUserTyping)
        //{
        //    var chatBase = With as TLChatBase;
        //    if (chatBase != null && chatBase.Id == chatUserTyping.ChatId)
        //    {
        //        var action = chatUserTyping.Action;
        //        if (action is TLSendMessageCancelAction)
        //        {
        //            InputTypingManager.RemoveTypingUser(chatUserTyping.UserId);
        //            return;
        //        }

        //        InputTypingManager.AddTypingUser(chatUserTyping.UserId, action);
        //    }
        //}

        private string GetTypingSubtitle(IList<Tuple<int, ChatAction>> typingUsers)
        {
            return GetTypingString(Chat, typingUsers, ProtoService.GetUser);
        }

        public static string GetTypingString(Chat chat, IList<Tuple<int, ChatAction>> typingUsers, Func<int, User> getUser)
        {
            if (chat.Type is ChatTypePrivate || chat.Type is ChatTypeSecret)
            {
                var tuple = typingUsers.FirstOrDefault();
                if (tuple != null)
                {
                    var action = tuple.Item2;
                    switch (action)
                    {
                        //case TLSendMessageChooseContactAction chooseContact:
                        //    return "";
                        case ChatActionStartPlayingGame gamePlay:
                            return Strings.Resources.SendingGame;
                        //case TLSendMessageGeoLocationAction geoLocation:
                        //    return "";
                        case ChatActionRecordingVoiceNote recordAudio:
                            return Strings.Resources.RecordingAudio;
                        case ChatActionRecordingVideoNote recordRound:
                        case ChatActionUploadingVideoNote uploadRound:
                            return Strings.Resources.RecordingRound;
                        //case TLSendMessageTypingAction typing:
                        //    return Strings.Resources.Typing;
                        case ChatActionUploadingVoiceNote uploadAudio:
                            return Strings.Resources.SendingAudio;
                        case ChatActionUploadingDocument uploadDocument:
                            return Strings.Resources.SendingFile;
                        case ChatActionUploadingPhoto uploadPhoto:
                            return Strings.Resources.SendingPhoto;
                        case ChatActionRecordingVideo recordVideo:
                        case ChatActionUploadingVideo uploadVideo:
                            return Strings.Resources.SendingVideoStatus;
                    }
                }

                return Strings.Resources.Typing;
            }

            if (typingUsers.Count == 1)
            {
                var user = getUser.Invoke(typingUsers[0].Item1);
                if (user == null)
                {
                    return null;
                }

                var userName = string.IsNullOrEmpty(user.FirstName) ? user.LastName : user.FirstName;

                var tuple = typingUsers.FirstOrDefault();
                if (tuple != null)
                {
                    var action = tuple.Item2;
                    switch (action)
                    {
                        //case TLSendMessageChooseContactAction chooseContact:
                        //    return "";
                        case ChatActionStartPlayingGame gamePlay:
                            return string.Format(Strings.Resources.IsSendingGame, userName);
                        //case TLSendMessageGeoLocationAction geoLocation:
                        //    return "";
                        case ChatActionRecordingVoiceNote recordAudio:
                            return string.Format(Strings.Resources.IsRecordingAudio, userName);
                        case ChatActionRecordingVideoNote recordRound:
                        case ChatActionUploadingVideoNote uploadRound:
                            return string.Format(Strings.Resources.IsSendingVideo, userName);
                        //case TLSendMessageTypingAction typing:
                        //    return string.Format(Strings.Resources.IsTyping, userName);
                        case ChatActionUploadingVoiceNote uploadAudio:
                            return string.Format(Strings.Resources.IsSendingAudio, userName);
                        case ChatActionUploadingDocument uploadDocument:
                            return string.Format(Strings.Resources.IsSendingFile, userName);
                        case ChatActionUploadingPhoto uploadPhoto:
                            return string.Format(Strings.Resources.IsSendingPhoto, userName);
                        case ChatActionRecordingVideo recordVideo:
                        case ChatActionUploadingVideo uploadVideo:
                            return string.Format(Strings.Resources.IsSendingVideo, userName);
                    }
                }

                return string.Format("{0} {1}", userName, Strings.Resources.IsTyping);
            }
            else
            {

                var count = 0;
                var label = string.Empty;
                foreach (var pu in typingUsers)
                {
                    var user = getUser.Invoke(pu.Item1);
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
                        return string.Format("{0} {1}", label, Strings.Resources.IsTyping);
                    }
                    else
                    {
                        if (typingUsers.Count > 2)
                        {
                            return string.Format("{0} {1}", label, Locale.Declension("AndMoreTyping", typingUsers.Count - 2));
                        }
                        else
                        {
                            return string.Format("{0} {1}", label, Strings.Resources.AreTyping);
                        }
                    }
                }

                return null;
            }
        }
    }
}
