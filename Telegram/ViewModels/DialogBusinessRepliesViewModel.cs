//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Rg.DiffUtils;
using System;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.ViewModels
{
    public partial class QuickReplyMessageViewModel : MessageViewModel
    {
        public QuickReplyMessageViewModel(IClientService clientService, WeakReference delegato, Chat chat, Message message, bool processText = false)
            : base(clientService, delegato, chat, null, null, message, processText)
        {
        }

        public bool CanBeEdited { get; set; }
    }

    public partial class DialogBusinessRepliesViewModel : DialogViewModel, IDiffHandler<MessageViewModel>
    {
        public DialogBusinessRepliesViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, ILocationService locationService, INotificationsService pushService, IVoipService voipService, INetworkService networkService, IStorageService storageService, ITranslateService translateService)
            : base(clientService, settingsService, aggregator, locationService, pushService, voipService, networkService, storageService, translateService)
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
            IsNewestSliceLoaded = true;
            IsOldestSliceLoaded = true;

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
                var message = new Message(x.Id, new MessageSenderUser(ClientService.Options.MyId), ClientService.Options.MyId, x.SendingState, null, true, false, false, false, false, false, false, false, false, 0, 0, null, null, null, null, null, null, null, null, null, 0, 0, x.ViaBotUserId, 0, 0, 0, string.Empty, x.MediaAlbumId, 0, null, string.Empty, x.Content, x.ReplyMarkup);
                var model = new QuickReplyMessageViewModel(ClientService, _messageDelegateWeak, _chat, message, true)
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

                IsNewestSliceLoaded = true;
                IsOldestSliceLoaded = true;
            });
        }

        public bool CompareItems(MessageViewModel oldItem, MessageViewModel newItem)
        {
            return oldItem.Id == newItem.Id;
        }

        public void UpdateItem(MessageViewModel oldItem, MessageViewModel newItem)
        {
            oldItem.Replace(newItem);
            oldItem.Content = newItem.Content;
        }
    }
}
