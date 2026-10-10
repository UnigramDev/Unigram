//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.Views.Popups;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels
{
    public partial class BackgroundInfo
    {
        public InputBackground Background { get; set; }
        public BackgroundType Type { get; set; }
        public bool ForDarkTheme { get; set; }

        public BackgroundInfo(InputBackground background, BackgroundType type, bool forDarkTheme)
        {
            Background = background;
            Type = type;
            ForDarkTheme = forDarkTheme;
        }
    }

    public partial class BackgroundParameters
    {
        public string Slug { get; }

        public Background Background { get; }

        public ThemeSettings Settings { get; }

        /// <summary>
        /// The built-in whose variant is edited; null outside the themes page.
        /// </summary>
        public TelegramThemeType? ThemeType { get; }

        /// <summary>
        /// The variant edited, or null for a new one made with "+", which only exists once saved.
        /// </summary>

        public string Variant { get; }

        public long? ChatId { get; }

        public long? MessageId { get; }

        public BackgroundParameters(string slug, long? chatId = null)
        {
            Slug = slug;
            ChatId = chatId;
        }

        public BackgroundParameters(Background background, long? chatId = null, long? messageId = null)
        {
            Background = background;
            ChatId = chatId;
            MessageId = messageId;
        }

        public BackgroundParameters(TelegramThemeType type, string variant, ThemeSettings settings)
        {
            ThemeType = type;
            Variant = variant;
            Settings = settings;
        }
    }

    public partial class PatternInfo
    {
        public long BackgroundId { get; }

        public Document Document { get; }

        public PatternInfo(long backgroundId, Document document)
        {
            BackgroundId = backgroundId;
            Document = document;
        }
    }

    public partial class BackgroundViewModel : ViewModelBase, IDelegable<IBackgroundDelegate>
    {
        public IBackgroundDelegate Delegate { get; set; }

        private Background _background;

        private TelegramThemeType? _themeType;
        private string _variant;

        private long? _chatId;
        private long? _messageId;

        private bool _batchUpdate;

        public BackgroundViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Patterns = new RangeObservableCollection<PatternInfo>();
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            BackgroundParameters data = parameter as BackgroundParameters;
            Background background = data?.Background;
            ThemeSettings settings = data?.Settings;

            if (data?.Slug != null)
            {
                var split = data.Slug.Split('#');
                if (split[0] == Constants.WallpaperLocalFileName)
                {
                    var local = TdExtensions.GetLocalFile(System.IO.Path.Combine(ApplicationData.Current.TemporaryFolder.Path, Constants.WallpaperLocalFileName));
                    var document = new Document(Constants.WallpaperLocalFileName, "image/jpeg", null, null, local);

                    background = new Background(Constants.WallpaperLocalId, false, false, Constants.WallpaperLocalFileName, document, new BackgroundTypeWallpaper(false, false));
                }
                else if (split[0] == Constants.WallpaperColorFileName)
                {
                    background = new Background(Constants.WallpaperColorId, false, false, Constants.WallpaperColorFileName, null, new BackgroundTypeFill(new BackgroundFillSolid(0xdfe4e8)));
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
                        var response = await ClientService.SendAsync(new SearchBackground(uri.Segments.Last()));
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

            background ??= settings?.Background;

            if (background == null)
            {
                return;
            }

            Item = background;
            ThemeSettings = settings;

            _background = background;
            _themeType = data?.ThemeType;
            _variant = data?.Variant;

            _chatId = data?.ChatId;
            _messageId = data?.MessageId;

            _batchUpdate = true;

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
                BackgroundColors = [fillSolid.Color.ToColor()];
                Rotation = 0;
            }
            else if (fill is BackgroundFillGradient fillGradient)
            {
                BackgroundColors = [fillGradient.TopColor.ToColor(), fillGradient.BottomColor.ToColor()];
                Rotation = fillGradient.RotationAngle;
            }
            else if (fill is BackgroundFillFreeformGradient freeformGradient)
            {
                BackgroundColors = [.. freeformGradient.Colors.Select(x => x.ToColor())];
                Rotation = 0;
            }

            if (settings != null)
            {
                if (settings.HasOutgoingMessageAccentColor)
                {
                    AccentColors = [settings.AccentColor.ToColor(), settings.OutgoingMessageAccentColor.ToColor()];
                }
                else
                {
                    AccentColors = [settings.AccentColor.ToColor()];
                }

                if (settings.OutgoingMessageFill is BackgroundFillSolid outgoingFillSolid)
                {
                    MessageColors = [outgoingFillSolid.Color.ToColor()];
                }
                else if (settings.OutgoingMessageFill is BackgroundFillGradient outgoingFillGradient)
                {
                    MessageColors = [outgoingFillGradient.TopColor.ToColor(), outgoingFillGradient.BottomColor.ToColor()];
                }
                else if (settings.OutgoingMessageFill is BackgroundFillFreeformGradient outgoingFreeformGradient)
                {
                    MessageColors = [.. outgoingFreeformGradient.Colors.Select(x => x.ToColor())];
                }
            }

            _batchUpdate = false;
            Delegate?.UpdateBackground(_item);

            if (_item.Type is BackgroundTypePattern or BackgroundTypeFill)
            {
                var response = await ClientService.SendAsync(new GetInstalledBackgrounds(background.IsDark));
                if (response is Backgrounds backgrounds)
                {
                    var empty = new PatternInfo(0, null);
                    var patterns = backgrounds.BackgroundsValue.Where(x => x.Type is BackgroundTypePattern)
                                                               .Distinct(new EqualityComparerDelegate<Background>((x, y) =>
                                                               {
                                                                   return x.Document.DocumentValue.Id == y.Document.DocumentValue.Id;
                                                               }, obj =>
                                                               {
                                                                   return obj.Document.DocumentValue.Id;
                                                               }))
                                                               .Select(x => new PatternInfo(x.Id, x.Document));

                    Patterns.ReplaceWith([empty, .. patterns]);

                    _selectedPattern = Patterns.FirstOrDefault(x => x?.Document?.DocumentValue.Id == background.Document?.DocumentValue.Id);
                    RaisePropertyChanged(nameof(SelectedPattern));
                }
            }
        }

        public long ChatId => _chatId ?? 0;

        public RangeObservableCollection<PatternInfo> Patterns { get; private set; }

        private Background _item;
        public Background Item
        {
            get => _item;
            set => Set(ref _item, value);
        }

        private ThemeSettings _themeSettings;
        public ThemeSettings ThemeSettings
        {
            get => _themeSettings;
            set => Set(ref _themeSettings, value);
        }

        private bool _isBlurEnabled;
        public bool IsBlurEnabled
        {
            get => _isBlurEnabled;
            set => SetComponent(ref _isBlurEnabled, value);
        }

        private IList<Color> _backgroundColors;
        public IList<Color> BackgroundColors
        {
            get => _backgroundColors;
            set
            {
                if (SetComponent(ref _backgroundColors, value))
                {
                    Delegate?.UpdateBackgroundColors(value);
                }
            }
        }

        private IList<Color> _accentColors;
        public IList<Color> AccentColors
        {
            get => _accentColors;
            set
            {
                if (SetThemeSetting(ref _accentColors, value))
                {
                    Delegate?.UpdateAccentColors(value);
                }
            }
        }

        private IList<Color> _messageColors;
        public IList<Color> MessageColors
        {
            get => _messageColors;
            set
            {
                if (SetThemeSetting(ref _messageColors, value))
                {
                    Delegate?.UpdateMessageColors(value);
                }
            }
        }

        private bool SetComponent<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (_batchUpdate)
            {
                return Set(ref storage, value, propertyName);
            }

            if (Set(ref storage, value, propertyName))
            {
                Item = new Background(Item.Id, false, Item.IsDark, Item.Name, Item.Document, Item.Type switch
                {
                    BackgroundTypeFill => new BackgroundTypeFill(GetFill()),
                    BackgroundTypePattern => new BackgroundTypePattern(GetFill(), Math.Abs(_intensity), _intensity < 0, false),
                    BackgroundTypeWallpaper => new BackgroundTypeWallpaper(_isBlurEnabled, false),
                    _ => null
                });

                ThemeSettings?.Background = Item;
                return true;
            }

            return false;
        }

        private bool SetThemeSetting<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Set(ref storage, value, propertyName))
            {
                ThemeSettings = BuildThemeSettings();
                Delegate?.UpdateThemeSettings(ThemeSettings);
                return true;
            }

            return false;
        }

        private ThemeSettings BuildThemeSettings()
        {
            if (_themeSettings == null)
            {
                return null;
            }

            var accentColor = AccentColors[0].ToValue();
            var outgoingMessageFill = GetFill(MessageColors);
            var hasOutgoingMessageAccentColor = AccentColors.Count > 1;
            var outgoingMessageAccentColor = AccentColors[^1].ToValue();

            return new ThemeSettings(_themeSettings.BaseTheme, accentColor, Item, outgoingMessageFill, false, hasOutgoingMessageAccentColor, outgoingMessageAccentColor);
        }

        public BackgroundFill GetFill()
        {
            return GetFill(_backgroundColors);
        }

        public BackgroundFill GetFill(IList<Color> colors)
        {
            if (colors == null)
            {
                return null;
            }

            if (colors.Count >= 2)
            {
                if (colors.Count >= 3)
                {
                    return new BackgroundFillFreeformGradient(colors.Select(x => x.ToValue()).ToVector());
                }

                return new BackgroundFillGradient(colors[0].ToValue(), colors[1].ToValue(), _rotation);
            }
            else if (colors.Count > 0)
            {
                return new BackgroundFillSolid(colors[0].ToValue());
            }

            return null;
        }

        private int _rotation;
        public int Rotation
        {
            get => _rotation;
            set => SetComponent(ref _rotation, value);
        }

        private int _intensity;
        public int Intensity
        {
            get => _intensity;
            set => SetComponent(ref _intensity, value);
        }

        private PatternInfo _selectedPattern;
        public PatternInfo SelectedPattern
        {
            get => _selectedPattern;
            set
            {
                Set(ref _selectedPattern, value);

                if (value?.Document?.DocumentValue.Id != _item.Document?.DocumentValue.Id && ((value != null && _item?.Type is BackgroundTypeFill) || _item?.Type is BackgroundTypePattern))
                {
                    if (value == null)
                    {
                        Item = new Background(0, false, Item.IsDark, Item.Name, null, new BackgroundTypeFill(GetFill()));
                    }
                    else
                    {
                        Item = new Background(value.BackgroundId, false, Item.IsDark, Item.Name, value.Document, new BackgroundTypePattern(GetFill(), _intensity < 0 ? 100 + _intensity : _intensity, _intensity < 0, false));
                    }

                    // As SetComponent does: a variant being edited saves ThemeSettings, not Item.
                    ThemeSettings?.Background = Item;
                    Delegate?.UpdateBackground(Item);
                }
            }
        }

        public Background GetPattern(Document value)
        {
            if (value == null)
            {
                return new Background(Item.Id, false, Item.IsDark, Item.Name, null, new BackgroundTypeFill(GetFill()));
            }

            return new Background(Item.Id, false, Item.IsDark, Item.Name, value, new BackgroundTypePattern(GetFill(), 50, _intensity < 0, false));
        }

        public void ChangeRotation()
        {
            Rotation = (_rotation + 45) % 360;
        }

        public async void Share()
        {
            var background = _item;
            if (background == null)
            {
                return;
            }

            var response = await ClientService.SendAsync(new GetBackgroundUrl(background.Name, background.Type));
            if (response is HttpUrl url)
            {
                await ShowPopupAsync(new ChooseChatsPopup(), new ChooseChatsConfigurationPostLink(url));
            }
        }

        public async void Done(bool onlySelf)
        {
            var background = await GetBackgroundAsync();
            if (background != null)
            {
                // Worn before the marker below is written: the marker names what the slot wears,
                // and that is where the update is stored when it arrives.
                if (_themeType is TelegramThemeType themeType && ThemeSettings != null)
                {
                    // Created here and not when the popup opened, so cancelling leaves nothing.
                    var variant = _variant ?? AppSettings.Appearance.CreateVariant(themeType, ThemeSettings);
                    if (_variant != null)
                    {
                        AppSettings.Appearance.SaveVariant(themeType, variant, ThemeSettings);
                    }

                    AppSettings.Appearance.WearVariant(themeType, variant, null, true);
                    NightModeService.Current.Show(AppearanceSettings.GetBase(themeType), XamlRoot);
                }

                if (_chatId is long chatId)
                {
                    if (_messageId is long messageId && background.Type.GetType() == _background?.Type.GetType())
                    {
                        ClientService.Send(new SetChatBackground(chatId, new InputBackgroundPrevious(messageId), background.Type, 30, onlySelf));
                    }
                    else
                    {
                        ClientService.Send(new SetChatBackground(chatId, background.Background, background.Type, 30, onlySelf));
                    }
                }
                else if (background.Background == null && background.Type == null)
                {
                    AppSettings.Appearance.DeleteDefaultBackground(Session.Id, background.ForDarkTheme);
                    ClientService.Send(new DeleteDefaultBackground(background.ForDarkTheme));
                }
                else
                {
                    AppSettings.Appearance.SetDefaultBackground(Session.Id, background.ForDarkTheme);
                    ClientService.Send(new SetDefaultBackground(background.Background, background.Type, background.ForDarkTheme));
                }
            }
        }

        public async Task<BackgroundInfo> GetBackgroundAsync()
        {
            var background = _item;
            if (background == null)
            {
                return null;
            }

            var dark = NightModeService.Current.IsDarkTheme();
            var freeform = dark ? new[] { 0x6C7FA6, 0x2E344B, 0x7874A7, 0x333258 } : new[] { 0xDBDDBB, 0x6BA587, 0xD5D88D, 0x88B884 };

            // This is a new background and it has to be uploaded to Telegram servers
            if (background.Id == Constants.WallpaperLocalId)
            {
                try
                {
                    var item = await ApplicationData.Current.TemporaryFolder.GetFileAsync(Constants.WallpaperLocalFileName);
                    var generated = await GenerationService.PrepareAsync(item, ConversionType.Copy, forceCopy: true);

                    return new BackgroundInfo(new InputBackgroundLocal(generated), new BackgroundTypeWallpaper(_isBlurEnabled, false), dark);
                }
                catch
                {
                    return null;
                }
            }
            else
            {
                var fill = GetFill();
                if (background.Type is BackgroundTypePattern && fill is BackgroundFillFreeformGradient fillFreeform && fillFreeform.Colors.SequenceEqual(freeform))
                {
                    return new BackgroundInfo(null, null, dark);
                }
                else
                {
                    BackgroundType type = null;
                    if (background.Type is BackgroundTypeFill)
                    {
                        type = new BackgroundTypeFill(fill);
                    }
                    else if (background.Type is BackgroundTypePattern)
                    {
                        type = new BackgroundTypePattern(fill, Math.Abs(_intensity), _intensity < 0, false);
                    }
                    else if (background.Type is BackgroundTypeWallpaper)
                    {
                        type = new BackgroundTypeWallpaper(_isBlurEnabled, false);
                    }

                    if (type == null)
                    {
                        return null;
                    }

                    var input = background.Document != null
                        ? new InputBackgroundRemote(background.Id)
                        : null;

                    return new BackgroundInfo(input, type, dark);
                }
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

        public static BackgroundColor Empty = new(0, true);

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
