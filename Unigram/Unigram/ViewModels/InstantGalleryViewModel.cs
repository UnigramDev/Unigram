using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Core.Common;

namespace Unigram.ViewModels
{
    public class InstantGalleryViewModel : GalleryViewModelBase
    {
        private readonly bool _shouldGroup;

        public InstantGalleryViewModel()
            : base(null, null, null)
        {
            Items = new MvxObservableCollection<GalleryItem>();
            Items.CollectionChanged += OnCollectionChanged;
        }

        public InstantGalleryViewModel(TLMessage message, TLWebPage webPage)
            : base(null, null, null)
        {
            _shouldGroup = true;

            Items = new MvxObservableCollection<GalleryItem>(GetWebPagePhotos(message, webPage));
            FirstItem = Items.FirstOrDefault();
            SelectedItem = Items.FirstOrDefault();
            TotalItems = Items.Count;
        }

        public override MvxObservableCollection<GalleryItem> Group => _shouldGroup ? this.Items : null;

        private GalleryItem GetBlock(TLMessage message, TLWebPage webPage, TLPageBlockBase pageBlock)
        {
            if (pageBlock is TLPageBlockPhoto photoBlock)
            {
                var photo = TLWebPage.GetPhotoWithId(webPage, photoBlock.PhotoId) as TLPhoto;
                if (photo == null)
                {
                    return null;
                }

                return new GalleryPhotoItem(photo, message.From);
            }
            else if (pageBlock is TLPageBlockVideo videoBlock)
            {
                var document = TLWebPage.GetDocumentWithId(webPage, videoBlock.VideoId) as TLDocument;
                if (document == null)
                {
                    return null;
                }

                return new GalleryDocumentItem(document, message.From);
            }

            return null;
        }

        private List<GalleryItem> GetWebPagePhotos(TLMessage message, TLWebPage webPage)
        {
            var result = new List<GalleryItem>();
            var blocks = webPage.CachedPage?.Blocks ?? new TLVector<TLPageBlockBase>();

            foreach (var block in blocks)
            {
                if (block is TLPageBlockSlideshow slideshow)
                {
                    foreach (var item in slideshow.Items)
                    {
                        result.Add(GetBlock(message, webPage, item));
                    }
                }
                else if (block is TLPageBlockCollage collage)
                {
                    foreach (var item in collage.Items)
                    {
                        result.Add(GetBlock(message, webPage, item));
                    }
                }
            }

            return result;
        }

        private void OnCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            TotalItems = Items.Count;
        }
    }
}
