using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TdWindows;
using Telegram.Api;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Unigram.Strings;
using Unigram.ViewModels.Delegates;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Chats
{
    public class ChatInviteViewModel : UsersSelectionViewModel
    {
        public IChatDelegate Delegate { get; }

        public ChatInviteViewModel(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
        }

        private Chat _chat;
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

        public bool IsCreator
        {
            get
            {
                return false;
                //return _item != null && ((_item is TLChannel channel && (channel.IsCreator || (channel.HasAdminRights && channel.AdminRights.IsInviteLink))) || (_item is TLChat chat && chat.IsCreator));
            }
        }

        public bool IsGroup
        {
            get
            {
                return false;
                //return _item != null && ((_item is TLChannel channel && channel.IsMegaGroup) || (_item is TLChat chat));
            }
        }

        public override string Title => Strings.Resources.SelectContact;

        public override int Maximum => 1;

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            //Item = null;

            //var chat = parameter as TLChatBase;
            //var peer = parameter as TLPeerBase;
            //if (peer != null)
            //{
            //    chat = CacheService.GetChat(peer.Id);
            //}

            //if (chat != null)
            //{
            //    Item = chat;
            //    RaisePropertyChanged(() => IsCreator);
            //    RaisePropertyChanged(() => IsGroup);
            //}

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

                //Delegate?.UpdateSupergroup(chat, item);

                //Members = new ChatMemberCollection(ProtoService, supergroup.SupergroupId, _filter ?? _find(string.Empty));
            }

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        protected override async void SendExecute(User user)
        {
            var count = ProtoService.GetOption<OptionValueInteger>("forwarded_messages_count_max");

            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (user == null)
            {
                return;
            }

            var confirm = await TLMessageDialog.ShowAsync(string.Format(Strings.Resources.AddToTheGroup, user.GetFullName()), Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            ProtoService.Send(new AddChatMember(chat.Id, user.Id, count?.Value ?? 0));

            NavigationService.GoBack();
        }
    }
}
