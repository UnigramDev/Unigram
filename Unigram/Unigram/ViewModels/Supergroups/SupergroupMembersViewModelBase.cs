using Rg.DiffUtils;
using System;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Supergroups
{
    public class SupergroupMembersViewModelBase : TLViewModelBase, IDelegable<ISupergroupDelegate>
    {
        private readonly SupergroupMembersFilter _filter;
        private readonly Func<string, SupergroupMembersFilter> _find;

        public ISupergroupDelegate Delegate { get; set; }

        public SupergroupMembersViewModelBase(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, SupergroupMembersFilter filter, Func<string, SupergroupMembersFilter> search)
            : base(protoService, cacheService, settingsService, aggregator)
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

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var chatId = (long)parameter;

            Chat = ProtoService.GetChat(chatId);

            var chat = _chat;
            if (chat == null)
            {
                return Task.CompletedTask;
            }

            if (chat.Type is ChatTypeSupergroup supergroup)
            {
                var item = ProtoService.GetSupergroup(supergroup.SupergroupId);
                var cache = ProtoService.GetSupergroupFull(supergroup.SupergroupId);

                Delegate?.UpdateSupergroup(chat, item);

                if (cache == null)
                {
                    ProtoService.Send(new GetSupergroupFullInfo(supergroup.SupergroupId));
                }
                else
                {
                    Delegate?.UpdateSupergroupFullInfo(chat, item, cache);
                }

                Members.SetQuery(string.Empty);
            }
            else if (chat.Type is ChatTypeBasicGroup basicGroup)
            {
                var item = ProtoService.GetBasicGroup(basicGroup.BasicGroupId);

                if (Delegate is IBasicGroupDelegate basicDelegate)
                {
                    basicDelegate.UpdateBasicGroup(chat, item);
                }

                ChatMembersFilter filter = _filter switch
                {
                    SupergroupMembersFilterAdministrators => new ChatMembersFilterAdministrators(),
                    SupergroupMembersFilterBanned => new ChatMembersFilterBanned(),
                    SupergroupMembersFilterBots => new ChatMembersFilterBots(),
                    SupergroupMembersFilterRestricted => new ChatMembersFilterRestricted(),
                    _ => null
                };

                Members.SetQuery(string.Empty);
            }

            return Task.CompletedTask;
        }

        public SearchCollection<ChatMember, ChatMemberCollection> Members { get; private set; }

        private ChatMemberCollection SetItems(object sender, string query)
        {
            if (_chat?.Type is ChatTypeSupergroup supergroup)
            {
                return new ChatMemberCollection(ProtoService, supergroup.SupergroupId, _find(query));
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

                return new ChatMemberCollection(ProtoService, _chat.Id, query, filter);
            }

            return null;
        }

        public class ChatMemberHandler : IDiffHandler<ChatMember>
        {
            public bool CompareItems(ChatMember oldItem, ChatMember newItem)
            {
                return oldItem?.MemberId == newItem?.MemberId;
            }

            public void UpdateItem(ChatMember oldItem, ChatMember newItem) { }
        }
    }
}
