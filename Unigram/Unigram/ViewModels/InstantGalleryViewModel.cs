using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Services;
using Unigram.ViewModels.Gallery;

namespace Unigram.ViewModels
{
    public class InstantGalleryViewModel : GalleryViewModelBase
    {
        private readonly bool _shouldGroup;

        public InstantGalleryViewModel(IClientService clientService, IStorageService storageService, IEventAggregator aggregator)
            : base(clientService, storageService, aggregator)
        {
            Items = new MvxObservableCollection<GalleryContent>();
            Items.CollectionChanged += OnCollectionChanged;
        }

        public static async Task<InstantGalleryViewModel> CreateAsync(IClientService clientService, IStorageService storageService, IEventAggregator aggregator, MessageViewModel message, WebPage webPage)
        {
            var items = new List<GalleryContent>();

            var response = await clientService.SendAsync(new GetWebPageInstantView(webPage.Url, false));
            if (response is WebPageInstantView instantView && instantView.IsFull)
            {
                foreach (var block in instantView.PageBlocks)
                {
                    if (block is PageBlockSlideshow slideshow)
                    {
                        foreach (var item in slideshow.PageBlocks)
                        {
                            items.Add(CountBlock(clientService, instantView, item));
                        }
                    }
                    else if (block is PageBlockCollage collage)
                    {
                        foreach (var item in collage.PageBlocks)
                        {
                            items.Add(CountBlock(clientService, instantView, item));
                        }
                    }
                }
            }

            if (items.Count > 0)
            {
                var result = new InstantGalleryViewModel(clientService, storageService, aggregator);
                result.Items.ReplaceWith(items);
                result.FirstItem = items.FirstOrDefault();
                result.SelectedItem = items.FirstOrDefault();
                result.TotalItems = items.Count;

                return result;
            }

            return null;
        }

        private static GalleryContent CountBlock(IClientService clientService, WebPageInstantView webPage, PageBlock pageBlock)
        {
            if (pageBlock is PageBlockPhoto photoBlock)
            {
                return new GalleryPhoto(clientService, photoBlock.Photo, photoBlock.Caption.ToPlainText());
            }
            else if (pageBlock is PageBlockVideo videoBlock)
            {
                return new GalleryVideo(clientService, videoBlock.Video, videoBlock.Caption.ToPlainText());
            }
            else if (pageBlock is PageBlockAnimation animationBlock)
            {
                return new GalleryAnimation(clientService, animationBlock.Animation, animationBlock.Caption.ToPlainText());
            }

            return null;
        }

        public override MvxObservableCollection<GalleryContent> Group => _shouldGroup ? Items : null;

        //private GalleryItem GetBlock(TLMessage message, TLWebPage webPage, object pageBlock)
        //{
        //    if (pageBlock is TLPageBlockPhoto photoBlock)
        //    {
        //        var photo = TLWebPage.GetPhotoWithId(webPage, photoBlock.PhotoId) as TLPhoto;
        //        if (photo == null)
        //        {
        //            return null;
        //        }

        //        return new GalleryPhotoItem(photo, message.From);
        //    }
        //    else if (pageBlock is TLPageBlockVideo videoBlock)
        //    {
        //        var document = TLWebPage.GetDocumentWithId(webPage, videoBlock.VideoId) as TLDocument;
        //        if (document == null)
        //        {
        //            return null;
        //        }

        //        return new GalleryDocumentItem(document, message.From);
        //    }

        //    return null;
        //}

        //private List<GalleryItem> GetWebPagePhotos(TLMessage message, TLWebPage webPage)
        //{
        //    var result = new List<GalleryItem>();
        //    var blocks = webPage.CachedPage?.Blocks ?? new TLVector<TLPageBlockBase>();

        //    foreach (var block in blocks)
        //    {
        //        if (block is TLPageBlockSlideshow slideshow)
        //        {
        //            foreach (var item in slideshow.Items)
        //            {
        //                result.Add(GetBlock(message, webPage, item));
        //            }
        //        }
        //        else if (block is TLPageBlockCollage collage)
        //        {
        //            foreach (var item in collage.Items)
        //            {
        //                result.Add(GetBlock(message, webPage, item));
        //            }
        //        }
        //    }

        //    return result;
        //}

        private void OnCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            TotalItems = Items.Count;
        }
    }
}
