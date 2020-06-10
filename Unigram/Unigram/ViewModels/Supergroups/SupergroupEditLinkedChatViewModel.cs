using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Supergroups
{
    public class SupergroupEditLinkedChatViewModel : TLViewModelBase,
        IDelegable<ISupergroupDelegate>,
        IHandle<UpdateSupergroup>,
        IHandle<UpdateSupergroupFullInfo>
    {
        public ISupergroupDelegate Delegate { get; set; }

        public SupergroupEditLinkedChatViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<Chat>();

            LinkCommand = new RelayCommand<Chat>(LinkExecute);
            UnlinkCommand = new RelayCommand(UnlinkExecute);
        }

        protected Chat _chat;
        public Chat Chat
        {
            get
            {
                return _chat;
            }
            set
            {
                Set(ref _chat, value);
            }
        }

        public MvxObservableCollection<Chat> Items { get; private set; }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var chatId = (long)parameter;

            Chat = ProtoService.GetChat(chatId);

            var chat = _chat;
            if (chat == null)
            {
                return Task.CompletedTask;
            }

            Aggregator.Subscribe(this);
            Delegate?.UpdateChat(chat);

            if (chat.Type is ChatTypeSupergroup super)
            {
                var item = ProtoService.GetSupergroup(super.SupergroupId);
                var cache = ProtoService.GetSupergroupFull(super.SupergroupId);

                UpdateSupergroup(chat, item);

                if (cache == null)
                {
                    ProtoService.Send(new GetSupergroupFullInfo(super.SupergroupId));
                }
                else
                {
                    UpdateSupergroupFullInfo(chat, item, cache);
                }
            }

            return Task.CompletedTask;
        }

        public override Task OnNavigatedFromAsync(IDictionary<string, object> pageState, bool suspending)
        {
            Aggregator.Unsubscribe(this);
            return Task.CompletedTask;
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
                BeginOnUIThread(() => UpdateSupergroupFullInfo(chat, ProtoService.GetSupergroup(update.SupergroupId), update.SupergroupFullInfo));
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
                var linkedChat = CacheService.GetChat(fullInfo.LinkedChatId);
                if (linkedChat != null)
                {
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
            var response = await ProtoService.SendAsync(new GetSuitableDiscussionChats());
            if (response is Telegram.Td.Api.Chats chats)
            {
                var result = new List<Chat>();

                foreach (var id in chats.ChatIds)
                {
                    var linkedChat = CacheService.GetChat(id);
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

            var supergroup = CacheService.GetSupergroup(chat);
            if (supergroup == null)
            {
                return;
            }

            var fullInfo = CacheService.GetSupergroupFull(chat);
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
                String message;
                bool history = false;
                if (CacheService.TryGetSupergroup(linkedChat, out Supergroup linkedSupergroup))
                {
                    var linkedFullInfo = CacheService.GetSupergroupFull(linkedChat);
                    if (linkedFullInfo == null)
                    {
                        linkedFullInfo = await ProtoService.SendAsync(new GetSupergroupFullInfo(linkedSupergroup.Id)) as SupergroupFullInfo;
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
                    linkedChat = await ProtoService.SendAsync(new UpgradeBasicGroupChatToSupergroupChat(linkedChat.Id)) as Chat;
                }

                if (linkedChat == null)
                {
                    return;
                }

                if (history && linkedChat.Type is ChatTypeSupergroup super)
                {
                    await ProtoService.SendAsync(new ToggleSupergroupIsAllHistoryAvailable(super.SupergroupId, true));
                }

                var response = await ProtoService.SendAsync(new SetChatDiscussionGroup(chat.Id, linkedChat.Id));
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

            var supergroup = CacheService.GetSupergroup(chat);
            if (supergroup == null)
            {
                return;
            }

            var fullInfo = CacheService.GetSupergroupFull(chat);
            if (fullInfo == null)
            {
                return;
            }

            var linkedChat = CacheService.GetChat(fullInfo.LinkedChatId);
            if (linkedChat == null)
            {
                return;
            }

            var confirm = await MessagePopup.ShowAsync(string.Format(Strings.Resources.DiscussionUnlinkChannelAlert, linkedChat.Title), Strings.Resources.DiscussionUnlinkGroup, Strings.Resources.DiscussionUnlink, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var response = await ProtoService.SendAsync(new SetChatDiscussionGroup(supergroup.IsChannel ? chat.Id : linkedChat.Id, 0));
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
