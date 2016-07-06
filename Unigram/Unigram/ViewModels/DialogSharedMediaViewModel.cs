using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Collections;
using Unigram.Common;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class DialogSharedMediaViewModel : UnigramViewModelBase
    {
        public DialogSharedMediaViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            Media = new MediaCollection(ProtoService, (TLInputPeerBase)parameter, new TLInputMessagesFilterPhotoVideo());
            Files = new MediaCollection(ProtoService, (TLInputPeerBase)parameter, new TLInputMessagesFilterDocument());
            Music = new MediaCollection(ProtoService, (TLInputPeerBase)parameter, new TLInputMessagesFilterMusic());



            MediaCollection = new ListCollectionView(Media);
            FilesCollection = new ListCollectionView(Files);
            MusicCollection = new ListCollectionView(Music);

            RaisePropertyChanged(() => Files);
            RaisePropertyChanged(() => Files);
            RaisePropertyChanged(() => Music);



            RaisePropertyChanged(() => MediaCollection);
            //RaisePropertyChanged(() => FilesCollection);
            //RaisePropertyChanged(() => MusicCollection);

            return Task.CompletedTask;
        }

        public MediaCollection Media { get; private set; }
        public MediaCollection Files { get; private set; }
        public MediaCollection Music { get; private set; }



        public ListCollectionView MediaCollection { get; private set; }
        public ListCollectionView FilesCollection { get; private set; }
        public ListCollectionView MusicCollection { get; private set; }
    }
}
