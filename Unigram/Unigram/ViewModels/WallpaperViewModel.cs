using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Native;
using Unigram.Services;
using Unigram.Services.Updates;
using Unigram.ViewModels.Delegates;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class WallpaperViewModel : TLViewModelBase, IDelegable<IWallpaperDelegate>
    {
        public IWallpaperDelegate Delegate { get; set; }

        public WallpaperViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            DoneCommand = new RelayCommand(DoneExecute);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (parameter is int id)
            {
                if (id == Constants.WallpaperLocalId)
                {
                    Item = new Wallpaper(Constants.WallpaperLocalId, new PhotoSize[0], 0);
                }
                else
                {
                    var response = await ProtoService.SendAsync(new GetWallpapers());
                    if (response is Wallpapers wallpapers)
                    {
                        Item = wallpapers.WallpapersValue.FirstOrDefault(x => x.Id == id);
                    }
                }

                if (id == Settings.Wallpaper.SelectedBackground)
                {
                    IsBlurEnabled = Settings.Wallpaper.IsBlurEnabled;
                    IsMotionEnabled = Settings.Wallpaper.IsMotionEnabled;
                }

                Delegate?.UpdateWallpaper(_item);
            }
        }

        private Wallpaper _item;
        public Wallpaper Item
        {
            get { return _item; }
            set { Set(ref _item, value); }
        }

        private bool _isBlurEnabled;
        public bool IsBlurEnabled
        {
            get { return _isBlurEnabled; }
            set { Set(ref _isBlurEnabled, value); }
        }

        private bool _isMotionEnabled;
        public bool IsMotionEnabled
        {
            get { return _isMotionEnabled; }
            set { Set(ref _isMotionEnabled, value); }
        }



        public RelayCommand DoneCommand { get; }
        private async void DoneExecute()
        {
            var background = 1000001;
            var color = 0;

            var wallpaper = _item;
            if (wallpaper != null && wallpaper.Id == Constants.WallpaperLocalId || wallpaper.Sizes.Count > 0)
            {
                if (wallpaper.Id != 1000001)
                {
                    StorageFile result = await ApplicationData.Current.LocalFolder.CreateFileAsync($"{SessionId}\\{Constants.WallpaperFileName}", CreationCollisionOption.ReplaceExisting);
                    StorageFile item;

                    if (wallpaper.Id == Constants.WallpaperLocalId)
                    {
                        item = await ApplicationData.Current.LocalFolder.GetFileAsync($"{SessionId}\\{Constants.WallpaperLocalFileName}");
                    }
                    else
                    {
                        var big = wallpaper.GetBig();
                        if (big == null || !big.Photo.Local.IsDownloadingCompleted)
                        {
                            return;
                        }

                        item = await StorageFile.GetFileFromPathAsync(big.Photo.Local.Path);
                    }

                    if (_isBlurEnabled)
                    {
                        using (var source = await item.OpenReadAsync())
                        using (var stream = await result.OpenAsync(FileAccessMode.ReadWrite))
                        {
                            var device = new CanvasDevice();
                            var bitmap = await CanvasBitmap.LoadAsync(device, source);

                            double ratioX = (double)450 / bitmap.SizeInPixels.Width;
                            double ratioY = (double)450 / bitmap.SizeInPixels.Height;
                            double ratio = Math.Max(ratioX, ratioY);

                            var width = (int)(bitmap.SizeInPixels.Width * ratio);
                            var height = (int)(bitmap.SizeInPixels.Height * ratio);

                            var renderer = new CanvasRenderTarget(device, width, height, bitmap.Dpi);

                            using (var ds = renderer.CreateDrawingSession())
                            {
                                var blur = new GaussianBlurEffect
                                {
                                    BlurAmount = 12.0f,
                                    Source = bitmap
                                };

                                ds.DrawImage(blur, new Rect(0, 0, width, height), new Rect(0, 0, bitmap.SizeInPixels.Width, bitmap.SizeInPixels.Height));
                            }

                            await renderer.SaveAsync(stream, CanvasBitmapFileFormat.Jpeg);
                        }
                    }
                    else
                    {
                        await item.CopyAndReplaceAsync(result);
                    }

                    var accent = await ImageHelper.GetAccentAsync(result);
                    Theme.Current.AddOrUpdateColor("MessageServiceBackgroundBrush", accent[0]);
                    Theme.Current.AddOrUpdateColor("MessageServiceBackgroundPressedBrush", accent[1]);
                }
                else
                {
                    Theme.Current.AddOrUpdateColor("MessageServiceBackgroundBrush", Color.FromArgb(0x66, 0x7A, 0x8A, 0x96));
                    Theme.Current.AddOrUpdateColor("MessageServiceBackgroundPressedBrush", Color.FromArgb(0x88, 0x7A, 0x8A, 0x96));
                }

                Settings.Wallpaper.IsBlurEnabled = _isBlurEnabled;
                Settings.Wallpaper.IsMotionEnabled = _isMotionEnabled;
                Settings.Wallpaper.SelectedBackground = background = wallpaper.Id;
                Settings.Wallpaper.SelectedColor = color = 0;
            }
            else if (wallpaper != null)
            {
                Settings.Wallpaper.IsBlurEnabled = false;
                Settings.Wallpaper.IsMotionEnabled = false;
                Settings.Wallpaper.SelectedBackground = background = wallpaper.Id;
                Settings.Wallpaper.SelectedColor = color = wallpaper.Color;
            }

            Aggregator.Publish(new UpdateWallpaper(background, color));
            NavigationService.GoBack();
        }
    }
}
