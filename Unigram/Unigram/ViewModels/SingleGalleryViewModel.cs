using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Unigram.Core.Common;

namespace Unigram.ViewModels
{
    public class SingleGalleryViewModel : GalleryViewModelBase
    {
        public SingleGalleryViewModel(GalleryItem item) 
            : base(null, null, null)
        {
            Items = new MvxObservableCollection<GalleryItem> { item };
            SelectedItem = item;
            FirstItem = item;
        }
    }
}
