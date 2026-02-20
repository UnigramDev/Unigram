//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.ViewModels
{
    public record MessageComposerReplyTo(MessageViewModel Message, InputTextQuote Quote, int ChecklistTaskId, bool CanBeRepliedInAnotherChat)
    {
        public InputMessageReplyTo ToInput(DialogViewModel viewModel)
        {
            var sameTopic = (viewModel.TopicId == null || viewModel.Thread != null) || (viewModel.TopicId != null && Message.TopicId.AreTheSame(viewModel.TopicId));

            var chatId = Message.ChatId;
            if (chatId == viewModel.ChatId && sameTopic)
            {
                if (Message.TopicId != null && (viewModel.IsForum || viewModel.IsDirectMessagesGroup))
                {
                    // TODO: this should no longer be needed
                    //if (Message.TopicId.IsForum(ForumTopicService.GeneralId))
                    //{
                    //    return new InputMessageReplyToTopicMessage(Message.Id, new MessageTopicForum(Message.MessageThreadId), Quote, ChecklistTaskId);
                    //}

                    return new InputMessageReplyToTopicMessage(Message.Id, Message.TopicId, Quote, ChecklistTaskId);
                }

                return new InputMessageReplyToMessage(Message.Id, Quote, ChecklistTaskId);
            }

            return new InputMessageReplyToExternalMessage(chatId, Message.Id, Quote, ChecklistTaskId);
        }
    }

    public record MessageComposerEditing(MessageViewModel Message, InputMessageContent Media);

    public partial class MessageComposerHeader
    {
        public IClientService ClientService { get; }

        public MessageComposerHeader(IClientService clientService)
        {
            ClientService = clientService;
        }

        public MessageComposerReplyTo ReplyTo { get; set; }

        public MessageComposerEditing Editing { get; set; }

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
                return ReplyTo == null && Editing == null && SuggestedPostInfo == null;
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
