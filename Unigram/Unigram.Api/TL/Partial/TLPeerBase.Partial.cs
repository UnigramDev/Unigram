using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;

namespace Telegram.Api.TL
{
    public abstract partial class TLPeerBase : TLObject
    {
        public Int32 Id
        {
            get
            {
                if (this is TLPeerUser)
                    return ((TLPeerUser)this).UserId;
                else if (this is TLPeerChat)
                    return ((TLPeerChat)this).ChatId;
                else
                    return ((TLPeerChannel)this).ChannelId;
            }
            set
            {
                if (this is TLPeerUser)
                    ((TLPeerUser)this).UserId = value;
                else if (this is TLPeerChat)
                    ((TLPeerChat)this).ChatId = value;
                else
                    ((TLPeerChannel)this).ChannelId = value;
            }
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            if (this is TLPeerUser userPeer)
            {
                if (obj is TLPeerUser otherUser)
                {
                    return userPeer.UserId == otherUser.UserId;
                }
                else if (obj is TLInputPeerUser inputUser)
                {
                    return userPeer.UserId == inputUser.UserId;
                }
                else if (obj is TLInputPeerSelf selfUser)
                {
                    return userPeer.UserId == SettingsHelper.UserId;
                }
            }
            else if (this is TLPeerChat chatPeer)
            {
                if (obj is TLPeerChat otherChat)
                {
                    return chatPeer.ChatId == otherChat.ChatId;
                }
                else if (obj is TLInputPeerChat inputChat)
                {
                    return chatPeer.ChatId == inputChat.ChatId;
                }
            }
            else if (this is TLPeerChannel channelPeer)
            {
                if (obj is TLPeerChannel otherChannel)
                {
                    return channelPeer.ChannelId == otherChannel.ChannelId;
                }
                else if (obj is TLInputPeerChannel inputChannel)
                {
                    return channelPeer.ChannelId == inputChannel.ChannelId;
                }
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        //#region User equality

        //public static bool operator ==(TLPeerBase peer1, TLPeerBase peer2)
        //{
        //    if ((object)peer1 == null || (object)peer2 == null)
        //    {
        //        return false;
        //    }

        //    return peer1.Equals(peer2);
        //}

        //public static bool operator !=(TLPeerBase peer1, TLPeerBase peer2)
        //{
        //    return !(peer1 == peer2);
        //}

        //#endregion

        //#region Chat equality

        //public static bool operator ==(TLPeerBase peer, TLChat chat)
        //{
        //    if (((object)peer == null) || ((object)peer == null))
        //    {
        //        return false;
        //    }

        //    if (peer is TLPeerChat chatPeer && chatPeer.ChatId == chat.Id)
        //    {
        //        return true;
        //    }

        //    return false;
        //}

        //public static bool operator !=(TLPeerBase peer, TLChat chat)
        //{
        //    return !(peer == chat);
        //}

        //#endregion

        //#region Channel equality

        //public static bool operator ==(TLPeerBase peer, TLChannel channel)
        //{
        //    if (((object)peer == null) || ((object)peer == null))
        //    {
        //        return false;
        //    }

        //    if (peer is TLPeerChannel channelPeer && channelPeer.ChannelId == channel.Id)
        //    {
        //        return true;
        //    }

        //    return false;
        //}

        //public static bool operator !=(TLPeerBase peer, TLChannel channel)
        //{
        //    return !(peer == channel);
        //}

        //#endregion
    }
}