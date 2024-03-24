//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Supergroups
{
    public class SupergroupEditStickerSetArgs
    {
        public SupergroupEditStickerSetArgs(long chatId, StickerType stickerType)
        {
            ChatId = chatId;
            StickerType = stickerType;
        }

        public long ChatId { get; }

        public StickerType StickerType { get; }
    }

    public class SupergroupEditStickerSetViewModel : ViewModelBase, IHandle
    {
        public SupergroupEditStickerSetViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<StickerSetInfo>();
        }

        public StickerType StickerType { get; set; }

        protected Chat _chat;
        public Chat Chat
        {
            get => _chat;
            set => Set(ref _chat, value);
        }

        private StickerSetInfo _listSelectedItem;
        public StickerSetInfo ListSelectedItem
        {
            get => _listSelectedItem;
            set => Set(ref _listSelectedItem, value);
        }

        private List<StickerSetInfo> _items = new();

        private class StickerSetEqualityComparer : IEqualityComparer<StickerSetInfo>
        {
            public bool Equals(StickerSetInfo x, StickerSetInfo y)
            {
                return x.Id == y.Id;
            }

            public int GetHashCode(StickerSetInfo obj)
            {
                return obj.Id.GetHashCode();
            }
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is not SupergroupEditStickerSetArgs args)
            {
                return Task.CompletedTask;
            }

            ClientService.Send(new GetInstalledStickerSets(args.StickerType), result =>
            {
                if (result is StickerSets sets)
                {
                    BeginOnUIThread(() =>
                    {
                        _items.Clear();
                        _items.Add(new StickerSetInfo(0, args.StickerType is StickerTypeCustomEmoji ? Strings.NoEmojiPack : Strings.NoStickerSet, "disabled", null, null, false, false, false, false, null, false, false, false, 0, null));
                        _items.AddRange(sets.Sets);

                        Items.ReplaceWith(_items);
                    });
                }
            });

            Chat = ClientService.GetChat(args.ChatId);
            StickerType = args.StickerType;

            var chat = _chat;
            if (chat == null)
            {
                return Task.CompletedTask;
            }

            //Delegate?.UpdateChat(chat);

            if (chat.Type is ChatTypeSupergroup super)
            {
                var item = ClientService.GetSupergroup(super.SupergroupId);
                var cache = ClientService.GetSupergroupFull(super.SupergroupId);

                //Delegate?.UpdateSupergroup(chat, item);

                if (cache == null)
                {
                    ClientService.Send(new GetSupergroupFullInfo(super.SupergroupId));
                }
                else
                {
                    UpdateSupergroupFullInfo(chat, item, cache);

                }
            }

            return Task.CompletedTask;
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateSupergroupFullInfo>(this, Handle);
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
                BeginOnUIThread(() => UpdateSupergroupFullInfo(chat, ClientService.GetSupergroup(update.SupergroupId), update.SupergroupFullInfo));
            }
        }

        private async void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            var id = StickerType switch
            {
                StickerTypeCustomEmoji => fullInfo.CustomEmojiStickerSetId,
                _ => fullInfo.StickerSetId
            };

            var already = Items.FirstOrDefault(x => x.Id == id);
            if (already != null)
            {
                ListSelectedItem = already;
            }
            else
            {
                var response = await ClientService.SendAsync(new GetStickerSet(fullInfo.StickerSetId));
                if (response is StickerSet set)
                {
                    var info = set.ToInfo();

                    Items.Add(info);
                    ListSelectedItem = info;
                }
            }
        }

        public MvxObservableCollection<StickerSetInfo> Items { get; private set; }

        public async void Continue()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup supergroup && StickerType is StickerTypeRegular)
            {
                var response = await ClientService.SendAsync(new SetSupergroupStickerSet(supergroup.SupergroupId, ListSelectedItem?.Id ?? 0));
                if (response is Ok)
                {
                    NavigationService.GoBack();
                }
                else
                {

                }
            }
        }
    }
}
