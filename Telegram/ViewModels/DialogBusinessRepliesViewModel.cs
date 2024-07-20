//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Rg.DiffUtils;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Services;
using Telegram.Services.Factories;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;

namespace Telegram.ViewModels
{
    public class QuickReplyMessageViewModel : MessageViewModel
    {
        public QuickReplyMessageViewModel(IClientService clientService, IPlaybackService playbackService, IMessageDelegate delegato, Chat chat, Message message, bool processText = false)
            : base(clientService, playbackService, delegato, chat, message, processText)
        {
        }

        public bool CanBeEdited { get; set; }
    }

    public class DialogBusinessRepliesViewModel : DialogViewModel, IDiffHandler<MessageViewModel>
    {
        public DialogBusinessRepliesViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, ILocationService locationService, INotificationsService pushService, IPlaybackService playbackService, IVoipService voipService, IVoipGroupService voipGroupService, INetworkService networkService, IStorageService storageService, ITranslateService translateService, IMessageFactory messageFactory)
            : base(clientService, settingsService, aggregator, locationService, pushService, playbackService, voipService, voipGroupService, networkService, storageService, translateService, messageFactory)
        {
        }

        public override DialogType Type => DialogType.BusinessReplies;

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateQuickReplyShortcutMessages>(this, Handle);
            base.Subscribe();
        }

        public override async Task LoadQuickReplyShortcutSliceAsync()
        {
            IsFirstSliceLoaded = true;
            IsLastSliceLoaded = true;

            Handle(new UpdateQuickReplyShortcutMessages(QuickReplyShortcut.Id, ClientService.GetQuickReplyMessages(QuickReplyShortcut.Id)));

            var response = await ClientService.SendAsync(new LoadQuickReplyShortcutMessages(QuickReplyShortcut.Id));
        }

        private void Handle(UpdateQuickReplyShortcutMessages update)
        {
            var chat = Chat;
            if (chat == null)
            {
                return;
            }

            var replied = update.Messages.OrderBy(x => x.Id).Select(x =>
            {
                var message = new Message(x.Id, new MessageSenderUser(ClientService.Options.MyId), ClientService.Options.MyId, x.SendingState, null, true, false, false, false, false, false, false, false, 0, 0, null, null, null, null, null, null, 0, 0, null, 0, 0, x.ViaBotUserId, 0, 0, string.Empty, x.MediaAlbumId, 0, string.Empty, x.Content, x.ReplyMarkup);
                var model = new QuickReplyMessageViewModel(ClientService, PlaybackService, _messageDelegate, _chat, message, true)
                {
                    CanBeEdited = x.CanBeEdited
                };

                return model as MessageViewModel;
            }).ToList();

            BeginOnUIThread(() =>
            {
                ProcessMessages(chat, replied);

                var diff = DiffUtil.CalculateDiff(Items, replied, this, Constants.DiffOptions);

                foreach (var step in diff.Steps)
                {
                    if (step.Status == DiffStatus.Add)
                    {
                        Items.Insert(step.NewStartIndex, step.Items[0].NewValue);
                    }
                    else if (step.Status == DiffStatus.Move && step.OldStartIndex < Items.Count && step.NewStartIndex < Items.Count)
                    {
                        Items.Move(step.OldStartIndex, step.NewStartIndex);
                    }
                    else if (step.Status == DiffStatus.Remove && step.OldStartIndex < Items.Count)
                    {
                        Items.RemoveAt(step.OldStartIndex);
                    }
                }

                foreach (var item in diff.NotMovedItems)
                {
                    UpdateItem(item.OldValue, item.NewValue);

                    Delegate?.UpdateBubbleWithMessageId(item.OldValue.Id, bubble => bubble.UpdateMessage(item.OldValue));
                }

                IsFirstSliceLoaded = true;
                IsLastSliceLoaded = true;
            });
        }

        public bool CompareItems(MessageViewModel oldItem, MessageViewModel newItem)
        {
            return oldItem.Id == newItem.Id;
        }

        public void UpdateItem(MessageViewModel oldItem, MessageViewModel newItem)
        {
            oldItem.UpdateWith(newItem);
        }
    }
}
