using System;
using System.Threading.Tasks;
using Telegram.Td.Api;
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

                Members = new ChatMemberCollection(ProtoService, supergroup.SupergroupId, _filter ?? _find(string.Empty));
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
            else if (chat.Type is ChatTypeBasicGroup)
            {
                ChatMembersFilter filter = _filter switch
                {
                    SupergroupMembersFilterAdministrators => new ChatMembersFilterAdministrators(),
                    SupergroupMembersFilterBanned => new ChatMembersFilterBanned(),
                    SupergroupMembersFilterBots => new ChatMembersFilterBots(),
                    SupergroupMembersFilterRestricted => new ChatMembersFilterRestricted(),
                    _ => null
                };

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
