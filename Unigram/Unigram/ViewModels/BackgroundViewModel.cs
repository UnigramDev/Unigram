using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls.Views;
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
    public class BackgroundViewModel : TLViewModelBase, IDelegable<IBackgroundDelegate>
    {
        public IBackgroundDelegate Delegate { get; set; }

        public BackgroundViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Patterns = new MvxObservableCollection<Background>();

            RemoveColor1Command = new RelayCommand(RemoveColor1Execute);
            RemoveColor2Command = new RelayCommand(RemoveColor2Execute);
            AddColorCommand = new RelayCommand(AddColorExecute);
            ChangeRotationCommand = new RelayCommand(ChangeRotationExecute);

            ShareCommand = new RelayCommand(ShareExecute);
            DoneCommand = new RelayCommand(DoneExecute);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            Background background = null;

            if (parameter as string == Constants.WallpaperLocalFileName)
            {
                background = new Background(Constants.WallpaperLocalId, false, false, Constants.WallpaperLocalFileName, null, new BackgroundTypeWallpaper(false, false));
            }
            else if (parameter as string == Constants.WallpaperColorFileName)
            {
                background = new Background(Constants.WallpaperLocalId, false, false, Constants.WallpaperColorFileName, null, new BackgroundTypeFill(new BackgroundFillSolid(0xdfe4e8)));
            }
            else if (Uri.TryCreate("tg://bg/" + parameter, UriKind.Absolute, out Uri uri))
            {
                var type = TdBackground.FromUri(uri);
                if (type is BackgroundTypeFill)
                {
                    background = new Background(0, false, false, string.Empty, null, type);
                }
                else
                {
                    var response = await ProtoService.SendAsync(new SearchBackground(uri.Segments.Last()));
                    if (response is Background)
                    {
                        background = response as Background;
                        background.Type = type;
                    }
                    else if (response is Error error)
                    {

                    }
                }
            }

            if (background == null)
            {
                return;
            }

            Item = background;

            BackgroundFill fill = null;
            if (background.Type is BackgroundTypeFill typeFill)
            {
                fill = typeFill.Fill;
                Intensity = 100;
                IsBlurEnabled = false;
                IsMotionEnabled = false;
            }
            else if (background.Type is BackgroundTypePattern typePattern)
            {
                fill = typePattern.Fill;
                Intensity = typePattern.Intensity;
                IsBlurEnabled = false;
                IsMotionEnabled = typePattern.IsMoving;
            }
            else if (background.Type is BackgroundTypeWallpaper typeWallpaper)
            {
                fill = null;
                Intensity = 100;
                IsBlurEnabled = typeWallpaper.IsBlurred;
                IsMotionEnabled = typeWallpaper.IsMoving;
            }

            if (fill is BackgroundFillSolid fillSolid)
            {
                Color1 = fillSolid.Color.ToColor();
                Color2 = BackgroundColor.Empty;
                Rotation = 0;
            }
            else if (fill is BackgroundFillGradient fillGradient)
            {
                Color1 = fillGradient.TopColor.ToColor();
                Color2 = fillGradient.BottomColor.ToColor();
                Rotation = fillGradient.RotationAngle;
            }

            if (_item?.Id == Settings.Wallpaper.SelectedBackground)
            {
                IsBlurEnabled = Settings.Wallpaper.IsBlurEnabled;
                IsMotionEnabled = Settings.Wallpaper.IsMotionEnabled;
            }

            Delegate?.UpdateBackground(_item);

            if (_item.Type is BackgroundTypePattern || _item.Type is BackgroundTypeFill)
            {
                var response = await ProtoService.SendAsync(new GetBackgrounds());
                if (response is Backgrounds backgrounds)
                {
                    Patterns.ReplaceWith(new[] { new Background(0, true, false, string.Empty, null, new BackgroundTypeFill(new BackgroundFillSolid())) }.Union(backgrounds.BackgroundsValue.Where(x => x.Type is BackgroundTypePattern)));
                    SelectedPattern = backgrounds.BackgroundsValue.FirstOrDefault(x => x.Id == background.Id);
                }
            }

            //if (parameter is string name)
            //{
            //    if (name == Constants.WallpaperLocalFileName)
            //    {
            //        //Item = new Background(Constants.WallpaperLocalId, new PhotoSize[0], 0);
            //    }
            //    else
            //    {
            //        var response = await ProtoService.SendAsync(new SearchBackground(name));
            //        if (response is Background background)
            //        {
            //        }
            //    }

            //}
        }

        public MvxObservableCollection<Background> Patterns { get; private set; }

        private Background _item;
        public Background Item
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

        private BackgroundColor _color1;
        public BackgroundColor Color1
        {
            get => _color1;
            set => Set(ref _color1, value);
        }

        private BackgroundColor _color2;
        public BackgroundColor Color2
        {
            get => _color2;
            set => Set(ref _color2, value);
        }

        public BackgroundFill GetFill()
        {
            if (!_color1.IsEmpty && !_color2.IsEmpty)
            {
                return new BackgroundFillGradient(_color1.Value, _color2.Value, _rotation);
            }
            else if (!_color1.IsEmpty)
            {
                return new BackgroundFillSolid(_color1.Value);
            }
            else if (!_color2.IsEmpty)
            {
                return new BackgroundFillSolid(_color2.Value);
            }

            return null;
        }

        private bool _isColor1Checked = true;
        public bool IsColor1Checked
        {
            get => _isColor1Checked;
            set => Set(ref _isColor1Checked, value);
        }

        private bool _isColor2Checked = true;
        public bool IsColor2Checked
        {
            get => _isColor2Checked;
            set => Set(ref _isColor2Checked, value);
        }

        private int _rotation;
        public int Rotation
        {
            get => _rotation;
            set => Set(ref _rotation, value);
        }

        private int _intensity;
        public int Intensity
        {
            get => _intensity;
            set => Set(ref _intensity, value);
        }

        private Background _selectedPattern;
        public Background SelectedPattern
        {
            get => _selectedPattern;
            set { Set(ref _selectedPattern, value); if (_item?.Type is BackgroundTypeFill || _item?.Type is BackgroundTypePattern) Set(() => Item, ref _item, value); }
        }

        public RelayCommand RemoveColor1Command { get; }
        private void RemoveColor1Execute()
        {
            Color1 = Color2;
            Color2 = BackgroundColor.Empty;
            IsColor1Checked = true;
        }

        public RelayCommand RemoveColor2Command { get; }
        private void RemoveColor2Execute()
        {
            Color2 = BackgroundColor.Empty;
            IsColor1Checked = true;
        }

        public RelayCommand AddColorCommand { get; }
        private void AddColorExecute()
        {
            Color2 = Color1;
            IsColor2Checked = true;
        }

        public RelayCommand ChangeRotationCommand { get; }
        private void ChangeRotationExecute()
        {
            Rotation = (_rotation + 45) % 360;
        }

        public RelayCommand ShareCommand { get; }
        private async void ShareExecute()
        {
            var background = _item;
            if (background == null)
            {
                return;
            }

            var response = await ProtoService.SendAsync(new GetBackgroundUrl(background.Name, background.Type));
            if (response is HttpUrl url)
            {
                await ShareView.GetForCurrentView().ShowAsync(new Uri(url.Url), null);
            }
        }

        public RelayCommand DoneCommand { get; }
        private async void DoneExecute()
        {
            var background = _item;
            if (background == null)
            {
                return;
            }

            // This is a new background and it has to be uploaded to Telegram servers
            Task<BaseObject> task;
            if (background.Id == Constants.WallpaperLocalId && background.Name == Constants.WallpaperLocalFileName)
            {
                var item = await ApplicationData.Current.LocalFolder.GetFileAsync($"{SessionId}\\{Constants.WallpaperLocalFileName}");
                task = ProtoService.SendAsync(new SetBackground(new InputBackgroundLocal(new InputFileLocal(item.Path)), new BackgroundTypeWallpaper(_isBlurEnabled, _isMotionEnabled), Settings.Appearance.IsDarkTheme()));
            }
            else
            {
                BackgroundType type = null;
                if (background.Type is BackgroundTypeFill)
                {
                    type = new BackgroundTypeFill(GetFill());
                }
                else if (background.Type is BackgroundTypePattern)
                {
                    type = new BackgroundTypePattern(GetFill(), _intensity, _isMotionEnabled);
                }
                else if (background.Type is BackgroundTypeWallpaper)
                {
                    type = new BackgroundTypeWallpaper(_isBlurEnabled, _isMotionEnabled);
                }

                if (type == null)
                {
                    return;
                }

                task = ProtoService.SendAsync(new SetBackground(new InputBackgroundRemote(background.Id), type, Settings.Appearance.IsDarkTheme()));
            }

            var response = await task;
            if (response is Background result)
            {
                NavigationService.GoBack();
            }
            else if (response is Error error)
            {

            }
        }
    }

    public struct BackgroundColor
    {
        private BackgroundColor(int value, bool empty)
        {
            Value = value;
            IsEmpty = empty;
        }

        public static BackgroundColor FromValue(int value)
        {
            return new BackgroundColor(value, false);
        }

        public static BackgroundColor Empty = new BackgroundColor(0, true);

        public int Value;

        public bool IsEmpty;

        public static implicit operator Color(BackgroundColor rhs)
        {
            if (rhs.IsEmpty)
            {
                return Color.FromArgb(0, 0, 0, 0);
            }

            return rhs.Value.ToColor();
        }

        public static implicit operator BackgroundColor(Color lhs)
        {
            return FromValue((lhs.R << 16) + (lhs.G << 8) + lhs.B);
        }
    }
}
