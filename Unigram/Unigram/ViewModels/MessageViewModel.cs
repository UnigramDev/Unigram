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
    }
}
