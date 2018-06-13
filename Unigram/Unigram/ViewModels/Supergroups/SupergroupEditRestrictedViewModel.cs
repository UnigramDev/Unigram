using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Core.Services;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Unigram.Views;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Supergroups
{
    public class SupergroupEditRestrictedViewModel : UnigramViewModelBase, IDelegable<IMemberDelegate>
    {
        public IMemberDelegate Delegate { get; set; }

        public SupergroupEditRestrictedViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            ProfileCommand = new RelayCommand(ProfileExecute);
            SendCommand = new RelayCommand(SendExecute);
            DismissCommand = new RelayCommand(DismissExecute);
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

        private ChatMember _member;
        public ChatMember Member
        {
            get
            {
                return _member;
            }
            set
            {
                Set(ref _member, value);
            }
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var bundle = parameter as ChatMemberNavigation;
            if (bundle == null)
            {
                return;
            }

            Chat = ProtoService.GetChat(bundle.ChatId);

            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var response = await ProtoService.SendAsync(new GetChatMember(chat.Id, bundle.UserId));
            if (response is ChatMember member && chat.Type is ChatTypeSupergroup super)
            {
                var item = ProtoService.GetUser(member.UserId);
                var cache = ProtoService.GetUserFull(member.UserId);

                var group = ProtoService.GetSupergroup(super.SupergroupId);

                Delegate?.UpdateMember(chat, group, item, member);
                Delegate?.UpdateUser(chat, item, false);

                if (cache == null)
                {
                    ProtoService.Send(new GetUserFullInfo(member.UserId));
                }
                else
                {
                    Delegate?.UpdateUserFullInfo(chat, item, cache, false);
                }

                Member = member;

                if (member.Status is ChatMemberStatusRestricted restricted)
                {
                    CanAddWebPagePreviews = restricted.CanAddWebPagePreviews;
                    CanSendOtherMessages = restricted.CanSendOtherMessages;
                    CanSendMediaMessages = restricted.CanSendMediaMessages;
                    CanSendMessages = restricted.CanSendMessages;
                    CanViewMessages = true;
                }
                else
                {
                    CanAddWebPagePreviews = false;
                    CanSendOtherMessages = false;
                    CanSendMediaMessages = false;
                    CanSendMessages = false;
                    CanViewMessages = !(member.Status is ChatMemberStatusBanned);
                }
            }
        }

        #region Flags

        private bool _canViewMessages;
        public bool CanViewMessages
        {
            get
            {
                return _canViewMessages;
            }
            set
            {
                Set(ref _canViewMessages, value);

                // Don't allow send messages
                if (!value && _canSendMessages)
                {
                    CanSendMessages = false;
                }
            }
        }

        private bool _canSendMessages;
        public bool CanSendMessages
        {
            get
            {
                return _canSendMessages;
            }
            set
            {
                Set(ref _canSendMessages, value);

                // Allow view
                if (value && !_canViewMessages)
                {
                    CanViewMessages = true;
                }

                // Don't allow send media
                if (!value && _canSendMediaMessages)
                {
                    CanSendMediaMessages = false;
                }
            }
        }

        private bool _canSendMediaMessages;
        public bool CanSendMediaMessages
        {
            get
            {
                return _canSendMediaMessages;
            }
            set
            {
                Set(ref _canSendMediaMessages, value);

                // Allow send messages
                if (value && !_canSendMessages)
                {
                    CanSendMessages = true;
                }

                // Don't allow send stickers
                if (!value && _canSendOtherMessages)
                {
                    CanSendOtherMessages = false;
                }

                // Don't allow embed links
                if (!value && _canAddWebPagePreviews)
                {
                    CanAddWebPagePreviews = false;
                }
            }
        }

        private bool _canSendOtherMessages;
        public bool CanSendOtherMessages
        {
            get
            {
                return _canSendOtherMessages;
            }
            set
            {
                Set(ref _canSendOtherMessages, value);

                // Allow send media
                if (value && !_canSendMediaMessages)
                {
                    CanSendMediaMessages = true;
                }
            }
        }

        private bool _canAddWebPagePreviews;
        public bool CanAddWebPagePreviews
        {
            get
            {
                return _canAddWebPagePreviews;
            }
            set
            {
                Set(ref _canAddWebPagePreviews, value);

                if (value && !_canSendMediaMessages)
                {
                    CanSendMediaMessages = true;
                }
            }
        }

        #endregion

        public RelayCommand ProfileCommand { get; }
        private async void ProfileExecute()
        {
            var member = _member;
            if (member == null)
            {
                return;
            }

            var response = await ProtoService.SendAsync(new CreatePrivateChat(member.UserId, false));
            if (response is Chat chat)
            {
                NavigationService.Navigate(typeof(ProfilePage), chat.Id);
            }
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var member = _member;
            if (member == null)
            {
                return;
            }

            var supergroup = chat.Type as ChatTypeSupergroup;
            if (supergroup == null)
            {
                return;
            }

            var status = new ChatMemberStatusRestricted
            {
                IsMember = _canViewMessages,
                CanAddWebPagePreviews = _canAddWebPagePreviews,
                CanSendOtherMessages = _canSendOtherMessages,
                CanSendMediaMessages = _canSendMediaMessages,
                CanSendMessages = _canSendMessages
            };

            var response = await ProtoService.SendAsync(new SetChatMemberStatus(chat.Id, member.UserId, status));
            if (response is Ok)
            {
                NavigationService.GoBack();
                NavigationService.Frame.ForwardStack.Clear();
            }
            else
            {
                // TODO: ...
            }
        }

        public RelayCommand DismissCommand { get; }
        private async void DismissExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var member = _member;
            if (member == null)
            {
                return;
            }

            var response = await ProtoService.SendAsync(new SetChatMemberStatus(chat.Id, member.UserId, new ChatMemberStatusMember()));
            if (response is Ok)
            {
                NavigationService.GoBack();
                NavigationService.Frame.ForwardStack.Clear();
            }
            else
            {
                // TODO: ...
            }
        }
    }
}
