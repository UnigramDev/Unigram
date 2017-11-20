using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Strings;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Chats
{
    public class ChatInviteViewModel : UsersSelectionViewModel
    {
        public ChatInviteViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
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

        public bool IsCreator
        {
            get
            {
                return _item != null && ((_item is TLChannel channel && (channel.IsCreator || (channel.HasAdminRights && channel.AdminRights.IsInviteLink))) || (_item is TLChat chat && chat.IsCreator));
            }
        }

        public bool IsGroup
        {
            get
            {
                return _item != null && ((_item is TLChannel channel && channel.IsMegaGroup) || (_item is TLChat chat));
            }
        }

        public override string Title => Strings.Android.SelectContact;

        public override int Maximum => 1;

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            Item = null;

            var chat = parameter as TLChatBase;
            var peer = parameter as TLPeerBase;
            if (peer != null)
            {
                chat = CacheService.GetChat(peer.Id);
            }

            if (chat != null)
            {
                Item = chat;
                RaisePropertyChanged(() => IsCreator);
                RaisePropertyChanged(() => IsGroup);
            }

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        protected override async void SendExecute()
        {
            var user = SelectedItems.FirstOrDefault();
            if (user == null)
            {
                return;
            }

            Task<MTProtoResponse<TLUpdatesBase>> task = null;
            if (_item is TLChannel channel)
            {
                task = ProtoService.InviteToChannelAsync(channel.ToInputChannel(), new TLVector<TLInputUserBase> { user.ToInputUser() });
            }
            else if (_item is TLChat chat)
            {
                var count = 100;
                var config = CacheService.GetConfig();
                if (config != null)
                {
                    count = config.ForwardedCountMax;
                }

                task = ProtoService.AddChatUserAsync(chat.Id, user.ToInputUser(), count);
            }

            if (task == null)
            {
                return;
            }

            var response = await task;
            if (response.IsSucceeded)
            {
                NavigationService.GoBack();

                if (response.Result is TLUpdates updates)
                {
                    var newMessage = updates.Updates.FirstOrDefault(x => x is TLUpdateNewMessage) as TLUpdateNewMessage;
                    if (newMessage != null)
                    {
                        Aggregator.Publish(newMessage.Message);
                    }

                    var newChannelMessage = updates.Updates.FirstOrDefault(x => x is TLUpdateNewChannelMessage) as TLUpdateNewChannelMessage;
                    if (newChannelMessage != null)
                    {
                        Aggregator.Publish(newChannelMessage.Message);
                    }

                }
            }
            else
            {
                if (response.Error.TypeEquals(TLErrorType.PEER_FLOOD))
                {
                    var dialog = new TLMessageDialog();
                    dialog.Title = "Telegram";
                    dialog.Message = Strings.Resources.PeerFloodAddContact;
                    dialog.PrimaryButtonText = "More info";
                    dialog.SecondaryButtonText = "OK";

                    var confirm = await dialog.ShowQueuedAsync();
                    if (confirm == ContentDialogResult.Primary)
                    {
                        MessageHelper.HandleTelegramUrl("t.me/SpamBot");
                    }
                }
                else if (response.Error.TypeEquals(TLErrorType.USERS_TOO_MUCH))
                {
                    await TLMessageDialog.ShowAsync(Strings.Resources.UsersTooMuch, "Telegram", "OK");
                }
                else if (response.Error.TypeEquals(TLErrorType.BOTS_TOO_MUCH))
                {
                    await TLMessageDialog.ShowAsync(Strings.Resources.BotsTooMuch, "Telegram", "OK");
                }
                else if (response.Error.TypeEquals(TLErrorType.USER_NOT_MUTUAL_CONTACT))
                {
                    await TLMessageDialog.ShowAsync(Strings.Resources.UserNotMutualContact, "Telegram", "OK");
                }

                Execute.ShowDebugMessage("channels.inviteToChannel error " + response.Error);
            }
        }
    }
}
