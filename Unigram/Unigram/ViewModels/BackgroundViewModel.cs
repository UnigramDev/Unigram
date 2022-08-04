using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Unigram.Views.Popups;
using Windows.Storage.AccessCache;
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

            ChangeRotationCommand = new RelayCommand(ChangeRotationExecute);

            ShareCommand = new RelayCommand(ShareExecute);
            DoneCommand = new RelayCommand(DoneExecute);
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            Background background = parameter as Background;

            if (parameter is string data)
            {
                var split = data.Split('#');
                if (split[0] == Constants.WallpaperLocalFileName && split.Length == 2)
                {
                    background = new Background(Constants.WallpaperLocalId, false, false, split[1], null, new BackgroundTypeWallpaper(false, false));
                }
                else if (split[0] == Constants.WallpaperColorFileName)
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
            }
            else if (background.Type is BackgroundTypePattern typePattern)
            {
                fill = typePattern.Fill;
                Intensity = typePattern.IsInverted ? -typePattern.Intensity : typePattern.Intensity;
                IsBlurEnabled = false;
            }
            else if (background.Type is BackgroundTypeWallpaper typeWallpaper)
            {
                fill = null;
                Intensity = 100;
                IsBlurEnabled = typeWallpaper.IsBlurred;
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
            else if (fill is BackgroundFillFreeformGradient freeformGradient)
            {
                Color1 = freeformGradient.Colors[0].ToColor();
                Color2 = freeformGradient.Colors[1].ToColor();
                Color3 = freeformGradient.Colors[2].ToColor();

                if (freeformGradient.Colors.Count > 3)
                {
                    Color4 = freeformGradient.Colors[3].ToColor();
                }
            }

            Delegate?.UpdateBackground(_item);

            if (_item.Type is BackgroundTypePattern or BackgroundTypeFill)
            {
                var response = await ProtoService.SendAsync(new GetBackgrounds());
                if (response is Backgrounds backgrounds)
                {
                    var patterns = backgrounds.BackgroundsValue.Where(x => x.Type is BackgroundTypePattern)
                                                               .Distinct(new EqualityComparerDelegate<Background>((x, y) =>
                                                               {
                                                                   return x.Document.DocumentValue.Id == y.Document.DocumentValue.Id;
                                                               }, obj =>
                                                               {
                                                                   return obj.Document.DocumentValue.Id;
                                                               }));

                    Patterns.ReplaceWith(new[] { new Background(0, true, false, string.Empty, null, new BackgroundTypeFill(new BackgroundFillSolid())) }.Union(patterns));
                    SelectedPattern = patterns.FirstOrDefault(x => x.Document?.DocumentValue.Id == background.Document?.DocumentValue.Id);
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
            get => _item;
            set => Set(ref _item, value);
        }

        private bool _isBlurEnabled;
        public bool IsBlurEnabled
        {
            get => _isBlurEnabled;
            set => Set(ref _isBlurEnabled, value);
        }

        private BackgroundColor _color1 = BackgroundColor.Empty;
        public BackgroundColor Color1
        {
            get => _color1;
            set => SetColor(ref _color1, value);
        }

        private BackgroundColor _color2 = BackgroundColor.Empty;
        public BackgroundColor Color2
        {
            get => _color2;
            set => SetColor(ref _color2, value);
        }

        private BackgroundColor _color3 = BackgroundColor.Empty;
        public BackgroundColor Color3
        {
            get => _color3;
            set => SetColor(ref _color3, value);
        }

        private BackgroundColor _color4 = BackgroundColor.Empty;
        public BackgroundColor Color4
        {
            get => _color4;
            set => SetColor(ref _color4, value);
        }

        private void SetColor(ref BackgroundColor storage, BackgroundColor value, [CallerMemberName] string propertyName = null)
        {
            Set(ref storage, value, propertyName);

            if (_item?.Type is BackgroundTypePattern)
            {
                //RaisePropertyChanged(() => Item);
            }
        }

        public BackgroundFill GetFill()
        {
            if (!_color1.IsEmpty && !_color2.IsEmpty)
            {
                if (!_color3.IsEmpty && !_color4.IsEmpty)
                {
                    return new BackgroundFillFreeformGradient(new[] { _color1.Value, _color2.Value, _color3.Value, _color4.Value });
                }
                else if (!_color3.IsEmpty)
                {
                    return new BackgroundFillFreeformGradient(new[] { _color1.Value, _color2.Value, _color3.Value });
                }

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

        public Color GetPatternForeground()
        {
            if (_intensity < 0)
            {
                return Colors.Black;
            }

            if (!_color1.IsEmpty && !_color2.IsEmpty)
            {
                return ColorEx.GetPatternColor(ColorEx.GetAverageColor(_color1, _color2));
            }
            else if (!_color1.IsEmpty)
            {
                return ColorEx.GetPatternColor(_color1);
            }
            else if (!_color2.IsEmpty)
            {
                return ColorEx.GetPatternColor(_color2);
            }

            return Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF);
        }

        private bool _isColor1Checked = true;
        public bool IsColor1Checked
        {
            get => _isColor1Checked;
            set => Set(ref _isColor1Checked, value);
        }

        private bool _isColor2Checked;
        public bool IsColor2Checked
        {
            get => _isColor2Checked;
            set => Set(ref _isColor2Checked, value);
        }

        private bool _isColor3Checked;
        public bool IsColor3Checked
        {
            get => _isColor3Checked;
            set => Set(ref _isColor3Checked, value);
        }

        private bool _isColor4Checked;
        public bool IsColor4Checked
        {
            get => _isColor4Checked;
            set => Set(ref _isColor4Checked, value);
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
            set
            {
                Set(ref _selectedPattern, value);

                if (value != _item && ((value != null && _item?.Type is BackgroundTypeFill) || _item?.Type is BackgroundTypePattern))
                {
                    Set(ref _item, value, nameof(Item));
                    Delegate?.UpdateBackground(value);
                }
            }
        }

        public void RemoveColor(int index)
        {
            if (index <= 0)
            {
                Color1 = Color2;
            }

            if (index <= 1)
            {
                Color2 = Color3;
            }

            if (index <= 2)
            {
                Color3 = Color4;
            }

            if (index <= 3)
            {
                Color4 = BackgroundColor.Empty;
            }

            IsColor1Checked = true;
        }

        public void AddColor()
        {
            if (Color2.IsEmpty)
            {
                Color2 = Color1;
                IsColor2Checked = true;
            }
            else if (Color3.IsEmpty)
            {
                Color3 = Color2;
                IsColor3Checked = true;
            }
            else if (Color4.IsEmpty)
            {
                Color4 = Color3;
                IsColor4Checked = true;
            }
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
                await SharePopup.GetForCurrentView().ShowAsync(new Uri(url.Url), null);
            }
        }

        public RelayCommand DoneCommand { get; }
        private async void DoneExecute()
        {
            var wallpaper = _item;
            if (wallpaper == null)
            {
                return;
            }

            var dark = Settings.Appearance.IsDarkTheme();
            var freeform = dark ? new[] { 0x1B2836, 0x121A22, 0x1B2836, 0x121A22 } : new[] { 0xDBDDBB, 0x6BA587, 0xD5D88D, 0x88B884 };

            // This is a new background and it has to be uploaded to Telegram servers
            Task<BaseObject> task;
            if (wallpaper.Id == Constants.WallpaperLocalId && StorageApplicationPermissions.FutureAccessList.ContainsItem(wallpaper.Name))
            {
                var item = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(wallpaper.Name);
                var generated = await item.ToGeneratedAsync(ConversionType.Copy, forceCopy: true);

                task = ProtoService.SendAsync(new SetBackground(new InputBackgroundLocal(generated), new BackgroundTypeWallpaper(_isBlurEnabled, false), dark));
            }
            else
            {
                var fill = GetFill();
                if (wallpaper.Type is BackgroundTypeFill && fill is BackgroundFillFreeformGradient fillFreeform && fillFreeform.Colors.SequenceEqual(freeform))
                {
                    task = ProtoService.SendAsync(new SetBackground(null, null, dark));
                }
                else
                {
                    BackgroundType type = null;
                    if (wallpaper.Type is BackgroundTypeFill)
                    {
                        type = new BackgroundTypeFill(fill);
                    }
                    else if (wallpaper.Type is BackgroundTypePattern)
                    {
                        type = new BackgroundTypePattern(fill, _intensity < 0 ? 100 + _intensity : _intensity, _intensity < 0, false);
                    }
                    else if (wallpaper.Type is BackgroundTypeWallpaper)
                    {
                        type = new BackgroundTypeWallpaper(_isBlurEnabled, false);
                    }

                    if (type == null)
                    {
                        return;
                    }

                    var input = wallpaper.Id == Constants.WallpaperLocalId
                        ? null
                        : new InputBackgroundRemote(wallpaper.Id);

                    task = ProtoService.SendAsync(new SetBackground(input, type, dark));
                }
            }

            var response = await task;
            //if (response is Background)
            //{
            //    NavigationService.GoBack();
            //}
            //if (response is Error error)
            //{
            //    if (error.Code == 404)
            //    {
            //        NavigationService.GoBack();
            //    }
            //}
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
