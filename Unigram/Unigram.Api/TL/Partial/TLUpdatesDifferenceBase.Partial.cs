using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL.Updates
{
    public abstract partial class TLUpdatesDifferenceBase
    {
        public abstract TLUpdatesDifferenceBase GetEmptyObject();
    }

    public partial class TLUpdatesDifferenceEmpty
    {
        public override TLUpdatesDifferenceBase GetEmptyObject()
        {
            return new TLUpdatesDifferenceEmpty
            {
                Date = Date,
                Seq = Seq
            };
        }

        public override string ToString()
        {
            return string.Format("TLUpdatesDifferenceEmpty date={0} seq={1}", Date, Seq);
        }
    }

    public partial class TLUpdatesDifferenceTooLong
    {
        public override TLUpdatesDifferenceBase GetEmptyObject()
        {
            return new TLUpdatesDifferenceTooLong
            {
                Pts = Pts
            };
        }

        public override string ToString()
        {
            return string.Format("TLUpdatesDifferenceTooLong pts={0}", Pts);
        }
    }

    public partial class TLUpdatesDifference
    {
        public override TLUpdatesDifferenceBase GetEmptyObject()
        {
            return new TLUpdatesDifference
            {
                NewMessages = new TLVector<TLMessageBase>(NewMessages.Count),
                NewEncryptedMessages = new TLVector<TLEncryptedMessageBase>(NewEncryptedMessages.Count),
                OtherUpdates = new TLVector<TLUpdateBase>(OtherUpdates.Count),
                Users = new TLVector<TLUserBase>(Users.Count),
                Chats = new TLVector<TLChatBase>(Chats.Count),
                State = State
            };
        }

        public void ProcessReading()
        {
            var userInbox = new Dictionary<int, TLUpdateReadHistoryInbox>();
            var userOutbox = new Dictionary<int, TLUpdateReadHistoryOutbox>();
            var chatInbox = new Dictionary<int, TLUpdateReadHistoryInbox>();
            var chatOutbox = new Dictionary<int, TLUpdateReadHistoryOutbox>();
            var channelInbox = new Dictionary<int, TLUpdateReadChannelInbox>();
            var channelOutbox = new Dictionary<int, TLUpdateReadChannelOutbox>();

            var messages = new List<TLUpdateNewMessage>();
            var channelMessages = new List<TLUpdateNewChannelMessage>();

            foreach (TLUpdateBase update in this.OtherUpdates)
            {
                if (update is TLUpdateNewChannelMessage newChannelMessage)
                {
                    channelMessages.Add(newChannelMessage);
                }
                else if (update is TLUpdateNewMessage newMessage)
                {
                    messages.Add(newMessage);
                }
                else if (update is TLUpdateReadChannelInbox readChannelInbox)
                {
                    channelInbox[readChannelInbox.ChannelId] = readChannelInbox;
                }
                else if (update is TLUpdateReadChannelOutbox readChannelOutbox)
                {
                    channelOutbox[readChannelOutbox.ChannelId] = readChannelOutbox;
                }
                else if (update is TLUpdateReadHistoryInbox readHistoryInbox)
                {
                    if (readHistoryInbox.Peer is TLPeerChat)
                    {
                        chatInbox[readHistoryInbox.Peer.Id] = readHistoryInbox;
                    }
                    else if (readHistoryInbox.Peer is TLPeerUser)
                    {
                        userInbox[readHistoryInbox.Peer.Id] = readHistoryInbox;
                    }
                }
                else if (update is TLUpdateReadHistoryOutbox readHistoryOutbox)
                {
                    if (readHistoryOutbox.Peer is TLPeerChat)
                    {
                        chatOutbox[readHistoryOutbox.Peer.Id] = readHistoryOutbox;
                    }
                    else if (readHistoryOutbox.Peer is TLPeerUser)
                    {
                        userOutbox[readHistoryOutbox.Peer.Id] = readHistoryOutbox;
                    }
                }
            }

            for (int i = 0; i < channelMessages.Count; i++)
            {
                var messageCommon = channelMessages[i].Message as TLMessageCommonBase;
                if (messageCommon != null && !IsReadMessage(messageCommon, chatOutbox, chatInbox, userOutbox, userInbox, channelOutbox, channelInbox))
                {
                    messageCommon.SetUnreadSilent(true);
                }
            }

            for (int j = 0; j < messages.Count; j++)
            {
                var messageCommon = messages[j].Message as TLMessageCommonBase;
                if (messageCommon != null && !IsReadMessage(messageCommon, chatOutbox, chatInbox, userOutbox, userInbox, channelOutbox, channelInbox))
                {
                    messageCommon.SetUnreadSilent(true);
                }
            }

            for (int k = 0; k < NewMessages.Count; k++)
            {
                var messageCommon = this.NewMessages[k] as TLMessageCommonBase;
                if (messageCommon != null && !IsReadMessage(messageCommon, chatOutbox, chatInbox, userOutbox, userInbox, channelOutbox, channelInbox))
                {
                    messageCommon.SetUnreadSilent(true);
                }
            }
        }

        private static bool IsReadMessage(TLMessageCommonBase messageCommon, Dictionary<int, TLUpdateReadHistoryOutbox> readChatOutbox, Dictionary<int, TLUpdateReadHistoryInbox> readChatInbox, Dictionary<int, TLUpdateReadHistoryOutbox> readUserOutbox, Dictionary<int, TLUpdateReadHistoryInbox> readUserInbox, Dictionary<int, TLUpdateReadChannelOutbox> readChannelOutbox, Dictionary<int, TLUpdateReadChannelInbox> readChannelInbox)
        {
            if (messageCommon.ToId is TLPeerChat)
            {
                if (messageCommon.IsOut)
                {
                    if (readChatOutbox.TryGetValue(messageCommon.ToId.Id, out TLUpdateReadHistoryOutbox update) && update.MaxId >= messageCommon.Id)
                    {
                        return true;
                    }
                }
                else if (readChatInbox.TryGetValue(messageCommon.ToId.Id, out TLUpdateReadHistoryInbox update) && update.MaxId >= messageCommon.Id)
                {
                    return true;
                }
            }
            else if (messageCommon.ToId is TLPeerUser)
            {
                if (messageCommon.IsOut)
                {
                    if (readUserOutbox.TryGetValue(messageCommon.ToId.Id, out TLUpdateReadHistoryOutbox update) && update.MaxId >= messageCommon.Id)
                    {
                        return true;
                    }
                }
                else if (readUserInbox.TryGetValue(messageCommon.FromId ?? 0, out TLUpdateReadHistoryInbox update) && update.MaxId >= messageCommon.Id)
                {
                    return true;
                }
            }
            else if (messageCommon.ToId is TLPeerChannel)
            {
                if (messageCommon.IsOut)
                {
                    if (readChannelOutbox.TryGetValue(messageCommon.ToId.Id, out TLUpdateReadChannelOutbox update) && update.MaxId >= messageCommon.Id)
                    {
                        return true;
                    }
                }
                else if (readChannelInbox.TryGetValue(messageCommon.ToId.Id, out TLUpdateReadChannelInbox update) && update.MaxId >= messageCommon.Id)
                {
                    return true;
                }
            }

            return false;
        }

        public override string ToString()
        {
            return string.Format("TLUpdatesDifference state=[{0}] messages={1} other={2} users={3} chats={4} encrypted={5}", State, NewMessages.Count, OtherUpdates.Count, Users.Count, Chats.Count, NewEncryptedMessages.Count);
        }
    }

    public partial class TLUpdatesDifferenceSlice
    {
        public override TLUpdatesDifferenceBase GetEmptyObject()
        {
            return new TLUpdatesDifferenceSlice
            {
                NewMessages = new TLVector<TLMessageBase>(NewMessages.Count),
                NewEncryptedMessages = new TLVector<TLEncryptedMessageBase>(NewEncryptedMessages.Count),
                OtherUpdates = new TLVector<TLUpdateBase>(OtherUpdates.Count),
                Users = new TLVector<TLUserBase>(Users.Count),
                Chats = new TLVector<TLChatBase>(Chats.Count),
                IntermediateState = IntermediateState
            };
        }
    }
}
