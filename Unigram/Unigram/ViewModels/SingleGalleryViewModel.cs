using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Core.Common;
using Unigram.Services;

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
