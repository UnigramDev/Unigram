//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using Telegram.Collections;
using Telegram.Services;
using Telegram.ViewModels.Gallery;

namespace Telegram.ViewModels
{
    public partial class StandaloneGalleryViewModel : GalleryViewModelBase
    {
        public StandaloneGalleryViewModel(IClientService clientService, IStorageService storageService, IEventAggregator aggregator, GalleryMedia item)
            : base(clientService, storageService, aggregator)
        {
            Items = new MvxObservableCollection<GalleryMedia> { item };
            SelectedItem = item;
            FirstItem = item;
        }

        public StandaloneGalleryViewModel(IClientService clientService, IStorageService storageService, IEventAggregator aggregator, IList<GalleryMedia> items, GalleryMedia item)
            : base(clientService, storageService, aggregator)
        {
            Items = new MvxObservableCollection<GalleryMedia>(items);
            SelectedItem = item;
            FirstItem = item;
        }
    }
}
