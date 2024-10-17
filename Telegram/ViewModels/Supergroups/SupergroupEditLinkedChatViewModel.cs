//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.Views.Create;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Supergroups
{
    public partial class SupergroupEditLinkedChatViewModel : SupergroupViewModelBase, IDelegable<ISupergroupDelegate>, IHandle
    {
        public ISupergroupDelegate Delegate { get; set; }

        public SupergroupEditLinkedChatViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<Chat>();
        }

        protected Chat _chat;
        public Chat Chat
        {
            get => _chat;
            set => Set(ref _chat, value);
        }

        private bool _joinToSendMessages;
        public bool JoinToSendMessages
        {
            get => _joinToSendMessages;
            set => Set(ref _joinToSendMessages, value);
        }

        private bool _joinByRequest;
        public bool JoinByRequest
        {
            get => _joinByRequest;
            set => Set(ref _joinByRequest, value);
        }

        public MvxObservableCollection<Chat> Items { get; private set; }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var chatId = (long)parameter;

            Chat = ClientService.GetChat(chatId);

            var chat = _chat;
            if (chat == null)
            {
                return Task.CompletedTask;
            }

            Delegate?.UpdateChat(chat);

            if (chat.Type is ChatTypeSupergroup super)
            {
                var item = ClientService.GetSupergroup(super.SupergroupId);
                var cache = ClientService.GetSupergroupFull(super.SupergroupId);

                UpdateSupergroup(chat, item);

                if (cache == null)
                {
                    ClientService.Send(new GetSupergroupFullInfo(super.SupergroupId));
                }
                else
                {
                    UpdateSupergroupFullInfo(chat, item, cache);
                }
            }

            return Task.CompletedTask;
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateSupergroup>(this, Handle)
                .Subscribe<UpdateSupergroupFullInfo>(Handle);
        }

        public void Handle(UpdateSupergroup update)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup super && super.SupergroupId == update.Supergroup.Id)
            {
                BeginOnUIThread(() => UpdateSupergroup(chat, update.Supergroup));
            }
        }

        public void Handle(UpdateSupergroupFullInfo update)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup super && super.SupergroupId == update.SupergroupId)
            {
                BeginOnUIThread(() => UpdateSupergroupFullInfo(chat, ClientService.GetSupergroup(update.SupergroupId), update.SupergroupFullInfo));
            }
        }

        private void UpdateSupergroup(Chat chat, Supergroup group)
        {
            Delegate?.UpdateSupergroup(chat, group);
        }

        private void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            Delegate?.UpdateSupergroupFullInfo(chat, group, fullInfo);

            if (fullInfo.LinkedChatId != 0)
            {
                var linkedChat = ClientService.GetChat(fullInfo.LinkedChatId);
                if (linkedChat != null)
                {
                    if (ClientService.TryGetSupergroup(linkedChat, out Supergroup linkedSupergroup))
                    {
                        JoinToSendMessages = linkedSupergroup.JoinToSendMessages;
                        JoinByRequest = linkedSupergroup.JoinByRequest;
                    }

                    Items.ReplaceWith(new[] { linkedChat });
                }
                else
                {
                    LoadSuitableChats();
                }
            }
            else
            {
                LoadSuitableChats();
            }
        }

        private async void LoadSuitableChats()
        {
            var response = await ClientService.SendAsync(new GetSuitableDiscussionChats());
            if (response is Telegram.Td.Api.Chats chats)
            {
                var result = new List<Chat>();

                foreach (var id in chats.ChatIds)
                {
                    var linkedChat = ClientService.GetChat(id);
                    if (linkedChat != null)
                    {
                        result.Add(linkedChat);
                    }
                }

                Items.ReplaceWith(result);
            }
        }

        public async void Link(Chat linkedChat)
        {
            var chat = _chat;
            if (chat == null || linkedChat == null)
            {
                return;
            }

            var supergroup = ClientService.GetSupergroup(chat);
            if (supergroup == null)
            {
                return;
            }

            var fullInfo = ClientService.GetSupergroupFull(chat);
            if (fullInfo == null)
            {
                return;
            }

            if (fullInfo.LinkedChatId == linkedChat.Id)
            {
                NavigationService.NavigateToChat(linkedChat);
            }
            else
            {
                string message;
                bool history = false;
                if (ClientService.TryGetSupergroup(linkedChat, out Supergroup linkedSupergroup))
                {
                    var linkedFullInfo = ClientService.GetSupergroupFull(linkedChat);
                    linkedFullInfo ??= await ClientService.SendAsync(new GetSupergroupFullInfo(linkedSupergroup.Id)) as SupergroupFullInfo;

                    if (linkedSupergroup == null || linkedFullInfo == null)
                    {
                        return;
                    }

                    if (linkedSupergroup.HasActiveUsername())
                    {
                        if (supergroup.HasActiveUsername())
                        {
                            message = string.Format(Strings.DiscussionLinkGroupPublicAlert, linkedChat.Title, chat.Title);
                        }
                        else
                        {
                            message = string.Format(Strings.DiscussionLinkGroupPrivateAlert, linkedChat.Title, chat.Title);
                        }
                    }
                    else
                    {
                        message = string.Format(Strings.DiscussionLinkGroupPublicPrivateAlert, linkedChat.Title, chat.Title);
                    }

                    if (!linkedFullInfo.IsAllHistoryAvailable)
                    {
                        message += "\r\n\r\n" + Strings.DiscussionLinkGroupAlertHistory;
                        history = true;
                    }
                }
                else
                {
                    message = string.Format(Strings.DiscussionLinkGroupPublicPrivateAlert, linkedChat.Title, chat.Title);
                    history = true;
                }

                var confirm = await ShowPopupAsync(message, Strings.DiscussionLinkGroup, Strings.DiscussionLinkGroup, Strings.Cancel);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                if (linkedChat.Type is ChatTypeBasicGroup)
                {
                    linkedChat = await UpgradeAsync(linkedChat);
                }

                if (linkedChat == null)
                {
                    return;
                }

                if (history && linkedChat.Type is ChatTypeSupergroup super)
                {
                    await ClientService.SendAsync(new ToggleSupergroupIsAllHistoryAvailable(super.SupergroupId, true));
                }

                var response = await ClientService.SendAsync(new SetChatDiscussionGroup(chat.Id, linkedChat.Id));
                if (response is Ok)
                {
                    NavigationService.GoBack();
                    NavigationService.Frame.ForwardStack.Clear();
                }
            }
        }

        public async void Unlink()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var supergroup = ClientService.GetSupergroup(chat);
            if (supergroup == null)
            {
                return;
            }

            var fullInfo = ClientService.GetSupergroupFull(chat);
            if (fullInfo == null)
            {
                return;
            }

            var linkedChat = ClientService.GetChat(fullInfo.LinkedChatId);
            if (linkedChat == null)
            {
                return;
            }

            var confirm = await ShowPopupAsync(string.Format(Strings.DiscussionUnlinkChannelAlert, linkedChat.Title), Strings.DiscussionUnlinkGroup, Strings.DiscussionUnlink, Strings.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var response = await ClientService.SendAsync(new SetChatDiscussionGroup(supergroup.IsChannel ? chat.Id : linkedChat.Id, 0));
            if (response is Ok && !supergroup.IsChannel)
            {
                NavigationService.GoBack();
                NavigationService.Frame.ForwardStack.Clear();
            }
            else
            {

            }
        }

        public async void Create()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var completion = new TaskCompletionSource<Chat>();

            var confirm = await ShowPopupAsync(new NewGroupPopup(completion, string.Format(Strings.GroupCreateDiscussionDefaultName, chat.Title)));
            if (confirm == ContentDialogResult.Primary)
            {
                var linkedChat = await completion.Task;
                if (linkedChat != null)
                {
                    if (linkedChat.Type is ChatTypeSupergroup supergroup)
                    {
                        await ClientService.SendAsync(new ToggleSupergroupIsAllHistoryAvailable(supergroup.SupergroupId, true));
                    }

                    var response = await ClientService.SendAsync(new SetChatDiscussionGroup(chat.Id, linkedChat.Id));
                    if (response is Ok)
                    {
                        NavigationService.GoBack();
                        NavigationService.Frame.ForwardStack.Clear();
                    }
                }
            }
        }
    }
}
