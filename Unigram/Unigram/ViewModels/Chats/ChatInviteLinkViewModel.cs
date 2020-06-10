using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Chats
{
    public class ChatInviteLinkViewModel : TLViewModelBase,
        IHandle<UpdateBasicGroupFullInfo>,
        IHandle<UpdateSupergroupFullInfo>
    {
        public ChatInviteLinkViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            CopyCommand = new RelayCommand(CopyExecute);
            RevokeCommand = new RelayCommand(RevokeExecute);
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

        private string _inviteLink;
        public string InviteLink
        {
            get
            {
                return _inviteLink;
            }
            set
            {
                Set(ref _inviteLink, value);
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
            //Delegate?.UpdateChat(chat);

            if (chat.Type is ChatTypeBasicGroup basic)
            {
                var item = ProtoService.GetBasicGroup(basic.BasicGroupId);
                var cache = ProtoService.GetBasicGroupFull(basic.BasicGroupId);

                //Delegate?.UpdateBasicGroup(chat, item);

                if (cache == null)
                {
                    ProtoService.Send(new GetBasicGroupFullInfo(basic.BasicGroupId));
                }
                else
                {
                    //Delegate?.UpdateBasicGroupFullInfo(chat, item, cache);
                    UpdateInviteLink(chat, cache.InviteLink);
                }
            }
            else if (chat.Type is ChatTypeSupergroup super)
            {
                var item = ProtoService.GetSupergroup(super.SupergroupId);
                var cache = ProtoService.GetSupergroupFull(super.SupergroupId);

                //Delegate?.UpdateSupergroup(chat, item);

                if (cache == null)
                {
                    ProtoService.Send(new GetSupergroupFullInfo(super.SupergroupId));
                }
                else
                {
                    //Delegate?.UpdateSupergroupFullInfo(chat, item, cache);
                    UpdateInviteLink(chat, cache.InviteLink);
                }
            }

            return Task.CompletedTask;
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
                BeginOnUIThread(() => UpdateInviteLink(chat, update.BasicGroupFullInfo.InviteLink));
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
                BeginOnUIThread(() => UpdateInviteLink(chat, update.SupergroupFullInfo.InviteLink));
            }
        }

        private void UpdateInviteLink(Chat chat, string inviteLink)
        {
            if (string.IsNullOrEmpty(inviteLink))
            {
                ProtoService.Send(new GenerateChatInviteLink(chat.Id));
            }
            else
            {
                InviteLink = inviteLink;
            }
        }

        public RelayCommand CopyCommand { get; }
        private async void CopyExecute()
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(_inviteLink);
            ClipboardEx.TrySetContent(dataPackage);

            await MessagePopup.ShowAsync(Strings.Resources.LinkCopied, Strings.Resources.AppName, Strings.Resources.OK);
        }

        public RelayCommand RevokeCommand { get; }
        private async void RevokeExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var confirm = await MessagePopup.ShowAsync(Strings.Resources.RevokeAlert, Strings.Resources.RevokeLink, Strings.Resources.RevokeButton, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            ProtoService.Send(new GenerateChatInviteLink(chat.Id));
        }
    }
}
