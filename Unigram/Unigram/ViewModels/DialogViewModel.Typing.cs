using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TdWindows;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
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
                            return Strings.Android.SendingGame;
                        //case TLSendMessageGeoLocationAction geoLocation:
                        //    return "";
                        case ChatActionRecordingVoiceNote recordAudio:
                            return Strings.Android.RecordingAudio;
                        case ChatActionRecordingVideoNote recordRound:
                        case ChatActionUploadingVideoNote uploadRound:
                            return Strings.Android.RecordingRound;
                        //case TLSendMessageTypingAction typing:
                        //    return Strings.Android.Typing;
                        case ChatActionUploadingVoiceNote uploadAudio:
                            return Strings.Android.SendingAudio;
                        case ChatActionUploadingDocument uploadDocument:
                            return Strings.Android.SendingFile;
                        case ChatActionUploadingPhoto uploadPhoto:
                            return Strings.Android.SendingPhoto;
                        case ChatActionRecordingVideo recordVideo:
                        case ChatActionUploadingVideo uploadVideo:
                            return Strings.Android.SendingVideoStatus;
                    }
                }

                return Strings.Android.Typing;
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
                            return string.Format(Strings.Android.IsSendingGame, userName);
                        //case TLSendMessageGeoLocationAction geoLocation:
                        //    return "";
                        case ChatActionRecordingVoiceNote recordAudio:
                            return string.Format(Strings.Android.IsRecordingAudio, userName);
                        case ChatActionRecordingVideoNote recordRound:
                        case ChatActionUploadingVideoNote uploadRound:
                            return string.Format(Strings.Android.IsSendingVideo, userName);
                        //case TLSendMessageTypingAction typing:
                        //    return string.Format(Strings.Android.IsTyping, userName);
                        case ChatActionUploadingVoiceNote uploadAudio:
                            return string.Format(Strings.Android.IsSendingAudio, userName);
                        case ChatActionUploadingDocument uploadDocument:
                            return string.Format(Strings.Android.IsSendingFile, userName);
                        case ChatActionUploadingPhoto uploadPhoto:
                            return string.Format(Strings.Android.IsSendingPhoto, userName);
                        case ChatActionRecordingVideo recordVideo:
                        case ChatActionUploadingVideo uploadVideo:
                            return string.Format(Strings.Android.IsSendingVideo, userName);
                    }
                }

                return string.Format("{0} {1}", userName, Strings.Android.IsTyping);
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
                        return string.Format("{0} {1}", label, Strings.Android.IsTyping);
                    }
                    else
                    {
                        if (typingUsers.Count > 2)
                        {
                            return string.Format("{0} {1}", label, Locale.Declension("AndMoreTyping", typingUsers.Count - 2));
                        }
                        else
                        {
                            return string.Format("{0} {1}", label, Strings.Android.AreTyping);
                        }
                    }
                }

                return null;
            }
        }
    }
}
