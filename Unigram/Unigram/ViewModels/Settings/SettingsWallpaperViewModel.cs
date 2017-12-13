using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Core.Common;
using Unigram.Core.Helpers;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsWallPaperViewModel : UnigramViewModelBase
    {
        private const string TempWallpaperFileName = "temp_wallpaper.jpg";

        public SettingsWallPaperViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            Items = new MvxObservableCollection<TLWallPaperBase>();
            ProtoService.GetWallpapersAsync(result =>
            {
                var defa = result.FirstOrDefault(x => x.Id == 1000001);
                if (defa != null)
                {
                    result.Remove(defa);
                    result.Insert(0, defa);
                }

                BeginOnUIThread(() =>
                {
                    Items.ReplaceWith(result);
                    UpdateView();
                });
            });

            LocalCommand = new RelayCommand(LocalExecute);
            DoneCommand = new RelayCommand(DoneExecute);
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

            var selected = ApplicationSettings.Current.SelectedBackground;
            if (selected == -1)
            {
                IsLocal = true;
                SelectedItem = null;

                var item = await ApplicationData.Current.LocalFolder.TryGetItemAsync(FileUtils.GetFilePath(Constants.WallpaperFileName));
                if (item is StorageFile file)
                {
                    using (var stream = await file.OpenReadAsync())
                    {
                        var bitmap = new BitmapImage();
                        await bitmap.SetSourceAsync(stream);
                        Local = bitmap;
                    }
                }
            }
            else
            {
                SelectedItem = Items.FirstOrDefault(x => x.Id == selected) ?? Items.FirstOrDefault(x => x.Id == 1000001) ?? new TLWallPaper { Id = 1000001 };
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

        private TLWallPaperBase _selectedItem;
        public TLWallPaperBase SelectedItem
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

        public MvxObservableCollection<TLWallPaperBase> Items { get; private set; }

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
                var result = await FileUtils.CreateTempFileAsync(TempWallpaperFileName);
                await file.CopyAndReplaceAsync(result);

                IsLocal = true;
                SelectedItem = null;

                using (var stream = await result.OpenReadAsync())
                {
                    var bitmap = new BitmapImage();
                    await bitmap.SetSourceAsync(stream);
                    Local = bitmap;
                }
            }
        }

        public RelayCommand DoneCommand { get; }
        private async void DoneExecute()
        {
            if (_selectedItem is TLWallPaper wallpaper)
            {
                if (wallpaper.Id != 1000001)
                {
                    var photoSize = wallpaper.Full as TLPhotoSize;
                    var location = photoSize.Location as TLFileLocation;
                    var fileName = string.Format("{0}_{1}_{2}.jpg", location.VolumeId, location.LocalId, location.Secret);

                    var item = await ApplicationData.Current.LocalFolder.TryGetItemAsync(FileUtils.GetTempFilePath(fileName));
                    if (item is StorageFile file)
                    {
                        var result = await FileUtils.CreateFileAsync(Constants.WallpaperFileName);
                        await file.CopyAndReplaceAsync(result);

                        var accent = await ImageHelper.GetAccentAsync(result);
                        Theme.Current.AddOrUpdateColor("MessageServiceBackgroundBrush", accent[0]);
                        Theme.Current.AddOrUpdateColor("MessageServiceBackgroundPressedBrush", accent[1]);
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    Theme.Current.AddOrUpdateColor("MessageServiceBackgroundBrush", Color.FromArgb(0x66, 0x7A, 0x8A, 0x96));
                    Theme.Current.AddOrUpdateColor("MessageServiceBackgroundPressedBrush", Color.FromArgb(0x88, 0x7A, 0x8A, 0x96));
                }

                ApplicationSettings.Current.SelectedBackground = wallpaper.Id;
                ApplicationSettings.Current.SelectedColor = 0;
            }
            else if (_selectedItem is TLWallPaperSolid solid)
            {
                ApplicationSettings.Current.SelectedBackground = solid.Id;
                ApplicationSettings.Current.SelectedColor = solid.BgColor;
            }
            else if (_selectedItem == null && _isLocal)
            {
                var item = await ApplicationData.Current.LocalFolder.TryGetItemAsync(FileUtils.GetTempFilePath(TempWallpaperFileName));
                if (item is StorageFile file)
                {
                    var result = await FileUtils.CreateFileAsync(Constants.WallpaperFileName);
                    await file.CopyAndReplaceAsync(result);

                    var accent = await ImageHelper.GetAccentAsync(result);
                    Theme.Current.AddOrUpdateColor("MessageServiceBackgroundBrush", accent[0]);
                    Theme.Current.AddOrUpdateColor("MessageServiceBackgroundPressedBrush", accent[1]);
                }
                else
                {
                    return;
                }

                ApplicationSettings.Current.SelectedBackground = -1;
                ApplicationSettings.Current.SelectedColor = 0;
            }

            Aggregator.Publish("Wallpaper");
            NavigationService.GoBack();
        }
    }
}
