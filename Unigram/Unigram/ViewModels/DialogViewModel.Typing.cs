using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Extensions;
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
                    TypingSubtitle = GetTypingSubtitle(users);
                    IsTyping = true;
                }, 
				() =>
                {
                    TypingSubtitle = null;
                    IsTyping = false;
                });
            }
        }

        private OutputTypingManager _outputTypingManager;
        public OutputTypingManager OutputTypingManager
        {
            get
            {
                return _outputTypingManager = _outputTypingManager ?? new OutputTypingManager(ProtoService, Peer);
            }
        }

        public void Handle(TLUpdateUserTyping userTyping)
        {
            var user = this.With as TLUser;
            if (user != null && !user.IsSelf && user.Id == userTyping.UserId)
            {
                var action = userTyping.Action;
                if (action is TLSendMessageCancelAction)
                {
                    InputTypingManager.RemoveTypingUser(userTyping.UserId);
                    return;
                }

                InputTypingManager.AddTypingUser(userTyping.UserId, action);
            }
        }

        public void Handle(TLUpdateChatUserTyping chatUserTyping)
        {
            var chatBase = With as TLChatBase;
            if (chatBase != null && chatBase.Id == chatUserTyping.ChatId)
            {
                var action = chatUserTyping.Action;
                if (action is TLSendMessageCancelAction)
                {
                    InputTypingManager.RemoveTypingUser(chatUserTyping.UserId);
                    return;
                }

                InputTypingManager.AddTypingUser(chatUserTyping.UserId, action);
            }
        }

        private string GetTypingSubtitle(IList<Tuple<int, TLSendMessageActionBase>> typingUsers)
        {
            return GetTypingString(Peer.ToPeer(), typingUsers, CacheService.GetUser, null);
        }

        public static string GetTypingString(TLPeerBase peer, IList<Tuple<int, TLSendMessageActionBase>> typingUsers, Func<int?, TLUserBase> getUser, Action<TLPeerBase> getFullInfoAction)
        {
            if (peer is TLPeerUser)
            {
                var tuple = typingUsers.FirstOrDefault();
                if (tuple != null)
                {
                    var action = tuple.Item2;
                    switch (action)
                    {
                        //case TLSendMessageChooseContactAction chooseContact:
                        //    return "";
                        case TLSendMessageGamePlayAction gamePlay:
                            return Strings.Resources.PlayingGame;
                        //case TLSendMessageGeoLocationAction geoLocation:
                        //    return "";
                        case TLSendMessageRecordAudioAction recordAudio:
                            return Strings.Resources.RecordingVoiceMessage;
                        case TLSendMessageRecordRoundAction recordRound:
                            return Strings.Resources.RecordingVideoMessage;
                        case TLSendMessageRecordVideoAction recordVideo:
                            return Strings.Resources.RecordingVideo;
                        //case TLSendMessageTypingAction typing:
                        //    return Strings.Resources.Typing;
                        case TLSendMessageUploadAudioAction uploadAudio:
                            return Strings.Resources.SendingAudio;
                        case TLSendMessageUploadDocumentAction uploadDocument:
                            return Strings.Resources.SendingFile;
                        case TLSendMessageUploadPhotoAction uploadPhoto:
                            return Strings.Resources.SendingPhoto;
                        case TLSendMessageUploadRoundAction uploadRound:
                            return Strings.Resources.SendingVideoMessage;
                        case TLSendMessageUploadVideoAction uploadVideo:
                            return Strings.Resources.SendingVideo;
                    }
                }

                return Strings.Resources.Typing;
            }

            if (typingUsers.Count == 1)
            {
                var user = getUser.Invoke(typingUsers[0].Item1) as TLUser;
                if (user == null)
                {
                    getFullInfoAction?.Invoke(peer);
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
                        case TLSendMessageGamePlayAction gamePlay:
                            return string.Format(Strings.Resources.IsPlayingGame, userName);
                        //case TLSendMessageGeoLocationAction geoLocation:
                        //    return "";
                        case TLSendMessageRecordAudioAction recordAudio:
                            return string.Format(Strings.Resources.IsRecordingAudio, userName);
                        case TLSendMessageRecordRoundAction recordRound:
                            return string.Format(Strings.Resources.IsRecordingVideoMessage, userName);
                        case TLSendMessageRecordVideoAction recordVideo:
                            return string.Format(Strings.Resources.IsRecordingVideo, userName);
                        //case TLSendMessageTypingAction typing:
                        //    return string.Format(Strings.Resources.IsTyping, userName);
                        case TLSendMessageUploadAudioAction uploadAudio:
                            return string.Format(Strings.Resources.IsSendingAudio, userName);
                        case TLSendMessageUploadDocumentAction uploadDocument:
                            return string.Format(Strings.Resources.IsSendingFile, userName);
                        case TLSendMessageUploadPhotoAction uploadPhoto:
                            return string.Format(Strings.Resources.IsSendingPhoto, userName);
                        case TLSendMessageUploadRoundAction uploadRound:
                            return string.Format(Strings.Resources.IsSendingVideoMessage, userName);
                        case TLSendMessageUploadVideoAction uploadVideo:
                            return string.Format(Strings.Resources.IsSendingVideo, userName);
                    }
                }

                return string.Format(Strings.Resources.IsTyping, userName);
            }
            else
            {
                if (typingUsers.Count > 3)
                {
                    return string.Format(Strings.Resources.AreTyping, Language.Declension(typingUsers.Count, Strings.Resources.CompanyNominativeSingular, Strings.Resources.CompanyNominativePlural, Strings.Resources.CompanyGenitiveSingular, Strings.Resources.CompanyGenitivePlural, null, null));
                }

                var names = new List<string>(typingUsers.Count);
                var missing = new List<int>();

				foreach (var current in typingUsers)
                {
                    var user = getUser.Invoke(current.Item1) as TLUser;
                    if (user != null)
                    {
                        names.Add(string.IsNullOrEmpty(user.FirstName) ? user.LastName : user.FirstName);
                    }
                    else
                    {
                        missing.Add(current.Item1);
                    }

                }

                if (missing.Count > 0)
                {
                    getFullInfoAction?.Invoke(peer);
                    return null;
                }

                return string.Format(Strings.Resources.AreTyping, string.Join(", ", names));
            }
        }
    }
}
