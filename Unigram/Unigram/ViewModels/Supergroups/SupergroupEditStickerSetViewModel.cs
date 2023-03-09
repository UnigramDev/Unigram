//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Navigation.Services;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Supergroups
{
    public class SupergroupEditStickerSetViewModel : TLViewModelBase
        , IHandle
    //, IHandle<UpdateSupergroupFullInfo>
    {
        public SupergroupEditStickerSetViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute);
            CancelCommand = new RelayCommand(CancelExecute);

            Items = new MvxObservableCollection<StickerSetInfo>();
        }

        protected Chat _chat;
        public Chat Chat
        {
            get => _chat;
            set => Set(ref _chat, value);
        }

        private bool _isAvailable = true;
        public bool IsAvailable
        {
            get => _isAvailable;
            set => Set(ref _isAvailable, value);
        }

        private string _shortName;
        public string ShortName
        {
            get => _shortName;
            set => Set(ref _shortName, value);
        }

        private StickerSetInfo _selectedItem;
        public StickerSetInfo SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (value == _selectedItem)
                {
                    return;
                }

                Set(ref _selectedItem, value);

                if (value != null && value.IsInstalled)
                {
                    ListSelectedItem = Items.FirstOrDefault(x => x.Id == value.Id) ?? value;
                }
                else
                {
                    ListSelectedItem = null;
                }
            }
        }

        private StickerSetInfo _listSelectedItem;
        public StickerSetInfo ListSelectedItem
        {
            get => _listSelectedItem;
            set
            {
                if (value == _listSelectedItem)
                {
                    return;
                }

                Set(ref _listSelectedItem, value);

                if (value != null)
                {
                    SelectedItem = value;
                    ShortName = value.Name;
                }
            }
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            ClientService.Send(new GetInstalledStickerSets(new StickerTypeRegular()), result =>
            {
                if (result is StickerSets sets)
                {
                    BeginOnUIThread(() => Items.ReplaceWith(sets.Sets));
                }
            });

            var chatId = (long)parameter;

            Chat = ClientService.GetChat(chatId);

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
            var already = Items.FirstOrDefault(x => x.Id == fullInfo.StickerSetId);
            if (already != null)
            {
                SelectedItem = already;
                ShortName = already.Name;
            }
            else
            {
                var response = await ClientService.SendAsync(new GetStickerSet(fullInfo.StickerSetId));
                if (response is StickerSet set)
                {
                    SelectedItem = new StickerSetInfo(set.Id, set.Title, set.Name, set.Thumbnail, set.ThumbnailOutline, set.IsInstalled, set.IsArchived, set.IsOfficial, set.StickerFormat, set.StickerType, set.IsViewed, set.Stickers.Count, set.Stickers);
                    ShortName = set.Name;
                }
            }
        }

        public MvxObservableCollection<StickerSetInfo> Items { get; private set; }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            if (_shortName != _selectedItem?.Name && !string.IsNullOrWhiteSpace(_shortName))
            {
                await CheckAvailabilityAsync(_shortName);
            }

            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup supergroup)
            {
                var response = await ClientService.SendAsync(new SetSupergroupStickerSet(supergroup.SupergroupId, _selectedItem?.Id ?? 0));
                if (response is Ok)
                {
                    NavigationService.GoBack();
                }
                else
                {

                }
            }
        }

        public RelayCommand CancelCommand { get; }
        private void CancelExecute()
        {
            ShortName = null;
            SelectedItem = null;
        }

        public async void CheckAvailability(string shortName)
        {
            await CheckAvailabilityAsync(shortName);
        }

        private async Task CheckAvailabilityAsync(string shortName)
        {
            IsLoading = true;

            var response = await ClientService.SendAsync(new SearchStickerSet(shortName));
            if (response is StickerSet stickerSet)
            {
                IsLoading = false;
                IsAvailable = true;
                SelectedItem = new StickerSetInfo(stickerSet.Id, stickerSet.Title, stickerSet.Name, stickerSet.Thumbnail, stickerSet.ThumbnailOutline, stickerSet.IsInstalled, stickerSet.IsArchived, stickerSet.IsOfficial, stickerSet.StickerFormat, stickerSet.StickerType, stickerSet.IsViewed, stickerSet.Stickers.Count, stickerSet.Stickers);
                ShortName = stickerSet.Name;
            }
            else
            {
                IsLoading = false;
                IsAvailable = false;
                SelectedItem = null;
            }
        }
    }
}
