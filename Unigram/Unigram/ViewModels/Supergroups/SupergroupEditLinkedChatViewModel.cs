using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Supergroups
{
    public class SupergroupEditLinkedChatViewModel : TLViewModelBase
        , IDelegable<ISupergroupDelegate>
        , IHandle
        //, IHandle<UpdateSupergroup>
        //, IHandle<UpdateSupergroupFullInfo>
    {
        public ISupergroupDelegate Delegate { get; set; }

        public SupergroupEditLinkedChatViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<Chat>();

            LinkCommand = new RelayCommand<Chat>(LinkExecute);
            UnlinkCommand = new RelayCommand(UnlinkExecute);
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

        public RelayCommand<Chat> LinkCommand { get; }
        private async void LinkExecute(Chat linkedChat)
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
                    if (linkedFullInfo == null)
                    {
                        linkedFullInfo = await ClientService.SendAsync(new GetSupergroupFullInfo(linkedSupergroup.Id)) as SupergroupFullInfo;
                    }

                    if (linkedSupergroup == null)
                    {
                        return;
                    }

                    if (string.IsNullOrEmpty(linkedSupergroup.Username))
                    {
                        message = string.Format(Strings.Resources.DiscussionLinkGroupPublicPrivateAlert, linkedChat.Title, chat.Title);
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(supergroup.Username))
                        {
                            message = string.Format(Strings.Resources.DiscussionLinkGroupPrivateAlert, linkedChat.Title, chat.Title);
                        }
                        else
                        {
                            message = string.Format(Strings.Resources.DiscussionLinkGroupPublicAlert, linkedChat.Title, chat.Title);
                        }
                    }

                    if (!linkedFullInfo.IsAllHistoryAvailable)
                    {
                        message += "\r\n\r\n" + Strings.Resources.DiscussionLinkGroupAlertHistory;
                        history = true;
                    }
                }
                else
                {
                    message = string.Format(Strings.Resources.DiscussionLinkGroupPublicPrivateAlert, linkedChat.Title, chat.Title);
                    history = true;
                }

                var confirm = await MessagePopup.ShowAsync(message, Strings.Resources.DiscussionLinkGroup, Strings.Resources.DiscussionLinkGroup, Strings.Resources.Cancel);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                if (linkedChat.Type is ChatTypeBasicGroup)
                {
                    linkedChat = await ClientService.SendAsync(new UpgradeBasicGroupChatToSupergroupChat(linkedChat.Id)) as Chat;
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

        public RelayCommand UnlinkCommand { get; }
        private async void UnlinkExecute()
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

            var confirm = await MessagePopup.ShowAsync(string.Format(Strings.Resources.DiscussionUnlinkChannelAlert, linkedChat.Title), Strings.Resources.DiscussionUnlinkGroup, Strings.Resources.DiscussionUnlink, Strings.Resources.Cancel);
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
    }
}
