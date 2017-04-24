using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;

namespace Unigram.ViewModels
{
    public class InstantGalleryViewModel : GalleryViewModelBase
    {
        public InstantGalleryViewModel() 
            : base(null, null, null)
        {
            Items = new ObservableCollection<GalleryItem>();
            Items.CollectionChanged += OnCollectionChanged;
        }

        private void OnCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            TotalItems = Items.Count;
        }
    }
}
