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
                return _item != null && _item.IsSignatures && _item.IsBroadcast;
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

                var response = await ProtoService.GetFullChannelAsync(channel.ToInputChannel());
                if (response.IsSucceeded)
                {
                    Full = response.Result.FullChat as TLChannelFull;
                    About = _full.About;
                }
            }
        }

        public RelayCommand SendCommand => new RelayCommand(SendExecute);
        private async void SendExecute()
        {
            if (_item != null && _full != null && !string.Equals(_about, _full.About))
            {
                var response = await ProtoService.EditAboutAsync(_item, _about);
                if (response.IsSucceeded)
                {
                    _full.About = _about;
                    _full.RaisePropertyChanged(() => _full.About);
                }
                else
                {
                    // TODO:
                    return;
                }
            }

            if (_item != null && !string.Equals(_title, _item.Title))
            {
                var response = await ProtoService.EditTitleAsync(_item, _title);
                if (response.IsSucceeded)
                {
                    _item.Title = _title;
                    _item.RaisePropertyChanged(() => _item.Title);
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

        public override void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            base.RaisePropertyChanged(propertyName);

            if (propertyName.Equals(nameof(IsSignatures)))
            {
                ProtoService.ToggleSignaturesAsync(_item.ToInputChannel(), _isSignatures, null);
            }
        }
    }
}
