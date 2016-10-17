using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Unigram.Common;
using Unigram.Models;
using Windows.Storage.Pickers;

namespace Unigram.ViewModels
{
    public class SendPhotosViewModel : UnigramViewModelBase
    {
        public SendPhotosViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
        }

        private StoragePhoto _selectedItem;
        public StoragePhoto SelectedItem
        {
            get
            {
                return _selectedItem;
            }
            set
            {
                Set(ref _selectedItem, value);
            }
        }

        public ObservableCollection<StoragePhoto> Items { get; set; }

        public RelayCommand MoreCommand => new RelayCommand(MoreExecute);
        private async void MoreExecute()
        {
            var picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.AddRange(Constants.MediaTypes);

            var files = await picker.PickMultipleFilesAsync();
            if (files != null)
            {
                foreach (var file in files)
                {
                    Items.Add(new StoragePhoto(file));
                }
            }
        }

        public RelayCommand RemoveCommand => new RelayCommand(RemoveExecute);
        private void RemoveExecute()
        {
            if (SelectedItem != null)
            {
                var index = Items.IndexOf(SelectedItem);
                var next = index > 0 ? Items[index - 1] : null;
                var previous = index < Items.Count - 1 ? Items[index + 1] : null;

                Items.Remove(SelectedItem);

                if (next != null)
                {
                    SelectedItem = next;
                }
                else
                {
                    SelectedItem = previous;
                }
            }
        }
    }
}
