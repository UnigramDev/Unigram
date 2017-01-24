using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.FileManager;
using Telegram.Api.Services.FileManager.EventArgs;
using Telegram.Api.TL;
using Unigram.Common;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class DialogPhotosViewModel : PhotosViewModelBase
    {
        private readonly DisposableMutex _loadMoreLock = new DisposableMutex();
        private readonly TLInputPeerBase _peer;

        private int _lastMaxId;

        public DialogPhotosViewModel(TLInputPeerBase peer, TLMessage selected, IMTProtoService protoService)
            : base(protoService, null, null)
        {
            var media = selected.Media as TLMessageMediaPhoto;
            if (media != null)
            {
                Items = new ObservableCollection<object> { media.Photo };
                SelectedItem = media.Photo;
            }
            else
            {
                Items = new ObservableCollection<object>();
            }

            _peer = peer;
            _lastMaxId = selected.Id;

            Initialize();
        }

        private async void Initialize()
        {
            using (await _loadMoreLock.WaitAsync())
            {
                var result = await ProtoService.SearchAsync(_peer, string.Empty, new TLInputMessagesFilterPhotoVideo(), 0, 0, -5, _lastMaxId, 15);
                if (result.IsSucceeded)
                {
                    if (result.Result is TLMessagesMessagesSlice)
                    {
                        var slice = result.Result as TLMessagesMessagesSlice;
                        TotalItems = slice.Count;
                    }
                    else
                    {
                        TotalItems = result.Result.Messages.Count;
                    }

                    //Items.Clear();

                    foreach (var photo in result.Result.Messages)
                    {
                        var message = photo as TLMessage;
                        var media = message.Media as TLMessageMediaPhoto;
                        if (media != null)
                        {
                            Items.Add(media.Photo);
                        }
                    }

                    SelectedItem = Items[0];
                }
            }
        }

        //protected override async void LoadNext()
        //{
        //    if (User != null)
        //    {
        //        using (await _loadMoreLock.WaitAsync())
        //        {
        //            var result = await ProtoService.GetUserPhotosAsync(User.ToInputUser(), Items.Count, 0, 0);
        //            if (result.IsSucceeded)
        //            {
        //                foreach (var photo in result.Value.Photos)
        //                {
        //                    Items.Add(photo);
        //                }
        //            }
        //        }
        //    }
        //}
    }
}
