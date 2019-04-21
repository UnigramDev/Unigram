using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.BasicGroups
{
    public class BasicGroupEditAdministratorsViewModel : TLViewModelBase, IDelegable<IBasicGroupDelegate>, IHandle<UpdateBasicGroup>, IHandle<UpdateBasicGroupFullInfo>
    {
        public IBasicGroupDelegate Delegate { get; set; }

        public BasicGroupEditAdministratorsViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            ToggleCommand = new RelayCommand(ToggleExecute);
            ToggleMemberCommand = new RelayCommand<ChatMember>(ToggleMemberExecute);
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

            Aggregator.Subscribe(this);
            Delegate?.UpdateChat(chat);

            if (chat.Type is ChatTypeBasicGroup basic)
            {
                var item = ProtoService.GetBasicGroup(basic.BasicGroupId);
                var cache = ProtoService.GetBasicGroupFull(basic.BasicGroupId);

                Delegate?.UpdateBasicGroup(chat, item);

                if (cache == null)
                {
                    ProtoService.Send(new GetBasicGroupFullInfo(basic.BasicGroupId));
                }
                else
                {
                    Delegate?.UpdateBasicGroupFullInfo(chat, item, cache);
                }
            }

            return Task.CompletedTask;
        }

        public override Task OnNavigatedFromAsync(IDictionary<string, object> pageState, bool suspending)
        {
            Aggregator.Unsubscribe(this);
            return Task.CompletedTask;
        }



        public void Handle(UpdateBasicGroup update)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeBasicGroup basic && basic.BasicGroupId == update.BasicGroup.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateBasicGroup(chat, update.BasicGroup));
            }
        }

        public void Handle(UpdateBasicGroupFullInfo update)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeBasicGroup basic && basic.BasicGroupId == update.BasicGroupId)
            {
                BeginOnUIThread(() => Delegate?.UpdateBasicGroupFullInfo(chat, ProtoService.GetBasicGroup(update.BasicGroupId), update.BasicGroupFullInfo));
            }
        }

        public RelayCommand ToggleCommand { get; }
        private void ToggleExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeBasicGroup basicGroup)
            {
                var item = ProtoService.GetBasicGroup(basicGroup.BasicGroupId);
                if (item == null)
                {
                    return;
                }

                ProtoService.Send(new ToggleBasicGroupAdministrators(basicGroup.BasicGroupId, !item.EveryoneIsAdministrator));
            }
        }

        public RelayCommand<ChatMember> ToggleMemberCommand { get; }
        private void ToggleMemberExecute(ChatMember member)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeBasicGroup basicGroup)
            {
                var item = ProtoService.GetBasicGroup(basicGroup.BasicGroupId);
                if (item == null || item.EveryoneIsAdministrator)
                {
                    return;
                }

                ProtoService.Send(new SetChatMemberStatus(chat.Id, member.UserId, member.Status is ChatMemberStatusAdministrator
                    ? new ChatMemberStatusMember() as ChatMemberStatus
                    : new ChatMemberStatusAdministrator(true, true, false, false, true, true, true, false, false)));
            }
        }

        public void Find(string query)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            //if (chat.Type is ChatTypeSupergroup supergroup)
            //{
            //    Search = new ChatMemberCollection(ProtoService, supergroup.SupergroupId, _find(query));
            //}
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

    public class ChatMemberComparer : IComparer<ChatMember>
    {
        public ChatMemberComparer()
        {
        }

        public int Compare(ChatMember x, ChatMember y)
        {
            var x1 = x.Status is ChatMemberStatusCreator
                ? 0
                : x.Status is ChatMemberStatusAdministrator
                ? 1
                : 2;

            var y1 = y.Status is ChatMemberStatusCreator
                ? 0
                : x.Status is ChatMemberStatusAdministrator
                ? 1
                : 2;

            if (x1 > y1)
            {
                return 1;
            }
            else if (x1 < y1)
            {
                return -1;
            }
            //else if (x1 == y1)
            //{
            //    var epoch = LastSeenConverter.GetIndex(y).CompareTo(LastSeenConverter.GetIndex(x));
            //    if (epoch == 0)
            //    {
            //        var nameX = x.FirstName.Length > 0 ? x.FirstName : x.LastName;
            //        var nameY = y.FirstName.Length > 0 ? y.FirstName : y.LastName;

            //        var fullName = nameX.CompareTo(nameY);
            //        if (fullName == 0)
            //        {
            //            return y.Id.CompareTo(x.Id);
            //        }

            //        return fullName;
            //    }

            //    return epoch;
            //}

            return 0;
        }
    }
}
