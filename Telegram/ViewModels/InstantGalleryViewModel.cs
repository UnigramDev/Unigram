//
// Copyright (c) Fela Ameghino 2015-2026
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
    public partial class InstantGalleryViewModel : GalleryViewModelBase
    {
        private readonly bool _shouldGroup;

        public InstantGalleryViewModel(IClientService clientService, IStorageService storageService, IEventAggregator aggregator)
            : base(clientService, storageService, aggregator)
        {
            Items = new RangeObservableCollection<GalleryMedia>();
            Items.CollectionChanged += OnCollectionChanged;
        }

        public static async Task<InstantGalleryViewModel> CreateAsync(IClientService clientService, IStorageService storageService, IEventAggregator aggregator, MessageViewModel message, LinkPreview linkPreview)
        {
            var items = new List<GalleryMedia>();

            var response = await clientService.SendAsync(new GetWebPageInstantView(linkPreview.Url, true));
            if (response is WebPageInstantView instantView && instantView.IsFull)
            {
                foreach (var block in instantView.Blocks)
                {
                    if (block is PageBlockSlideshow slideshow)
                    {
                        foreach (var item in slideshow.Blocks)
                        {
                            items.Add(CountBlock(clientService, instantView, item));
                        }
                    }
                    else if (block is PageBlockCollage collage)
                    {
                        foreach (var item in collage.Blocks)
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
                    items.Add(new GalleryMedia(clientService, photo.Photo));
                }
                else if (media is LinkPreviewAlbumMediaVideo video)
                {
                    items.Add(new GalleryMedia(clientService, video.Video));
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
                return new GalleryMedia(clientService, photoBlock.Photo, photoBlock.Caption?.ToFormattedText());
            }
            else if (pageBlock is PageBlockVideo videoBlock)
            {
                return new GalleryMedia(clientService, videoBlock.Video, videoBlock.Caption?.ToFormattedText());
            }
            else if (pageBlock is PageBlockAnimation animationBlock)
            {
                return new GalleryMedia(clientService, animationBlock.Animation, animationBlock.Caption?.ToFormattedText());
            }

            return null;
        }

        public override RangeObservableCollection<GalleryMedia> Group => _shouldGroup ? Items : null;

        private void OnCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            TotalItems = Items.Count;
        }
    }
}
