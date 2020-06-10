using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
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

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
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

                Members = new ChatMemberCollection(ProtoService, supergroup.SupergroupId, _filter ?? _find(string.Empty));
            }
            else if (chat.Type is ChatTypeBasicGroup basicGroup)
            {
                var item = ProtoService.GetBasicGroup(basicGroup.BasicGroupId);

                if (Delegate is IBasicGroupDelegate basicDelegate)
                {
                    basicDelegate.UpdateBasicGroup(chat, item);
                }

                var filter = default(ChatMembersFilter);
                switch (_filter)
                {
                    case SupergroupMembersFilterAdministrators administrators:
                        filter = new ChatMembersFilterAdministrators();
                        break;
                    case SupergroupMembersFilterBanned banned:
                        filter = new ChatMembersFilterBanned();
                        break;
                    case SupergroupMembersFilterBots bots:
                        filter = new ChatMembersFilterBots();
                        break;
                    case SupergroupMembersFilterRecent recent:
                        filter = null;
                        break;
                    case SupergroupMembersFilterRestricted restricted:
                        filter = new ChatMembersFilterRestricted();
                        break;
                    case SupergroupMembersFilterSearch search:
                        filter = null;
                        break;
                }

                Members = new ChatMemberCollection(ProtoService, chat.Id, string.Empty, filter);
            }

            return Task.CompletedTask;
        }

        public void Find(string query)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup supergroup)
            {
                Search = new ChatMemberCollection(ProtoService, supergroup.SupergroupId, _find(query));
            }
            else if (chat.Type is ChatTypeBasicGroup basicGroup)
            {
                var filter = default(ChatMembersFilter);
                switch (_filter)
                {
                    case SupergroupMembersFilterAdministrators administrators:
                        filter = new ChatMembersFilterAdministrators();
                        break;
                    case SupergroupMembersFilterBanned banned:
                        filter = new ChatMembersFilterBanned();
                        break;
                    case SupergroupMembersFilterBots bots:
                        filter = new ChatMembersFilterBots();
                        break;
                    case SupergroupMembersFilterRecent recent:
                        filter = null;
                        break;
                    case SupergroupMembersFilterRestricted restricted:
                        filter = new ChatMembersFilterRestricted();
                        break;
                    case SupergroupMembersFilterSearch search:
                        filter = null;
                        break;
                }

                Search = new ChatMemberCollection(ProtoService, chat.Id, query, filter);
            }
        }

        protected ChatMemberCollection _members;
        public ChatMemberCollection Members
        {
            get
            {
                return _members;
            }
            set
            {
                Set(ref _members, value);
            }
        }

        protected ChatMemberCollection _search;
        public ChatMemberCollection Search
        {
            get
            {
                return _search;
            }
            set
            {
                Set(ref _search, value);
            }
        }
    }
}
