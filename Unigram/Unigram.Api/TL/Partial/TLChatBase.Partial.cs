using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;

namespace Telegram.Api.TL
{
    public abstract partial class TLChatBase : ITLDialogWith, ITLInputPeer
    {
        //#region Full chat information

        //public TLChatParticipantsBase Participants { get; set; }

        //public TLPhotoBase ChatPhoto { get; set; }

        //public TLPeerNotifySettingsBase NotifySettings { get; set; }

        //public int UsersOnline { get; set; }

        //public TLExportedChatInviteBase ExportedInvite { get; set; }

        //public TLVector<TLBotInfo> BotInfo { get; set; }

        //#endregion

        public virtual void Update(TLChatBase chat)
        {
            Id = chat.Id;

            //if (chat.Participants != null)
            //{
            //    Participants = chat.Participants;
            //}

            //if (chat.ChatPhoto != null)
            //{
            //    ChatPhoto = chat.ChatPhoto;
            //}

            //if (chat.NotifySettings != null)
            //{
            //    NotifySettings = chat.NotifySettings;
            //}
        }

        #region Add

        public virtual string DisplayName
        {
            get
            {
                return null;
            }
        }

        public virtual object PhotoSelf
        {
            get
            {
                return this;
            }
        }

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;
        public override void RaisePropertyChanged(string propertyName)
        {
            Execute.OnUIThread(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }

        public virtual TLInputPeerBase ToInputPeer()
        {
            throw new NotImplementedException();
        }

        public virtual TLPeerBase ToPeer()
        {
            throw new NotImplementedException();
        }
    }

    public partial class TLChat
    {
        public override string DisplayName
        {
            get
            {
                return Title;
            }
        }

        public override TLInputPeerBase ToInputPeer()
        {
            return new TLInputPeerChat { ChatId = Id };
        }

        public override TLPeerBase ToPeer()
        {
            return new TLPeerChat { ChatId = Id };
        }
    }

    public partial class TLChatForbidden
    {
        public override string DisplayName
        {
            get
            {
                return Title;
            }
        }

        public override TLInputPeerBase ToInputPeer()
        {
            return new TLInputPeerChat { ChatId = Id };
        }

        public override TLPeerBase ToPeer()
        {
            return new TLPeerChat { ChatId = Id };
        }
    }

    public partial class TLChannel
    {
        public override string DisplayName
        {
            get
            {
                return Title;
            }
        }

        public override TLInputPeerBase ToInputPeer()
        {
            return new TLInputPeerChannel { ChannelId = Id, AccessHash = AccessHash.Value };
        }

        public override TLPeerBase ToPeer()
        {
            return new TLPeerChannel { ChannelId = Id };
        }
    }

    public partial class TLChannelForbidden
    {
        public override string DisplayName
        {
            get
            {
                return Title;
            }
        }

        public override TLInputPeerBase ToInputPeer()
        {
            return new TLInputPeerChannel { ChannelId = Id, AccessHash = AccessHash };
        }

        public override TLPeerBase ToPeer()
        {
            return new TLPeerChannel { ChannelId = Id };
        }
    }
}
