using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.Cache.EventArgs;
using Telegram.Api.TL;
using Template10.Common;
using Template10.Mvvm;
using Unigram.Common;
using Unigram.Controls.Views;
using Unigram.Converters;
using Unigram.Core.Common;
using Unigram.ViewModels.Chats;
using Unigram.ViewModels.Users;
using Windows.Storage;
using Windows.System;

namespace Unigram.ViewModels
{
    public class GallerySecretViewModel : UnigramViewModelBase
    {
        public GallerySecretViewModel(TLInputPeerBase peer, TLMessage message, IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            SelectedItem = new GalleryMessageItem(message);

            if (message.IsMediaUnread && !message.IsOut)
            {
                var vector = new TLVector<int> { message.Id };
                aggregator.Publish(new TLUpdateReadMessagesContents { Messages = vector });
                ProtoService.ReadMessageContentsAsync(vector, result =>
                {
                    // TODO: start UI timeout

                    message.IsMediaUnread = false;
                    message.RaisePropertyChanged(() => message.IsMediaUnread);
                });
            }
        }

        protected GalleryItem _firstItem;
        public GalleryItem SelectedItem
        {
            get
            {
                return _firstItem;
            }
            set
            {
                Set(ref _firstItem, value);
            }
        }

        public RelayCommand StickersCommand => new RelayCommand(StickersExecute);
        private async void StickersExecute()
        {
            if (_firstItem != null && _firstItem.HasStickers)
            {
                var inputStickered = _firstItem.ToInputStickeredMedia();
                if (inputStickered != null)
                {
                    var response = await ProtoService.GetAttachedStickersAsync(inputStickered);
                    if (response.IsSucceeded)
                    {
                        if (response.Result.Count > 1)
                        {
                            await AttachedStickersView.Current.ShowAsync(response.Result);
                        }
                        else if (response.Result.Count > 0)
                        {
                            await StickerSetView.Current.ShowAsync(response.Result[0]);
                        }
                    }
                }
            }
        }
    }
}
