using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Navigation;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Navigation.Services;
using Unigram.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

namespace Unigram.ViewModels.Chats
{
    public class ChatInviteLinkViewModel : TLViewModelBase,
        IHandle<UpdateBasicGroupFullInfo>,
        IHandle<UpdateSupergroupFullInfo>
    {
        public ChatInviteLinkViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Members = new MvxObservableCollection<User>();
            Administrators = new MvxObservableCollection<ChatInviteLinkCount>();

            CopyCommand = new RelayCommand(CopyExecute);
            RevokeCommand = new RelayCommand(RevokeExecute);
        }

        protected Chat _chat;
        public Chat Chat
        {
            get => _chat;
            set => Set(ref _chat, value);
        }

        private string _inviteLink;
        public string InviteLink
        {
            get => _inviteLink;
            set => Set(ref _inviteLink, value);
        }

        public MvxObservableCollection<User> Members { get; private set; }
        public MvxObservableCollection<ChatInviteLinkCount> Administrators { get; private set; }

        public ItemsCollection Items { get; private set; }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var chatId = (long)parameter;

            state.TryGet("inviteLink", out string inviteLink);

            Chat = ProtoService.GetChat(chatId);

            if (inviteLink == null)
            {
                if (CacheService.TryGetSupergroupFull(_chat, out SupergroupFullInfo supergroup))
                {
                    inviteLink = supergroup.InviteLink?.InviteLink;
                }
                else if (CacheService.TryGetBasicGroupFull(_chat, out BasicGroupFullInfo basicGroup))
                {
                    inviteLink = basicGroup.InviteLink?.InviteLink;
                }
            }

            Items = new ItemsCollection(this, _chat, inviteLink, CacheService.Options.MyId);

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

        private void UpdateInviteLink(Chat chat, ChatInviteLink inviteLink)
        {
            if (inviteLink == null)
            {
                ProtoService.Send(new CreateChatInviteLink(chat.Id, string.Empty, 0, 0, false));
            }
            else
            {
                InviteLink = inviteLink.InviteLink;
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

            //ProtoService.Send(new ReplacePermanentChatInviteLink(chat.Id));
        }

        public class ItemsCollection : MvxObservableCollection<object>, IGroupSupportIncrementalLoading
        {
            private readonly ChatInviteLinkViewModel _viewModel;
            private readonly Chat _chat;
            private readonly string _inviteLink;
            private readonly long _userId;

            private ItemsStage _stage = ItemsStage.Members;
            private bool _stageHeader;
            private string _stageFooter;

            private bool _hasMoreItems = true;

            private ChatInviteLinkMember _offsetMember;

            private int _offsetDate = 0;
            private string _offsetInviteLink = string.Empty;

            private enum ItemsStage
            {
                Members,
                Links,
                ExpiredLinks,
                Administrators
            }

            public ItemsCollection(ChatInviteLinkViewModel viewModel, Chat chat, string inviteLink, long userId)
            {
                _viewModel = viewModel;
                _chat = chat;
                _inviteLink = inviteLink;
                _userId = userId;

                _stage = _inviteLink != null ? ItemsStage.Members : ItemsStage.Links;
                _stageHeader = true;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async task =>
                {
                    var count = 0u;

                    if (_stage == ItemsStage.Members)
                    {
                        var response = await _viewModel.ProtoService.SendAsync(new GetChatInviteLinkMembers(_chat.Id, _inviteLink, _offsetMember, 20));
                        if (response is ChatInviteLinkMembers members)
                        {
                            foreach (var item in members.Members)
                            {
                                _offsetMember = item;

                                Add(item);
                                count++;
                            }

                            if (members.Members.Count < 1)
                            {
                                _stageHeader = true;
                                _stageFooter = null;
                                _stage = ItemsStage.Links;
                            }
                        }
                        else
                        {
                            _stageHeader = true;
                            _stageFooter = null;
                            _stage = ItemsStage.Links;
                        }
                    }
                    else if (_stage is ItemsStage.Links or ItemsStage.ExpiredLinks)
                    {
                        var response = await _viewModel.ProtoService.SendAsync(new GetChatInviteLinks(_chat.Id, _userId, _stage == ItemsStage.ExpiredLinks, _offsetDate, _offsetInviteLink, 20));
                        if (response is ChatInviteLinks inviteLinks)
                        {
                            if (inviteLinks.TotalCount > 0 && _stageHeader)
                            {
                                _stageHeader = false;
                                Add(new CollectionSeparator
                                {
                                    Footer = _stageFooter,
                                    Header = _stage == ItemsStage.Links ? Strings.Resources.Abort : Strings.Resources.Expired
                                });
                            }

                            foreach (var item in inviteLinks.InviteLinks)
                            {
                                _offsetDate = item.Date;
                                _offsetInviteLink = item.InviteLink;

                                Add(item);
                                count++;
                            }

                            if (inviteLinks.InviteLinks.Count < 1)
                            {
                                _offsetDate = 0;
                                _offsetInviteLink = string.Empty;

                                _stageHeader = true;
                                _stageFooter = _stage == ItemsStage.Links ? Strings.Resources.CreateNewLinkHelp : null;
                                _stage = _stage == ItemsStage.Links ? ItemsStage.ExpiredLinks : ItemsStage.Administrators;
                            }
                        }
                        else
                        {
                            _offsetDate = 0;
                            _offsetInviteLink = string.Empty;

                            _stageHeader = true;
                            _stageFooter = _stage == ItemsStage.Links ? Strings.Resources.CreateNewLinkHelp : null;
                            _stage = _stage == ItemsStage.Links ? ItemsStage.ExpiredLinks : ItemsStage.Administrators;
                        }
                    }
                    else if (_stage == ItemsStage.Administrators && IsChatOwner(_chat))
                    {
                        var response = await _viewModel.ProtoService.SendAsync(new GetChatInviteLinkCounts(_chat.Id));
                        if (response is ChatInviteLinkCounts inviteLinkCounts)
                        {
                            if (inviteLinkCounts.InviteLinkCounts.Count > 0 && _stageHeader)
                            {
                                _stageHeader = false;
                                Add(new CollectionSeparator
                                {
                                    Footer = _stageFooter,
                                    Header = Strings.Resources.LinksCreatedByOtherAdmins
                                });
                            }

                            foreach (var item in inviteLinkCounts.InviteLinkCounts)
                            {
                                if (item.UserId == _userId)
                                {
                                    continue;
                                }

                                Add(item);
                                count++;
                            }

                            _hasMoreItems = false;
                        }
                    }

                    return new LoadMoreItemsResult { Count = count };
                });
            }

            private bool IsChatOwner(Chat chat)
            {
                if (_viewModel.CacheService.TryGetSupergroup(chat, out Supergroup supergroup))
                {
                    return supergroup.Status is ChatMemberStatusCreator;
                }
                else if (_viewModel.CacheService.TryGetBasicGroup(chat, out BasicGroup basicGroup))
                {
                    return basicGroup.Status is ChatMemberStatusCreator;
                }

                return false;
            }

            public bool HasMoreItems => _hasMoreItems;
        }
    }
}
