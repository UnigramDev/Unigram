using Unigram.Collections;
using Unigram.Services;
using Unigram.ViewModels.Gallery;

namespace Unigram.ViewModels
{
    public class SingleGalleryViewModel : GalleryViewModelBase
    {
        public SingleGalleryViewModel(IClientService clientService, IStorageService storageService, IEventAggregator aggregator, GalleryContent item)
            : base(clientService, storageService, aggregator)
        {
            Items = new MvxObservableCollection<GalleryContent> { item };
            SelectedItem = item;
            FirstItem = item;
        }
    }
}
