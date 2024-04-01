//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Rg.DiffUtils;
using System;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Supergroups
{
    public class SupergroupMembersViewModelBase : SupergroupViewModelBase, IDelegable<ISupergroupDelegate>
    {
        private readonly SupergroupMembersFilter _filter;
        private readonly Func<string, SupergroupMembersFilter> _find;

        private int _memberCount;

        public ISupergroupDelegate Delegate { get; set; }

        public SupergroupMembersViewModelBase(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, SupergroupMembersFilter filter, Func<string, SupergroupMembersFilter> search)
            : base(clientService, settingsService, aggregator)
        {
            _filter = filter;
            _find = search;

            Members = new SearchCollection<ChatMember, ChatMemberCollection>(SetItems, new ChatMemberHandler());
        }

        protected Chat _chat;
        public Chat Chat
        {
            get => _chat;
            set => Set(ref _chat, value);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is ChatSavedMessagesTopicIdNavigationArgs savedMessagesTopicIdArgs)
            {
                parameter = savedMessagesTopicIdArgs.ChatId;
            }
            else if (parameter is ChatMessageIdNavigationArgs args)
            {
                parameter = args.ChatId;
            }

            var chatId = (long)parameter;

            Chat = ClientService.GetChat(chatId);

            var chat = _chat;
            if (chat == null)
            {
                return Task.CompletedTask;
            }

            if (chat.Type is ChatTypeSupergroup supergroup)
            {
                var item = ClientService.GetSupergroup(supergroup.SupergroupId);
                var cache = ClientService.GetSupergroupFull(supergroup.SupergroupId);

                Delegate?.UpdateSupergroup(chat, item);

                if (cache == null)
                {
                    ClientService.Send(new GetSupergroupFullInfo(supergroup.SupergroupId));
                }
                else
                {
                    Delegate?.UpdateSupergroupFullInfo(chat, item, cache);
                }

                _memberCount = cache?.MemberCount ?? item.MemberCount;
                Members.UpdateQuery(string.Empty);
            }
            else if (chat.Type is ChatTypeBasicGroup basicGroup)
            {
                var item = ClientService.GetBasicGroup(basicGroup.BasicGroupId);
                var cache = ClientService.GetBasicGroupFull(basicGroup.BasicGroupId);

                if (Delegate is IBasicGroupDelegate basicDelegate)
                {
                    basicDelegate.UpdateBasicGroup(chat, item);
                }

                _memberCount = cache?.Members.Count ?? item.MemberCount;
                Members.UpdateQuery(string.Empty);
            }

            return Task.CompletedTask;
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
                BeginOnUIThread(() => Delegate?.UpdateSupergroupFullInfo(chat, ClientService.GetSupergroup(update.SupergroupId), update.SupergroupFullInfo));
            }
        }

        public SearchCollection<ChatMember, ChatMemberCollection> Members { get; private set; }

        public void UpdateMembers()
        {
            var memberCount = ClientService.GetMembersCount(_chat);

            if (_memberCount != memberCount && memberCount > 0 && memberCount <= 200)
            {
                _memberCount = memberCount;
                Members.Reload();
            }
        }

        private ChatMemberCollection SetItems(object sender, string query)
        {
            if (_chat?.Type is ChatTypeSupergroup supergroup)
            {
                return new ChatMemberCollection(ClientService, supergroup.SupergroupId, _find(query));
            }
            else if (_chat?.Type is ChatTypeBasicGroup)
            {
                ChatMembersFilter filter = _filter switch
                {
                    SupergroupMembersFilterAdministrators => new ChatMembersFilterAdministrators(),
                    SupergroupMembersFilterBanned => new ChatMembersFilterBanned(),
                    SupergroupMembersFilterBots => new ChatMembersFilterBots(),
                    SupergroupMembersFilterRestricted => new ChatMembersFilterRestricted(),
                    _ => null
                };

                return new ChatMemberCollection(ClientService, _chat.Id, query, filter);
            }

            return null;
        }

        public class ChatMemberHandler : IDiffHandler<ChatMember>
        {
            public bool CompareItems(ChatMember oldItem, ChatMember newItem)
            {
                if (oldItem != null && newItem != null)
                {
                    return oldItem.MemberId.AreTheSame(newItem.MemberId);
                }

                return false;
            }

            public void UpdateItem(ChatMember oldItem, ChatMember newItem) { }
        }
    }
}
