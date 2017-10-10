using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Views.Channels;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Channels
{
    public class ChannelEditViewModel : ChannelDetailsViewModel
    {
        public ChannelEditViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IUploadFileManager uploadFileManager)
            : base(protoService, cacheService, aggregator, uploadFileManager)
        {
        }

        public bool CanEditSignatures
        {
            get
            {
                return _item != null && _item.IsBroadcast;
            }
        }

        public bool CanEditHiddenPreHistory
        {
            get
            {
                return _item != null && _full != null && _item.IsMegaGroup && !_item.HasUsername;
            }
        }

        private string _title;
        public string Title
        {
            get
            {
                return _title;
            }
            set
            {
                Set(ref _title, value);
            }
        }

        private string _about;
        public string About
        {
            get
            {
                return _about;
            }
            set
            {
                Set(ref _about, value);
            }
        }

        private bool _isSignatures;
        public bool IsSignatures
        {
            get
            {
                return _isSignatures;
            }
            set
            {
                Set(ref _isSignatures, value);
            }
        }
        private bool _isHiddenPreHistory;
        public bool IsHiddenPreHistory
        {
            get
            {
                return _isHiddenPreHistory;
            }
            set
            {
                Set(ref _isHiddenPreHistory, value);
            }
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            // SHOULD NOT CALL BASE!

            Item = null;
            Full = null;
            Title = null;
            About = null;

            var channel = parameter as TLChannel;
            var peer = parameter as TLPeerChannel;
            if (peer != null)
            {
                channel = CacheService.GetChat(peer.ChannelId) as TLChannel;
            }

            if (channel != null)
            {
                Item = channel;
                Title = _item.Title;
                IsSignatures = _item.IsSignatures;

                RaisePropertyChanged(() => CanEditSignatures);

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
                    Full = full;
                    About = _full.About;
                    IsHiddenPreHistory = _full.IsHiddenPreHistory;

                    RaisePropertyChanged(() => CanEditHiddenPreHistory);
                }
            }
        }

        public RelayCommand SendCommand => new RelayCommand(SendExecute);
        private async void SendExecute()
        {
            var about = _about.Format();
            var title = _title.Trim();

            if (_item != null && _full != null && !string.Equals(about, _full.About))
            {
                var response = await ProtoService.EditAboutAsync(_item, about);
                if (response.IsSucceeded)
                {
                    _full.About = about;
                    _full.RaisePropertyChanged(() => _full.About);
                }
                else
                {
                    // TODO:
                    return;
                }
            }

            if (_item != null && !string.Equals(title, _item.Title))
            {
                var response = await ProtoService.EditTitleAsync(_item, title);
                if (response.IsSucceeded)
                {
                    _item.Title = title;
                    _item.RaisePropertyChanged(() => _item.Title);
                }
                else
                {
                    // TODO:
                    return;
                }
            }

            if (_item != null && _isSignatures != _item.IsSignatures)
            {
                var response = await ProtoService.ToggleSignaturesAsync(_item.ToInputChannel(), _isSignatures);
                if (response.IsSucceeded)
                {

                }
                else
                {
                    // TODO:
                    return;
                }
            }

            if (_item != null && _full != null && _isHiddenPreHistory != _full.IsHiddenPreHistory)
            {
                var response = await ProtoService.TogglePreHistoryHiddenAsync(_item.ToInputChannel(), _isHiddenPreHistory);
                if (response.IsSucceeded)
                {

                }
                else
                {
                    // TODO:
                    return;
                }
            }

            NavigationService.GoBack();
        }

        public RelayCommand EditTypeCommand => new RelayCommand(EditTypeExecute);
        private void EditTypeExecute()
        {
            NavigationService.Navigate(typeof(ChannelEditTypePage), _item.ToPeer());
        }

        public RelayCommand EditStickerSetCommand => new RelayCommand(EditStickerSetExecute);
        private void EditStickerSetExecute()
        {
            NavigationService.Navigate(typeof(ChannelEditStickerSetPage), _item.ToPeer());
        }
    }
}
