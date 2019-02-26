using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Services;
using Unigram.Services.Updates;
using Unigram.Views;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsWallpapersViewModel : TLViewModelBase
    {
        public SettingsWallpapersViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<Wallpaper>();

            ProtoService.Send(new GetWallpapers(), result =>
            {
                if (result is Wallpapers wallpapers)
                {
                    var items = wallpapers.WallpapersValue.ToList();
                    var id = Settings.Wallpaper.SelectedBackground;

                    var predefined = items.FirstOrDefault(x => x.Id == 1000001);
                    if (predefined != null)
                    {
                        items.Remove(predefined);
                        items.Insert(0, predefined);
                    }

                    var selected = items.FirstOrDefault(x => x.Id == id);
                    if (selected != null)
                    {
                        items.Remove(selected);
                        items.Insert(0, selected);
                    }
                    else if (id == Constants.WallpaperLocalId)
                    {
                        items.Insert(0, new Wallpaper(Constants.WallpaperLocalId, new PhotoSize[0], 0));
                    }

                    BeginOnUIThread(() =>
                    {
                        Items.ReplaceWith(items);
                        UpdateView();
                    });
                }
            });

            LocalCommand = new RelayCommand(LocalExecute);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            UpdateView();
            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        private async void UpdateView()
        {
            if (Items == null)
            {
                return;
            }

            var selected = Settings.Wallpaper.SelectedBackground;
            if (selected == -1)
            {
                IsLocal = true;
                SelectedItem = null;

                var item = await ApplicationData.Current.LocalFolder.TryGetItemAsync($"{SessionId}\\{Constants.WallpaperFileName}");
                if (item is StorageFile file)
                {
                    using (var stream = await file.OpenReadAsync())
                    {
                        var bitmap = new BitmapImage();
                        try
                        {
                            await bitmap.SetSourceAsync(stream);
                        }
                        catch { }
                        Local = bitmap;
                    }
                }
            }
            else
            {
                SelectedItem = Items.FirstOrDefault(x => x.Id == selected) ?? Items.FirstOrDefault(x => x.Id == 1000001);
            }
        }

        private bool _isLocal;
        public bool IsLocal
        {
            get
            {
                return _isLocal;
            }
            set
            {
                Set(ref _isLocal, value);
            }
        }

        private StorageFile _localFile;

        private BitmapImage _local;
        public BitmapImage Local
        {
            get
            {
                return _local;
            }
            set
            {
                Set(ref _local, value);
            }
        }

        private Wallpaper _selectedItem;
        public Wallpaper SelectedItem
        {
            get
            {
                return _selectedItem;
            }
            set
            {
                Set(ref _selectedItem, value);

                if (value != null)
                {
                    Local = null;
                    IsLocal = false;
                }
            }
        }

        public MvxObservableCollection<Wallpaper> Items { get; private set; }

        public RelayCommand LocalCommand { get; }
        private async void LocalExecute()
        {
            var picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.AddRange(Constants.PhotoTypes);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                var result = await ApplicationData.Current.LocalFolder.CreateFileAsync($"{SessionId}\\{Constants.WallpaperLocalFileName}", CreationCollisionOption.ReplaceExisting);
                await file.CopyAndReplaceAsync(result);

                NavigationService.Navigate(typeof(WallpaperPage), Constants.WallpaperLocalId);
            }
        }
    }
}
