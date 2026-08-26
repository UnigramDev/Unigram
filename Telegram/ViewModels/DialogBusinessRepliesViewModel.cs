//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Collections;
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
                var message = new Message(x.Id, new MessageSenderUser(ClientService.Options.MyId), null, ClientService.Options.MyId, x.SendingState, null, true, false, false, false, false, false, false, false, false, false, 0, 0, null, null, null, null, null, null, null, null, null, 0, 0, x.ViaBotUserId, null, 0, 0, string.Empty, 0, string.Empty, x.MediaAlbumId, 0, null, string.Empty, x.Content, null, x.ReplyMarkup);
                var model = new QuickReplyMessageViewModel(ClientService, _messageDelegateWeak, _chat, message, true)
                {
                    CanBeEdited = x.CanBeEdited
                };

                return model as MessageViewModel;
            }).ToList();

            BeginOnUIThread(() =>
            {
                ProcessMessages(chat, replied);

                var diff = DiffCalculator.Create(Items, replied, this);

                while (diff.Next())
                {
                    if (diff.State == DiffState.Add)
                    {
                        Items.Insert(diff.NewIndex, diff.NewValue);
                    }
                    else if (diff.State == DiffState.Move && diff.OldIndex < Items.Count && diff.NewIndex < Items.Count)
                    {
                        Items.Move(diff.OldIndex, diff.NewIndex);
                    }
                    else if (diff.State == DiffState.Remove && diff.OldIndex < Items.Count)
                    {
                        Items.RemoveAt(diff.OldIndex);
                    }
                    else if (diff.State == DiffState.Unchanged)
                    {
                        // Copied out first: the walk is a ref struct and cannot be captured.
                        var oldValue = diff.OldValue;

                        UpdateItem(oldValue, diff.NewValue);

                        Delegate?.UpdateBubbleWithMessageId(oldValue.Id, bubble => bubble.UpdateMessage(oldValue));
                    }
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
