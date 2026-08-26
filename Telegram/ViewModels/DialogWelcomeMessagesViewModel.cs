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
using Telegram.Common;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.ViewModels
{
    public partial class WelcomeMessageViewModel : MessageViewModel
    {
        public WelcomeMessageViewModel(IClientService clientService, WeakReference delegato, Chat chat, Message message, bool processText = false)
            : base(clientService, delegato, chat, null, null, message, processText)
        {
        }
    }

    public partial class DialogWelcomeMessagesViewModel : DialogViewModel, IDiffHandler<MessageViewModel>
    {
        public DialogWelcomeMessagesViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, ILocationService locationService, INotificationsService pushService, IVoipService voipService, INetworkService networkService, IStorageService storageService, ITranslateService translateService)
            : base(clientService, settingsService, aggregator, locationService, pushService, voipService, networkService, storageService, translateService)
        {
        }

        public override DialogType Type => DialogType.WelcomeMessages;

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateChatWelcomeMessages>(this, Handle);
            base.Subscribe();
        }

        public override async Task LoadWelcomeMessageSliceAsync()
        {
            IsNewestSliceLoaded = true;
            IsOldestSliceLoaded = true;

            Handle(new UpdateChatWelcomeMessages(ChatId, ClientService.GetWelcomeMessages(ChatId)));

            var response = await ClientService.SendAsync(new LoadChatWelcomeMessages(ChatId));
        }

        private void Handle(UpdateChatWelcomeMessages update)
        {
            var chat = Chat;
            if (chat == null)
            {
                return;
            }

            var replied = update.Messages.OrderBy(x => x.Id).Select(x =>
            {
                var message = new Message(x.Id, new MessageSenderChat(ChatId), new MessageSenderUser(ClientService.Options.MyId), ChatId, null, null, false, false, false, false, false, false, false, false, false, false, 0, 0, null, null, null, null, null, null, null, null, null, 0, 0, 0, null, 0, 0, string.Empty, 0, string.Empty, 0, 0, null, string.Empty, x.Content, null, null);
                var model = new WelcomeMessageViewModel(ClientService, _messageDelegateWeak, _chat, message, true);

                return model as MessageViewModel;
            }).ToList();

            if (replied.Count > 0)
            {
                replied.Insert(0, new WelcomeMessageViewModel(ClientService, _messageDelegateWeak, _chat, new Message(0, new MessageSenderChat(chat.Id), null, chat.Id, null, null, false, false, false, false, false, false, false, false, false, false, 0, 0, null, null, null, null, null, null, null, null, null, 0, 0, 0, null, 0, 0, string.Empty, 0, string.Empty, 0, 0, null, string.Empty, new MessageCustomServiceAction(Locale.Declension(Strings.R.WelcomeMessageHint, replied.Count)), null, null)));
            }

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
