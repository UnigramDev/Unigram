//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Collections;
using Telegram.Services;
using Telegram.ViewModels.Gallery;

namespace Telegram.ViewModels
{
    public class SingleGalleryViewModel : GalleryViewModelBase
    {
        public SingleGalleryViewModel(IClientService clientService, IStorageService storageService, IEventAggregator aggregator, GalleryMedia item)
            : base(clientService, storageService, aggregator)
        {
            Items = new MvxObservableCollection<GalleryMedia> { item };
            SelectedItem = item;
            FirstItem = item;
        }
    }
}
