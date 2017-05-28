using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Chats
{
    public class ChatInviteLinkViewModel : UnigramViewModelBase
    {
        private TLPeerBase _peer;
        private TLExportedChatInviteBase _exportedInvite;

        public ChatInviteLinkViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
        }

        private TLChatBase _item;
        public TLChatBase Item
        {
            get
            {
                return _item;
            }
            set
            {
                Set(ref _item, value);
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

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            Task<MTProtoResponse<TLExportedChatInviteBase>> task = null;

            if (parameter is TLPeerChannel peerChannel)
            {
                _peer = peerChannel;

                var channel = CacheService.GetChat(peerChannel.ChannelId) as TLChannel;
                if (channel != null)
                {
                    Item = channel;

                    var full = CacheService.GetFullChat(channel.Id) as TLChannelFull;
                    if (full == null)
                    {
                        var response = await ProtoService.GetFullChannelAsync(channel.ToInputChannel());
                        if (response.IsSucceeded)
                        {
                            full = response.Result.FullChat as TLChannelFull;
                        }
                    }

                    if (full != null)
                    {
                        _exportedInvite = full.ExportedInvite;

                        if (full.ExportedInvite is TLChatInviteExported invite)
                        {
                            InviteLink = invite.Link;
                        }
                        else
                        {
                            task = ProtoService.ExportInviteAsync(channel.ToInputChannel());
                        }
                    }
                }
            }
            else if (parameter is TLPeerChat peerChat)
            {
                _peer = peerChat;

                var chat = CacheService.GetChat(peerChat.ChatId) as TLChat;
                if (chat != null)
                {
                    Item = chat;

                    var full = CacheService.GetFullChat(chat.Id) as TLChannelFull;
                    if (full == null)
                    {
                        var response = await ProtoService.GetFullChatAsync(chat.Id);
                        if (response.IsSucceeded)
                        {
                            full = response.Result.FullChat as TLChannelFull;
                        }
                    }

                    if (full != null)
                    {
                        _exportedInvite = full.ExportedInvite;

                        if (full.ExportedInvite is TLChatInviteExported invite)
                        {
                            InviteLink = invite.Link;
                        }
                        else
                        {
                            task = ProtoService.ExportChatInviteAsync(chat.Id);
                        }
                    }
                }
            }

            if (task != null)
            {
                var response = await task;
                if (response.IsSucceeded)
                {
                    _exportedInvite = response.Result;

                    var invite = response.Result as TLChatInviteExported;
                    if (invite != null && !string.IsNullOrEmpty(invite.Link))
                    {
                        InviteLink = invite.Link;
                    }
                }
                else
                {
                    Execute.ShowDebugMessage("channels.exportInvite error " + response.Error);
                }
            }
        }

        public RelayCommand CopyCommand => new RelayCommand(CopyExecute);
        private async void CopyExecute()
        {
            var package = new DataPackage();
            package.SetWebLink(new Uri(_inviteLink));
            Clipboard.SetContent(package);

            await new TLMessageDialog("Link copied to clipboard").ShowAsync();
        }

        public RelayCommand RevokeCommand => new RelayCommand(RevokeExecute);
        private async void RevokeExecute()
        {
            var confirm = await TLMessageDialog.ShowAsync("Are you sure you want to revoke this link? Once you do, no one will be able to join the group using it.", "Telegram", "Revoke", "Cancel");
            if (confirm == ContentDialogResult.Primary)
            {
                Task<MTProtoResponse<TLExportedChatInviteBase>> task = null;

                if (_peer is TLPeerChannel peerChannel)
                {
                    var channel = CacheService.GetChat(peerChannel.ChannelId) as TLChannel;
                    if (channel != null)
                    {
                        task = ProtoService.ExportInviteAsync(channel.ToInputChannel());
                    }
                }
                else if (_peer is TLPeerChat peerChat)
                {
                    var chat = CacheService.GetChat(peerChat.ChatId) as TLChat;
                    if (chat != null)
                    {
                        task = ProtoService.ExportChatInviteAsync(chat.Id);
                    }
                }

                if (task != null)
                {
                    var response = await task;
                    if (response.IsSucceeded)
                    {
                        _exportedInvite = response.Result;

                        var invite = response.Result as TLChatInviteExported;
                        if (invite != null && !string.IsNullOrEmpty(invite.Link))
                        {
                            InviteLink = invite.Link;

                            await TLMessageDialog.ShowAsync("The previous invite link is now inactive. A new invite link has just been generated.", "Telegram", "OK");
                        }
                    }
                    else
                    {
                        Execute.ShowDebugMessage("channels.exportInvite error " + response.Error);
                    }
                }
            }
        }
    }
}
