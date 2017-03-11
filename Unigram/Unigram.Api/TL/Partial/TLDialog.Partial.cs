using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Helpers;
using Windows.UI.Xaml;

namespace Telegram.Api.TL
{
    public partial class TLDialog : INotifyPropertyChanged
    {
        public ObservableCollection<TLMessageBase> Messages { get; set; } = new ObservableCollection<TLMessageBase>();

        public List<TLMessageBase> CommitMessages { get; set; } = new List<TLMessageBase>();

        //public override void ReadFromCache(TLBinaryReader from)
        //{
        //    Messages = new ObservableCollection<TLMessageBase>(TLFactory.Read<TLVector<TLMessageBase>>(from, true));
        //    CommitMessages = new List<TLMessageBase>(TLFactory.Read<TLVector<TLMessageBase>>(from, true));
        //}

        //public override void WriteToCache(TLBinaryWriter to)
        //{
        //    to.WriteObject(new TLVector<TLMessageBase>(Messages), true);
        //    to.WriteObject(new TLVector<TLMessageBase>(CommitMessages), true);
        //}

        public virtual int CountMessages()
        {
            return Messages.Count;
        }

        public TLInputPeerBase ToInputPeer()
        {
            var peer = With as ITLInputPeer;
            if (peer != null)
            {
                return peer.ToInputPeer();
            }

            return null;
        }

        public virtual void Update(TLDialog dialog)
        {
            Flags = dialog.Flags;
            Draft = dialog.Draft;

            ReadInboxMaxId = dialog.ReadInboxMaxId;

            Peer = dialog.Peer;
            UnreadCount = dialog.UnreadCount;

            //если последнее сообщение отправляется и имеет дату больше, то не меняем
            if (TopMessageItem == null && (TopMessageItem == null || TopMessageItem.Date > dialog.TopMessageItem.Date))
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

            for (var i = messages.Count - 1; i >= 0; i--)
            {
                if (messages[i].Id == 0)
                {
                    if (messages[i].Date < message.Date)
                    {
                        position = i + 1;
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
                    position = i + 1;
                    break;
                }
            }

            if (position != -1)
            {
                messages.Insert(position, message);
            }

            return position;

            //var position = -1;

            //if (messages.Count == 0)
            //{
            //    position = 0;
            //}

            //for (var i = 0; i < messages.Count; i++)
            //{
            //    if (messages[i].Id == 0)
            //    {
            //        if (messages[i].Date < message.Date)
            //        {
            //            position = i;
            //            break;
            //        }

            //        continue;
            //    }

            //    if (messages[i].Id == message.Id)
            //    {
            //        position = -1;
            //        break;
            //    }
            //    if (messages[i].Id < message.Id)
            //    {
            //        position = i;
            //        break;
            //    }
            //}

            //if (position != -1)
            //{
            //    //message._isAnimated = position == 0;
            //    messages.Insert(position, message);
            //}

            //return position;
        }






        public object MessagesSyncRoot = new object();

        public long? TopMessageRandomId { get; set; }

        public TLMessageBase _topMessageItem;
        public TLMessageBase TopMessageItem
        {
            get
            {
                return _topMessageItem;
            }
            set
            {
                _topMessageItem = value;
                RaisePropertyChanged(() => TopMessageItem);
            }
        }

        public int GetDateIndex()
        {
            return TopMessageItem != null ? TopMessageItem.Date : 0;
        }

        public int GetDateIndexWithDraft()
        {
            if (IsPinned)
            {
                return int.MaxValue - PinnedIndex;
            }

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


        public ITLDialogWith _with;
        public ITLDialogWith With
        {
            get
            {
                return _with;
            }
            set
            {
                _with = value;
                RaisePropertyChanged(() => With);
            }
        }

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

        public int PinnedIndex { get; set; }

        public bool IsSearchResult { get; set; }

        public Visibility ChatBaseVisibility
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
