using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.ViewModels.Delegates;

namespace Unigram.ViewModels
{
    public class MessageViewModel
    {
        private readonly IProtoService _protoService;
        private readonly IMessageDelegate _delegate;

        private Message _message;

        public MessageViewModel(IProtoService protoService, IMessageDelegate delegato, Message message)
        {
            _protoService = protoService;
            _delegate = delegato;

            _message = message;
        }

        public MessageViewModel(Message message)
        {
            _message = message;
        }

        public IProtoService ProtoService => _protoService;
        public IMessageDelegate Delegate => _delegate;

        public bool IsFirst { get; set; }
        public bool IsLast { get; set; }

        public DateTime ContentOpenedAt { get; set; }

        public ReplyMarkup ReplyMarkup { get => _message.ReplyMarkup; set => _message.ReplyMarkup = value; }
        public MessageContent Content { get => _message.Content; set => _message.Content = value; }
        public long MediaAlbumId => _message.MediaAlbumId;
        public int Views { get => _message.Views; set => _message.Views = value; }
        public string AuthorSignature => _message.AuthorSignature;
        public int ViaBotUserId => _message.ViaBotUserId;
        public double TtlExpiresIn { get => _message.TtlExpiresIn; set => _message.TtlExpiresIn = value; }
        public int Ttl => _message.Ttl;
        public long ReplyToMessageId => _message.ReplyToMessageId;
        public MessageForwardInfo ForwardInfo => _message.ForwardInfo;
        public int EditDate { get => _message.EditDate; set => _message.EditDate = value; }
        public int Date => _message.Date;
        public bool ContainsUnreadMention { get => _message.ContainsUnreadMention; set => _message.ContainsUnreadMention = value; }
        public bool IsChannelPost => _message.IsChannelPost;
        public bool CanBeDeletedForAllUsers => _message.CanBeDeletedForAllUsers;
        public bool CanBeDeletedOnlyForSelf => _message.CanBeDeletedOnlyForSelf;
        public bool CanBeForwarded => _message.CanBeForwarded;
        public bool CanBeEdited => _message.CanBeEdited;
        public bool IsOutgoing { get => _message.IsOutgoing; set => _message.IsOutgoing = value; }
        public MessageSendingState SendingState => _message.SendingState;
        public long ChatId => _message.ChatId;
        public int SenderUserId => _message.SenderUserId;
        public long Id => _message.Id;

        public Photo GetPhoto() => _message.GetPhoto();
        public File GetAnimation() => _message.GetAnimation();
        public File GetFile() => _message.GetFile();

        public bool IsService() => _message.IsService();
        public bool IsSaved() => _message.IsSaved();
        public bool IsSecret() => _message.IsSecret();

        public MessageViewModel ReplyToMessage { get; set; }
        public ReplyToMessageState ReplyToMessageState { get; set; } = ReplyToMessageState.None;

        public User GetSenderUser()
        {
            return ProtoService.GetUser(_message.SenderUserId);
        }

        public User GetViaBotUser()
        {
            return ProtoService.GetUser(_message.ViaBotUserId);
        }

        public Chat GetChat()
        {
            return ProtoService.GetChat(_message.ChatId);
        }

        public Message Get()
        {
            return _message;
        }

        public void Replace(Message message)
        {
            _message = message;
        }

        public bool UpdateFile(File file)
        {
            var message = _message.UpdateFile(file);

            var reply = ReplyToMessage;
            if (reply != null)
            {
                return reply.UpdateFile(file) || message;
            }

            return message;
        }

        public bool IsShareable()
        {
            var message = this;
            //if (currentPosition != null && !currentPosition.last)
            //{
            //    return false;
            //}
            //else if (messageObject.eventId != 0)
            //{
            //    return false;
            //}
            //else if (messageObject.messageOwner.fwd_from != null && !messageObject.isOutOwner() && messageObject.messageOwner.fwd_from.saved_from_peer != null && messageObject.getDialogId() == UserConfig.getInstance(currentAccount).getClientUserId())
            //{
            //    drwaShareGoIcon = true;
            //    return true;
            //}
            //else 
            if (message.Content is MessageSticker)
            {
                return false;
            }
            //else if (messageObject.messageOwner.fwd_from != null && messageObject.messageOwner.fwd_from.channel_id != 0 && !messageObject.isOutOwner())
            //{
            //    return true;
            //}
            else if (message.SenderUserId != 0)
            {
                if (message.Content is MessageText)
                {
                    return false;
                }

                var user = message.GetSenderUser();
                if (user != null && user.Type is UserTypeBot)
                {
                    return true;
                }
                if (!message.IsOutgoing)
                {
                    if (message.Content is MessageGame || message.Content is MessageInvoice)
                    {
                        return true;
                    }

                    var chat = message.ProtoService.GetChat(message.ChatId);
                    if (chat != null && chat.Type is ChatTypeSupergroup super && !super.IsChannel)
                    {
                        var supergroup = message.ProtoService.GetSupergroup(super.SupergroupId);
                        return supergroup != null && supergroup.Username.Length > 0 && !(message.Content is MessageContact) && !(message.Content is MessageLocation);
                    }
                }
            }
            //else if (messageObject.messageOwner.from_id < 0 || messageObject.messageOwner.post)
            //{
            //    if (messageObject.messageOwner.to_id.channel_id != 0 && (messageObject.messageOwner.via_bot_id == 0 && messageObject.messageOwner.reply_to_msg_id == 0 || messageObject.type != 13))
            //    {
            //        return true;
            //    }
            //}
            else if (message.IsChannelPost)
            {
                if (message.ViaBotUserId == 0 && message.ReplyToMessageId == 0 || !(message.Content is MessageSticker))
                {
                    return true;
                }
            }

            return false;
        }

    }
}
