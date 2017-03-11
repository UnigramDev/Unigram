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
    public partial class DialogViewModel : IHandle<TLUpdateUserTyping>, IHandle<TLUpdateChatUserTyping>
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
                return _inputTypingManager = (_inputTypingManager ?? new InputTypingManager((users) =>
                {
                    TypingSubtitle = GetTypingSubtitle(users);
                    IsTyping = true;
                    //this.Subtitle = this.GetTypingSubtitle(users);
                }, 
				() =>
                {
                    IsTyping = false;
                    //this.Subtitle = this.GetSubtitle();
                }));
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
            return GetTypingString(Peer.ToPeer(), typingUsers, new Func<int?, TLUserBase>(CacheService.GetUser), null);
        }

        public static string GetTypingString(TLPeerBase peer, IList<Tuple<int, TLSendMessageActionBase>> typingUsers, Func<int?, TLUserBase> getUser, Action<TLPeerBase> getFullInfoAction)
        {
            if (peer is TLPeerUser)
            {
                var tuple = typingUsers.FirstOrDefault();
                if (tuple != null)
                {
                    var action = tuple.Item2;
                    if (action is TLSendMessageUploadPhotoAction)
                    {
                        return Resources.SendingPhoto;
                    }
                    else if (action is TLSendMessageRecordAudioAction)
                    {
                        return Resources.RecordingVoiceMessage;
                    }
                    else if (action is TLSendMessageUploadAudioAction)
                    {
                        return Resources.SendingAudio;
                    }
                    else if (action is TLSendMessageUploadDocumentAction)
                    {
                        return Resources.SendingFile;
                    }
                    else if (action is TLSendMessageRecordVideoAction)
                    {
                        return Resources.RecordingVideo;
                    }
                    else if (action is TLSendMessageUploadVideoAction)
                    {
                        return Resources.SendingVideo;
                    }
                    else if (action is TLSendMessageGamePlayAction)
                    {
                        return Resources.PlayingGame;
                    }
                }

                return Resources.Typing;
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
                    if (action is TLSendMessageUploadPhotoAction)
                    {
                        return string.Format(Resources.IsSendingPhoto, userName);
                    }
                    if (action is TLSendMessageUploadAudioAction)
                    {
                        return string.Format(Resources.IsSendingAudio, userName);
                    }
                    if (action is TLSendMessageRecordAudioAction)
                    {
                        return string.Format(Resources.IsRecordingAudio, userName);
                    }
                    if (action is TLSendMessageUploadDocumentAction)
                    {
                        return string.Format(Resources.IsSendingFile, userName);
                    }
                    if (action is TLSendMessageRecordVideoAction)
                    {
                        return string.Format(Resources.IsRecordingVideo, userName);
                    }
                    if (action is TLSendMessageUploadVideoAction)
                    {
                        return string.Format(Resources.IsSendingVideo, userName);
                    }
                    if (action is TLSendMessageGamePlayAction)
                    {
                        return string.Format(Resources.IsPlayingGame, userName);
                    }
                }

                return string.Format(Resources.IsTyping, userName);
            }
            else
            {
                if (typingUsers.Count > 3)
                {
                    return string.Format(Resources.AreTyping, Language.Declension(typingUsers.Count, Resources.CompanyNominativeSingular, Resources.CompanyNominativePlural, Resources.CompanyGenitiveSingular, Resources.CompanyGenitivePlural, null, null));
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

                return string.Format(Resources.AreTyping, string.Join(", ", names));
            }
        }
    }
}
