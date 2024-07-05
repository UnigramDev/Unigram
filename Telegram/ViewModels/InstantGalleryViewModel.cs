//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Gallery;

namespace Telegram.ViewModels
{
    public class InstantGalleryViewModel : GalleryViewModelBase
    {
        private readonly bool _shouldGroup;

        public InstantGalleryViewModel(IClientService clientService, IStorageService storageService, IEventAggregator aggregator)
            : base(clientService, storageService, aggregator)
        {
            Items = new MvxObservableCollection<GalleryMedia>();
            Items.CollectionChanged += OnCollectionChanged;
        }

        public static async Task<InstantGalleryViewModel> CreateAsync(IClientService clientService, IStorageService storageService, IEventAggregator aggregator, MessageViewModel message, LinkPreview linkPreview)
        {
            var items = new List<GalleryMedia>();

            var response = await clientService.SendAsync(new GetWebPageInstantView(linkPreview.Url, false));
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

        public static InstantGalleryViewModel Create(IClientService clientService, IStorageService storageService, IEventAggregator aggregator, MessageViewModel message, LinkPreviewTypeAlbum album)
        {
            var items = new List<GalleryMedia>();

            foreach (var media in album.Media)
            {
                if (media is LinkPreviewAlbumMediaPhoto photo)
                {
                    items.Add(new GalleryPhoto(clientService, photo.Photo));
                }
                else if (media is LinkPreviewAlbumMediaVideo video)
                {
                    items.Add(new GalleryVideo(clientService, video.Video));
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

        private static GalleryMedia CountBlock(IClientService clientService, WebPageInstantView linkPreview, PageBlock pageBlock)
        {
            if (pageBlock is PageBlockPhoto photoBlock)
            {
                return new GalleryPhoto(clientService, photoBlock.Photo, photoBlock.Caption.ToFormattedText());
            }
            else if (pageBlock is PageBlockVideo videoBlock)
            {
                return new GalleryVideo(clientService, videoBlock.Video, videoBlock.Caption.ToFormattedText());
            }
            else if (pageBlock is PageBlockAnimation animationBlock)
            {
                return new GalleryAnimation(clientService, animationBlock.Animation, animationBlock.Caption.ToFormattedText());
            }

            return null;
        }

        public override MvxObservableCollection<GalleryMedia> Group => _shouldGroup ? Items : null;

        //private GalleryItem GetBlock(TLMessage message, TLWebPage linkPreview, object pageBlock)
        //{
        //    if (pageBlock is TLPageBlockPhoto photoBlock)
        //    {
        //        var photo = TLWebPage.GetPhotoWithId(linkPreview, photoBlock.PhotoId) as TLPhoto;
        //        if (photo == null)
        //        {
        //            return null;
        //        }

        //        return new GalleryPhotoItem(photo, message.From);
        //    }
        //    else if (pageBlock is TLPageBlockVideo videoBlock)
        //    {
        //        var document = TLWebPage.GetDocumentWithId(linkPreview, videoBlock.VideoId) as TLDocument;
        //        if (document == null)
        //        {
        //            return null;
        //        }

        //        return new GalleryDocumentItem(document, message.From);
        //    }

        //    return null;
        //}

        //private List<GalleryItem> GetWebPagePhotos(TLMessage message, TLWebPage linkPreview)
        //{
        //    var result = new List<GalleryItem>();
        //    var blocks = linkPreview.CachedPage?.Blocks ?? new TLVector<TLPageBlockBase>();

        //    foreach (var block in blocks)
        //    {
        //        if (block is TLPageBlockSlideshow slideshow)
        //        {
        //            foreach (var item in slideshow.Items)
        //            {
        //                result.Add(GetBlock(message, linkPreview, item));
        //            }
        //        }
        //        else if (block is TLPageBlockCollage collage)
        //        {
        //            foreach (var item in collage.Items)
        //            {
        //                result.Add(GetBlock(message, linkPreview, item));
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
