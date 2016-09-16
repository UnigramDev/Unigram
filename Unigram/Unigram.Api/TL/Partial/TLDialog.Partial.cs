using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Windows.UI.Xaml;

namespace Telegram.Api.TL
{
    public partial class TLDialog : INotifyPropertyChanged
    {
        public ObservableCollection<TLMessageBase> Messages { get; set; } = new ObservableCollection<TLMessageBase>();

        public virtual int CountMessages()
        {
            return Messages.Count;
        }

        public virtual void Update(TLDialog dialog)
        {
            Flags = dialog.Flags;
            Draft = dialog.Draft;

            ReadInboxMaxId = dialog.ReadInboxMaxId;

            Peer = dialog.Peer;
            UnreadCount = dialog.UnreadCount;

            //если последнее сообщение отправляется и имеет дату больше, то не меняем
            if (TopMessage == null && (TopMessageItem == null || TopMessageItem.Date > dialog.TopMessageItem.Date))
            {


                //добавляем сообщение в список в нужное место, если его еще нет
                var insertRequired = false;
                if (Messages != null && dialog.TopMessageItem != null)
                {
                    var oldMessage = Messages.FirstOrDefault(x => x.Id == dialog.TopMessageItem.Id);
                    if (oldMessage == null)
                    {
                        insertRequired = true;
                    }
                }

                if (insertRequired)
                {
                    InsertMessageInOrder(Messages, dialog.TopMessageItem);
                }

                return;
            }
            TopMessage = dialog.TopMessage;
            TopMessageRandomId = dialog.TopMessageRandomId;
            TopMessageItem = dialog.TopMessageItem;

            lock (MessagesSyncRoot)
            {
                if (Messages.Count > 0)
                {
                    for (int i = 0; i < Messages.Count; i++)
                    {
                        if (Messages[i].Date < TopMessageItem.Date)
                        {
                            Messages.Insert(i, TopMessageItem);
                            break;
                        }
                        if (Messages[i].Date == TopMessageItem.Date)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    Messages.Add(TopMessageItem);
                }
            }
        }

        public static int InsertMessageInOrder(IList<TLMessageBase> messages, TLMessageBase message)
        {
            var position = -1;

            if (messages.Count == 0)
            {
                position = 0;
            }

            for (var i = 0; i < messages.Count; i++)
            {
                if (messages[i].Id == 0)
                {
                    if (messages[i].Date < message.Date)
                    {
                        position = i;
                        break;
                    }

                    continue;
                }

                if (messages[i].Id == message.Id)
                {
                    position = -1;
                    break;
                }
                if (messages[i].Id < message.Id)
                {
                    position = i;
                    break;
                }
            }

            if (position != -1)
            {
                //message._isAnimated = position == 0;
                messages.Insert(position, message);
            }

            return position;
        }






        public object MessagesSyncRoot = new object();

        public long? TopMessageRandomId { get; set; }

        public TLMessageBase TopMessageItem { get; set; }

        public int GetDateIndex()
        {
            return TopMessageItem != null ? TopMessageItem.Date : 0;
        }

        public int GetDateIndexWithDraft()
        {
            var dateIndex = GetDateIndex();
            var draft = Draft as TLDraftMessage;
            if (draft != null)
            {
                return Math.Max(draft.Date, dateIndex);
            }

            return dateIndex;
        }

        public bool IsChat
        {
            get { return Peer is TLPeerChat; }
        }



        public TLObject With { get; set; }

        public int WithId
        {
            get
            {
                if (With is TLChatBase)
                {
                    return ((TLChatBase)With).Id;
                }
                if (With is TLUserBase)
                {
                    return ((TLUserBase)With).Id;
                }
                return -1;
            }
        }

        public int Index
        {
            get { return Peer != null && Peer.Id != null ? Peer.Id : default(int); }
        }

        public TLDialog Self
        {
            get
            {
                return this;
            }
        }

        #region Add

        public event PropertyChangedEventHandler PropertyChanged;
        public override void RaisePropertyChanged(string propertyName)
        {
            Execute.OnUIThread(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }



        private string _fullName;
        public string FullName
        {
            get
            {
                var user = With as TLUserBase;
                if (user != null)
                {
                    if (user.Id == 333000)
                    {
                        //return AppResources.AppName;
                        return "Telegram";
                    }

                    if (user.Id == 777000)
                    {
                        //return AppResources.TelegramNotifications;
                        return "Telegram";
                    }

                    //                    var userRequest = user as TLUserRequest;
                    //                    if (userRequest != null)
                    //                    {
                    //#if WP8
                    //                    //var phoneUtil = PhoneNumberUtil.GetInstance();
                    //                    //try
                    //                    //{
                    //                    //    return phoneUtil.Format(phoneUtil.Parse("+" + user.Phone.Value, ""), PhoneNumberFormat.INTERNATIONAL);
                    //                    //}
                    //                    //catch (Exception e)
                    //                    {
                    //                        return "+" + user.Phone.Value;
                    //                    }
                    //#else
                    //                        return "+" + user.Phone.Value;
                    //#endif

                    //                    }

                    if (user is TLUserEmpty /*|| user is TLUserDeleted*/)
                    {

                    }

                    return user.FullName.Trim();
                }

                var channel = With as TLChannel;
                if (channel != null)
                {
                    return channel.Title.Trim();
                }

                var chat = With as TLChatBase;
                if (chat != null)
                {
                    return chat.Title.Trim();
                }

                //var encryptedChat = With as TLEncryptedChatCommon;
                //if (encryptedChat != null)
                //{
                //    var currentUserId = IoC.Get<IMTProtoService>().CurrentUserId;
                //    var cache = IoC.Get<ICacheService>();

                //    if (currentUserId.Value == encryptedChat.AdminId.Value)
                //    {
                //        var cachedParticipant = cache.GetUser(encryptedChat.ParticipantId);
                //        return cachedParticipant != null ? cachedParticipant.FullName.Trim() : string.Empty;
                //    }

                //    var cachedAdmin = cache.GetUser(encryptedChat.AdminId);
                //    return cachedAdmin != null ? cachedAdmin.FullName.Trim() : string.Empty;
                //}

                return With != null ? With.ToString() : string.Empty;
            }
        }

        public Visibility VerifiedVisibility
        {
            get
            {
                var channel = With as TLChannel;
                if (channel != null)
                {
                    return channel.IsVerified ? Visibility.Visible : Visibility.Collapsed;
                }

                var user = With as TLUser;
                if (user != null)
                {
                    return user.IsVerified ? Visibility.Visible : Visibility.Collapsed;
                }

                return Visibility.Collapsed;
            }
        }

        public Visibility GroupChat
        {
            get
            {
                var chatType = Peer as TLPeerBase;
                if (Peer.TypeId == TLType.PeerChat || 
                    Peer.TypeId == TLType.PeerChannel)
                {
                    return Visibility.Visible;
                }
                else
                {
                    return Visibility.Collapsed;
                }
            }
        }

        public Visibility MutedVisibility
        {
            get
            {
                var notifySettings = NotifySettings as TLPeerNotifySettings;
                if (notifySettings == null)
                {
                    return Visibility.Collapsed;
                }

                if (notifySettings.IsSilent)
                {
                    return Visibility.Visible;
                }

                var clientDelta = MTProtoService.Current.ClientTicksDelta;
                var utc0SecsLong = notifySettings.MuteUntil * 4294967296 - clientDelta;
                var utc0SecsInt = utc0SecsLong / 4294967296.0;

                var muteUntilDateTime = Utils.UnixTimestampToDateTime(utc0SecsInt);

                return muteUntilDateTime > DateTime.Now ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        #endregion

    }
}
