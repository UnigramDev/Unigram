//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Chats;
using Telegram.Views.Chats.Popups;
using Telegram.Views.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Chats
{
    public partial class ChatInviteLinksViewModel : ViewModelBase, IIncrementalCollectionOwner
    {
        private LinksCollection _inviteLinks;
        private LinkCountsCollection _linkCounts;
        private LinksCollection _revokedLinks;

        public ChatInviteLinksViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        public IncrementalCollection<ChatInviteLink> InviteLinks => _inviteLinks?.Items;
        public IncrementalCollection<ChatInviteLinkCount> LinkCounts => _linkCounts?.Items;
        public IncrementalCollection<ChatInviteLink> RevokedLinks => _revokedLinks?.Items;

        private ChatInviteLink _inviteLink;
        public ChatInviteLink InviteLink
        {
            get => _inviteLink;
            set => Set(ref _inviteLink, value);
        }

        public bool IsChannel => _channel;

        private long _chatId;
        private long _creatorUserId;

        private bool _channel;
        private bool _canCreateJoinRequests;

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is long chatId)
            {
                parameter = new ChatInviteLinksArgs(chatId, ClientService.Options.MyId);
            }

            if (parameter is ChatInviteLinksArgs args)
            {
                _chatId = args.ChatId;
                _creatorUserId = args.CreatorUserId;

                _inviteLinks = new LinksCollection(ClientService, _chatId, _creatorUserId, false, this, null);
                _revokedLinks = new LinksCollection(ClientService, _chatId, _creatorUserId, true, this, this);
                _linkCounts = new LinkCountsCollection(ClientService, _chatId, _creatorUserId == ClientService.Options.MyId);

                if (ClientService.TryGetChat(_chatId, out Chat chat))
                {
                    if (ClientService.TryGetSupergroup(chat, out Supergroup supergroup))
                    {
                        _channel = supergroup.IsChannel;
                        _canCreateJoinRequests = !supergroup.IsPublic();

                        if (supergroup.HasActiveUsername(out string username))
                        {
                            InviteLink = new ChatInviteLink
                            {
                                InviteLink = MeUrlPrefixConverter.Convert(ClientService, username)
                            };
                        }
                    }
                    else
                    {
                        _channel = false;
                        _canCreateJoinRequests = true;
                    }
                }
            }

            return Task.CompletedTask;
        }

        public Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            if (_inviteLinks.HasMoreItems)
            {
                return _inviteLinks.LoadMoreItemsAsync(count);
            }
            else if (_linkCounts != null && _linkCounts.HasMoreItems)
            {
                return _linkCounts.LoadMoreItemsAsync(count);
            }

            return _revokedLinks.LoadMoreItemsAsyncImpl(count);
        }

        public async void CreateLink()
        {
            var popup = new ChatInviteLinkPopup(ClientService, _chatId, _channel, _canCreateJoinRequests, null);

            var confirm = await ShowPopupAsync(popup);
            if (confirm == ContentDialogResult.Primary)
            {
                var response = await ClientService.SendAsync(popup.Request);
                if (response is ChatInviteLink newLink)
                {
                    InviteLinks.Insert(0, newLink);
                }
            }
        }

        public void CopyLink(ChatInviteLink inviteLink)
        {
            MessageHelper.CopyLink(XamlRoot, inviteLink.InviteLink);
        }

        public async void ShareLink(ChatInviteLink inviteLink)
        {
            await ShowPopupAsync(new ChooseChatsPopup(), new ChooseChatsConfigurationPostText(inviteLink.InviteLink));
        }

        public async void EditLink(ChatInviteLink inviteLink)
        {
            var popup = new ChatInviteLinkPopup(ClientService, _chatId, _channel, _canCreateJoinRequests, inviteLink);

            var confirm = await ShowPopupAsync(popup);
            if (confirm == ContentDialogResult.Primary)
            {
                var response = await ClientService.SendAsync(popup.Request);
                if (response is ChatInviteLink newLink)
                {
                    var index = InviteLinks.IndexOf(inviteLink);
                    InviteLinks.RemoveAt(index);
                    InviteLinks.Insert(index, newLink);
                }
            }
        }

        public async void RevokeLink(ChatInviteLink inviteLink)
        {
            var confirm = await ShowPopupAsync(Strings.RevokeAlert, Strings.RevokeLink, Strings.RevokeButton, Strings.Cancel, destructive: true);
            if (confirm == ContentDialogResult.Primary)
            {
                var response = await ClientService.SendAsync(new RevokeChatInviteLink(_chatId, inviteLink.InviteLink));
                if (response is ChatInviteLinks inviteLinks)
                {
                    InviteLinks.Remove(inviteLink);

                    for (int i = inviteLinks.InviteLinks.Count - 1; i >= 0; i--)
                    {
                        RevokedLinks.Insert(0, inviteLinks.InviteLinks[i]);
                    }
                }
            }
        }

        public async void DeleteLink(ChatInviteLink inviteLink)
        {
            var confirm = await ShowPopupAsync(Strings.DeleteLinkHelp, Strings.DeleteLink, Strings.Delete, Strings.Cancel, destructive: true);
            if (confirm == ContentDialogResult.Primary)
            {
                var response = await ClientService.SendAsync(new DeleteRevokedChatInviteLink(_chatId, inviteLink.InviteLink));
                if (response is Ok)
                {
                    RevokedLinks.Remove(inviteLink);
                }
            }
        }

        public async void RevokeAll()
        {
            var confirm = await ShowPopupAsync(Strings.DeleteAllRevokedLinkHelp, Strings.DeleteAllRevokedLinks, Strings.Delete, Strings.Cancel, destructive: true);
            if (confirm == ContentDialogResult.Primary)
            {
                var response = await ClientService.SendAsync(new DeleteAllRevokedChatInviteLinks(_chatId, _creatorUserId));
                if (response is Ok)
                {
                    RevokedLinks.Clear();
                }
            }
        }

        public void OpenInviteLink(ChatInviteLink inviteLink)
        {
            ShowPopup(new ChatInviteLinkInfoPopup(this, _chatId, inviteLink));
        }

        public void OpenInviteLinkCount(ChatInviteLinkCount inviteLinkCount)
        {
            NavigationService.Navigate(typeof(ChatInviteLinksPage), new ChatInviteLinksArgs(_chatId, inviteLinkCount.UserId));
        }

        public bool HasMoreItems => _inviteLinks.HasMoreItems || _revokedLinks.HasMoreItemsImpl || _linkCounts.HasMoreItems;

        class LinksCollection : IIncrementalCollectionOwner
        {
            private readonly IClientService _clientService;
            private readonly long _chatId;
            private readonly long _creatorUserId;
            private readonly bool _isRevoked;

            private readonly ChatInviteLinksViewModel _viewModel;
            private readonly ChatInviteLinksViewModel _owner;

            private int _offsetDate = 0;
            private string _offsetInviteLink = string.Empty;

            public LinksCollection(IClientService clientService, long chatId, long creatorUserId, bool isRevoked, ChatInviteLinksViewModel viewModel, ChatInviteLinksViewModel owner)
            {
                _clientService = clientService;
                _chatId = chatId;
                _creatorUserId = creatorUserId;
                _isRevoked = isRevoked;
                _viewModel = viewModel;
                _owner = owner;

                Items = new IncrementalCollection<ChatInviteLink>(this);
            }

            public IncrementalCollection<ChatInviteLink> Items { get; private set; }

            public Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                if (_owner != null)
                {
                    return _owner.LoadMoreItemsAsync(count);
                }

                return LoadMoreItemsAsyncImpl(count);
            }

            public async Task<LoadMoreItemsResult> LoadMoreItemsAsyncImpl(uint count)
            {
                var totalCount = 0u;

                var response = await _clientService.SendAsync(new GetChatInviteLinks(_chatId, _creatorUserId, _isRevoked, _offsetDate, _offsetInviteLink, 100));
                if (response is ChatInviteLinks inviteLinks)
                {
                    foreach (var item in inviteLinks.InviteLinks)
                    {
                        _offsetDate = item.Date;
                        _offsetInviteLink = item.InviteLink;

                        if (item.IsPrimary && (_viewModel == null || _viewModel._canCreateJoinRequests))
                        {
                            if (_viewModel != null && _owner == null)
                            {
                                _viewModel.InviteLink = item;
                            }

                            continue;
                        }

                        Items.Add(item);
                        totalCount++;
                    }
                }

                HasMoreItemsImpl = totalCount > 0;

                return new LoadMoreItemsResult
                {
                    Count = totalCount
                };
            }

            public bool HasMoreItemsImpl { get; private set; } = true;

            public bool HasMoreItems
            {
                get
                {
                    if (_owner != null)
                    {
                        return _owner.HasMoreItems;
                    }

                    return HasMoreItemsImpl;
                }
            }
        }

        class LinkCountsCollection : IIncrementalCollectionOwner
        {
            private readonly IClientService _clientService;
            private readonly long _chatId;

            public LinkCountsCollection(IClientService clientService, long chatId, bool hasMoreItems)
            {
                _clientService = clientService;
                _chatId = chatId;

                Items = new IncrementalCollection<ChatInviteLinkCount>(this);
                HasMoreItems = hasMoreItems;
            }

            public IncrementalCollection<ChatInviteLinkCount> Items { get; private set; }

            public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                var totalCount = 0u;

                var response = await _clientService.SendAsync(new GetChatInviteLinkCounts(_chatId));
                if (response is ChatInviteLinkCounts inviteLinks)
                {
                    foreach (var item in inviteLinks.InviteLinkCounts)
                    {
                        if (item.UserId == _clientService.Options.MyId)
                        {
                            continue;
                        }

                        Items.Add(item);
                        totalCount++;
                    }
                }

                HasMoreItems = false;

                return new LoadMoreItemsResult
                {
                    Count = totalCount
                };
            }

            public bool HasMoreItems { get; private set; } = true;
        }

    }
}
