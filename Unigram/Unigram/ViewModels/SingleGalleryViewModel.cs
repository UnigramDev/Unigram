using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Collections;
using Unigram.Services;
using Unigram.ViewModels.Gallery;

namespace Unigram.ViewModels
{
    public class SingleGalleryViewModel : GalleryViewModelBase
    {
        public SingleGalleryViewModel(IProtoService protoService, IEventAggregator aggregator, GalleryItem item) 
            : base(protoService, aggregator)
        {
            Items = new MvxObservableCollection<GalleryItem> { item };
            SelectedItem = item;
            FirstItem = item;
        }
    }
}
