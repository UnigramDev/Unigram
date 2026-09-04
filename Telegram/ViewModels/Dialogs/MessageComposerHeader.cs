//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using System.Linq;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.ViewModels
{
    public record MessageComposerReplyTo(MessageViewModel Message, InputTextQuote Quote, int ChecklistTaskId, string PollOptionId, bool CanBeRepliedInAnotherChat)
    {
        public InputMessageReplyTo ToInput(DialogViewModel viewModel)
        {
            var sameTopic = (viewModel.TopicId == null || viewModel.Thread != null) || (viewModel.TopicId != null && Message.TopicId.AreTheSame(viewModel.TopicId));

            var chatId = Message.ChatId;
            if (chatId == viewModel.ChatId && Message.ReceiverId != null && Message.SenderId is MessageSenderUser senderUser)
            {
                return new InputMessageReplyToEphemeralMessage(Message.Id, senderUser.UserId, Quote, ChecklistTaskId, PollOptionId);
            }
            else if (chatId == viewModel.ChatId && sameTopic)
            {
                if (Message.TopicId != null && (viewModel.IsForum || viewModel.IsDirectMessagesGroup))
                {
                    // TODO: this should no longer be needed
                    //if (Message.TopicId.IsForum(ForumTopicService.GeneralId))
                    //{
                    //    return new InputMessageReplyToTopicMessage(Message.Id, new MessageTopicForum(Message.MessageThreadId), Quote, ChecklistTaskId);
                    //}

                    return new InputMessageReplyToTopicMessage(Message.Id, Message.TopicId, Quote, ChecklistTaskId, PollOptionId);
                }

                return new InputMessageReplyToMessage(Message.Id, Quote, ChecklistTaskId, PollOptionId);
            }

            return new InputMessageReplyToExternalMessage(chatId, Message.Id, Quote, ChecklistTaskId, PollOptionId);
        }
    }

    public record MessageComposerEditing(MessageViewModel Message, InputMessageContent Media);

    /// <summary>
    /// One message staged for forwarding: the message itself, which the composer header previews,
    /// and the two properties the copy options are offered from. <see cref="MessageProperties"/> is
    /// a request of its own, so whoever stages the forward is the one that has already asked.
    /// </summary>
    public record MessageComposerForwarded(MessageViewModel Message, bool CanBeCopied, bool HasCaption);

    /// <summary>
    /// What the composer is holding to forward. Only the composer's own send carries it — after
    /// whatever the user typed, and counting as one message per entry when they are asked to pay.
    /// Every other send leaves it staged, and so does committing an edit.
    /// </summary>
    /// <remarks>
    /// A draft has no room for any of this — TDLib has no field for it — so a staged forward lives
    /// only as long as the composer does.
    /// </remarks>
    public partial class MessageComposerForwarding
    {
        public MessageComposerForwarding(IList<MessageComposerForwarded> messages)
        {
            Messages = messages;
        }

        public IList<MessageComposerForwarded> Messages { get; }

        /// <summary>
        /// Drops the sender names, turning the forward into a copy. Written through the live
        /// instance by the header's flyout, the way the link preview options are.
        /// </summary>
        public bool SendCopy { get; set; }

        public bool RemoveCaption { get; set; }

        public bool CanBeCopied => Messages.Any(x => x.CanBeCopied);

        public bool CanRemoveCaption => Messages.Any(x => x.CanBeCopied && x.HasCaption);
    }

    public partial class MessageComposerHeader
    {
        public IClientService ClientService { get; }

        public MessageComposerHeader(IClientService clientService)
        {
            ClientService = clientService;
        }

        public MessageComposerReplyTo ReplyTo { get; set; }

        public MessageComposerEditing Editing { get; set; }

        public MessageComposerForwarding Forwarding { get; set; }

        public InputSuggestedPostInfo SuggestedPostInfo { get; set; }

        public LinkPreview LinkPreview { get; set; }
        public string LinkPreviewUrl { get; set; }

        public bool LinkPreviewDisabled
        {
            get => LinkPreviewOptions?.IsDisabled ?? false;
            set
            {
                if (LinkPreviewOptions == null && !value)
                {
                    return;
                }

                LinkPreviewOptions ??= new();
                LinkPreviewOptions.IsDisabled = value;
            }
        }

        private LinkPreviewOptions _linkPreviewOptions = new();
        public LinkPreviewOptions LinkPreviewOptions
        {
            get => _linkPreviewOptions;
            set
            {
                if (value != null)
                {
                    _linkPreviewOptions = value;
                }
            }
        }

        public bool IsEmpty
        {
            get
            {
                return ReplyTo == null && Editing == null && Forwarding == null && SuggestedPostInfo == null;
            }
        }

        public bool Matches(long messageId)
        {
            if (ReplyTo?.Message?.Id == messageId)
            {
                return true;
            }
            else if (Editing?.Message?.Id == messageId)
            {
                return true;
            }

            return false;
        }
    }
}
