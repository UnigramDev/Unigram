using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Telegram.Api.TL.Methods.Channels;
using Unigram.Common;
using Unigram.Views.Channels;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Channels
{
    public class CreateChannelStep1ViewModel : UnigramViewModelBase
    {
        public CreateChannelStep1ViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
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
                SendCommand.RaiseCanExecuteChanged();
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

        private RelayCommand _sendCommand;
        public RelayCommand SendCommand => _sendCommand = _sendCommand ?? new RelayCommand(SendExecute, () => !string.IsNullOrWhiteSpace(Title));
        private async void SendExecute()
        {
            var response = await ProtoService.CreateChannelAsync(TLChannelsCreateChannel.Flag.Broadcast, _title, _about);
            if (response.IsSucceeded)
            {
                var updates = response.Result as TLUpdates;
                if (updates != null)
                {
                    var channel = updates.Chats.FirstOrDefault() as TLChannel;
                    if (channel != null)
                    {
                        //if (this._photo != null)
                        //{
                        //    this.ContinueUploadingPhoto(channel);
                        //    return;
                        //}
                        //if (this._uploadingPhoto)
                        //{
                        //    this._uploadingCallback = delegate
                        //    {
                        //        this.ContinueUploadingPhoto(channel);
                        //    };
                        //    return;
                        //}
                        //this.ContinueNextStep(channel);

                        NavigationService.Navigate(typeof(CreateChannelStep2Page), channel);
                    }
                }
            }
        }
    }
}
