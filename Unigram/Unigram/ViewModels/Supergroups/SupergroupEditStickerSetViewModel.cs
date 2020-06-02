using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Supergroups
{
    public class SupergroupEditStickerSetViewModel : TLViewModelBase, IHandle<UpdateSupergroupFullInfo>
    {
        public SupergroupEditStickerSetViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute);
            CancelCommand = new RelayCommand(CancelExecute);

            Items = new MvxObservableCollection<StickerSetInfo>();

            Aggregator.Subscribe(this);
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

        private bool _isAvailable = true;
        public bool IsAvailable
        {
            get
            {
                return _isAvailable;
            }
            set
            {
                Set(ref _isAvailable, value);
            }
        }

        private string _shortName;
        public string ShortName
        {
            get
            {
                return _shortName;
            }
            set
            {
                Set(ref _shortName, value);
            }
        }

        private StickerSetInfo _selectedItem;
        public StickerSetInfo SelectedItem
        {
            get
            {
                return _selectedItem;
            }
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
            get
            {
                return _listSelectedItem;
            }
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

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            ProtoService.Send(new GetInstalledStickerSets(false), result =>
            {
                if (result is StickerSets sets)
                {
                    BeginOnUIThread(() => Items.ReplaceWith(sets.Sets));
                }
            });

            var chatId = (long)parameter;

            Chat = ProtoService.GetChat(chatId);

            var chat = _chat;
            if (chat == null)
            {
                return Task.CompletedTask;
            }

            Aggregator.Subscribe(this);
            //Delegate?.UpdateChat(chat);

            if (chat.Type is ChatTypeSupergroup super)
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
                    UpdateSupergroupFullInfo(chat, item, cache);

                }
            }

            return Task.CompletedTask;
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
                BeginOnUIThread(() => UpdateSupergroupFullInfo(chat, ProtoService.GetSupergroup(update.SupergroupId), update.SupergroupFullInfo));
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
                var response = await ProtoService.SendAsync(new GetStickerSet(fullInfo.StickerSetId));
                if (response is StickerSet set)
                {
                    SelectedItem = new StickerSetInfo(set.Id, set.Title, set.Name, set.Thumbnail, set.IsInstalled, set.IsArchived, set.IsOfficial, set.IsAnimated, set.IsMasks, set.IsViewed, set.Stickers.Count, set.Stickers);
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
                var response = await ProtoService.SendAsync(new SetSupergroupStickerSet(supergroup.SupergroupId, _selectedItem?.Id ?? 0));
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

            var response = await ProtoService.SendAsync(new SearchStickerSet(shortName));
            if (response is StickerSet stickerSet)
            {
                IsLoading = false;
                IsAvailable = true;
                SelectedItem = new StickerSetInfo(stickerSet.Id, stickerSet.Title, stickerSet.Name, stickerSet.Thumbnail, stickerSet.IsInstalled, stickerSet.IsArchived, stickerSet.IsOfficial, stickerSet.IsAnimated, stickerSet.IsMasks, stickerSet.IsViewed, stickerSet.Stickers.Count, stickerSet.Stickers);
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
