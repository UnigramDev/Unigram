//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Text;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Windows.Globalization.Fonts;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using AcrylicBrush = Microsoft.UI.Xaml.Media.AcrylicBrush;

namespace Telegram.Common
{
    public partial class Theme : ResourceDictionary
    {
        [ThreadStatic]
        public static Theme Current;

        private readonly ApplicationDataContainer _isolatedStore;
        private readonly bool _isPrimary;

        public Theme()
        {
            _isPrimary = Current == null;

            try
            {
                _isolatedStore = ApplicationData.Current.LocalSettings.CreateContainer("Theme", ApplicationDataCreateDisposition.Always);
                Current ??= this;

                this.Add("MessageFontSize", GetValueOrDefault("MessageFontSize", 14d));
                this.Add("ThreadStackLayout", new StackLayout());

                UpdateEmojiSet();
                //UpdateScrolls();
            }
            catch { }

            if (_isPrimary)
            {
                Update(ApplicationTheme.Light);
                Update(ApplicationTheme.Dark);
            }
        }

        public void UpdateEmojiSet()
        {
            var xamlAutoFontFamilyValue = FontFamily.XamlAutoFontFamily.Source;
            if (xamlAutoFontFamilyValue == "Segoe UI Variable")
            {
                xamlAutoFontFamilyValue = "Segoe UI";
            }

            // TODO: including Segoe UI breaks keycap emoji,
            // not including it breaks persian numerals.
            //if (xamlAutoFontFamilyValue == "Segoe UI")
            //{
            //    xamlAutoFontFamilyValue = string.Empty;
            //}

            var xamlAutoFontFamily = new StringBuilder(xamlAutoFontFamilyValue);
            var comma = ", ";

            if (false)
            {
                foreach (var language in Formatter.Languages)
                {
                    // We copy XAML behavior, only resolve for Japanese and Korean
                    if (language == "ja" || language == "ko" || language == "ja-JP" || language == "ko-KR")
                    {
                        try
                        {
                            var recommendedFonts = new LanguageFontGroup(language);
                            var family = recommendedFonts.UITextFont.FontFamily;

                            xamlAutoFontFamily.Prepend(family, comma);
                        }
                        catch
                        {
                            // All the remote procedure calls must be wrapped in a try-catch block
                        }
                    }
                }

                xamlAutoFontFamily.Prepend("Segoe UI", comma);
            }

            switch (SettingsService.Current.Appearance.EmojiSet)
            {
                case "microsoft":
                    xamlAutoFontFamily.Prepend("ms-appx:///Assets/Emoji/microsoft.ttf#Segoe UI Emoji", comma);
                    break;
                default:
                    xamlAutoFontFamily.Prepend("ms-appx:///Assets/Emoji/apple.ttf#Segoe UI Emoji", comma);
                    break;
            }

            XamlAutoFontFamily = xamlAutoFontFamily.ToString();

            this["EmojiThemeFontFamily"] = new FontFamily(xamlAutoFontFamily.ToString());
            this["ContentControlThemeFontFamily"] = new FontFamily(xamlAutoFontFamily.ToString());

            xamlAutoFontFamily.Prepend("ms-appx:///Assets/Fonts/Telegram.ttf#Telegram", comma);

            this["EmojiThemeFontFamilyWithSymbols"] = new FontFamily(xamlAutoFontFamily.ToString());
        }

        public string XamlAutoFontFamily { get; private set; }

        private bool _legacyScrollBars;
        private bool _legacyScrollViewer;

        public void UpdateScrolls()
        {
            if (_legacyScrollBars != SettingsService.Current.Diagnostics.LegacyScrollBars)
            {
                if (SettingsService.Current.Diagnostics.LegacyScrollBars)
                {
                    var style = new Style
                    {
                        TargetType = typeof(ScrollBar)
                    };

                    // Microsoft recommends turning off layout rounding for VerticalPanningRoot and/or HorizontalPanningRoot.
                    // We do it for the whole thing because it's just easier.
                    // https://github.com/microsoft/microsoft-ui-xaml/issues/3779#issuecomment-1896403485
                    style.Setters.Add(new Setter(UIElement.UseLayoutRoundingProperty, false));

                    this.Add(typeof(ScrollBar), style);
                }
                else
                {
                    this.Remove(typeof(ScrollBar));
                }

                _legacyScrollBars = SettingsService.Current.Diagnostics.LegacyScrollBars;
            }

            if (_legacyScrollViewer != SettingsService.Current.Diagnostics.LegacyScrollViewers)
            {
                if (SettingsService.Current.Diagnostics.LegacyScrollViewers)
                {
                    var style = new Style
                    {
                        TargetType = typeof(ScrollViewer),
                    };

                    style.Setters.Add(new Setter(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto));
                    style.Setters.Add(new Setter(ScrollViewer.VerticalScrollModeProperty, ScrollMode.Enabled));

                    this.Add(typeof(ScrollViewer), style);
                }
                else
                {
                    this.Remove(typeof(ScrollViewer));
                }

                _legacyScrollViewer = SettingsService.Current.Diagnostics.LegacyScrollViewers;
            }
        }

        public static Color Accent { get; private set; } = Colors.Red;

        public ChatTheme ChatTheme => _lastTheme;

        public ChatBackground ChatBackground => _lastChatBackground;

        public void Update(ElementTheme requested)
        {
            Update(requested == ElementTheme.Light
                ? ApplicationTheme.Light
                : ApplicationTheme.Dark);
        }

        private readonly Dictionary<ElementTheme, ThemeParameters> _parameters = new();
        public ThemeParameters Parameters => _parameters[WindowContext.Current.ActualTheme];

        #region Local 

        private int? _lastAccent;
        private long? _lastBackground;

        private ChatTheme _lastTheme;
        private ChatBackground _lastChatBackground;

        public bool Update(ElementTheme elementTheme, ChatTheme theme, ChatBackground background)
        {
            var updated = false;
            var requested = elementTheme == ElementTheme.Dark ? TelegramTheme.Dark : TelegramTheme.Light;
            var nextBackground = background?.Background;

            var settings = requested == TelegramTheme.Light ? theme?.LightSettings : theme?.DarkSettings;
            if (settings != null)
            {
                if (_lastAccent != settings.AccentColor)
                {
                    _lastTheme = theme;

                    var tint = SettingsService.Current.Appearance[requested].Type;
                    if (tint == TelegramThemeType.Classic || (tint == TelegramThemeType.Custom && requested == TelegramTheme.Light))
                    {
                        tint = TelegramThemeType.Day;
                    }
                    else if (tint == TelegramThemeType.Custom)
                    {
                        tint = TelegramThemeType.Tinted;
                    }

                    var accent = settings.AccentColor.ToColor();
                    var outgoing = settings.OutgoingMessageAccentColor.ToColor();

                    var info = ThemeAccentInfo.FromAccent(tint, accent, outgoing);
                    ThemeOutgoing.Update(info.Parent, info.Values);
                    ThemeIncoming.Update(info.Parent, info.Values);
                }

                nextBackground ??= settings.Background;

                _lastAccent = settings.AccentColor;
            }
            else
            {
                if (_lastAccent != null)
                {
                    _lastTheme = null;

                    var options = SettingsService.Current.Appearance;
                    if (options[requested].Type == TelegramThemeType.Custom && System.IO.File.Exists(options[requested].Custom))
                    {
                        var info = ThemeCustomInfo.FromFile(options[requested].Custom);
                        ThemeOutgoing.Update(info.Parent, info.Values);
                        ThemeIncoming.Update(info.Parent, info.Values);
                    }
                    else if (ThemeAccentInfo.IsAccent(options[requested].Type))
                    {
                        var info = ThemeAccentInfo.FromAccent(options[requested].Type, options.Accents[options[requested].Type]);
                        ThemeOutgoing.Update(info.Parent, info.Values);
                        ThemeIncoming.Update(info.Parent, info.Values);
                    }
                    else
                    {
                        ThemeOutgoing.Update(requested);
                        ThemeIncoming.Update(requested);
                    }
                }

                _lastAccent = null;
            }

            if (nextBackground?.Id != _lastBackground)
            {
                updated = true;
            }

            _lastBackground = nextBackground?.Id;
            _lastChatBackground = background;

            return updated;
        }

        #endregion

        #region Global

        private void Update(ApplicationTheme theme)
        {
            var settings = SettingsService.Current.Appearance;
            var requested = theme == ApplicationTheme.Light
                ? TelegramTheme.Light
                : TelegramTheme.Dark;

            if (settings.ChatTheme != null)
            {
                Update(requested, settings.ChatTheme);
            }
            else if (settings[requested].Type == TelegramThemeType.Custom && System.IO.File.Exists(settings[requested].Custom))
            {
                Update(ThemeCustomInfo.FromFile(settings[requested].Custom));
            }
            else if (ThemeAccentInfo.IsAccent(settings[requested].Type))
            {
                Update(ThemeAccentInfo.FromAccent(settings[requested].Type, settings.Accents[settings[requested].Type]));
            }
            else
            {
                Update(requested);
            }

            if (ChatTheme != null)
            {
                Update(theme == ApplicationTheme.Light ? ElementTheme.Light : ElementTheme.Dark, ChatTheme, ChatBackground);
            }
        }

        private void Update(TelegramTheme requested, ChatTheme theme)
        {
            var settings = requested == TelegramTheme.Light ? theme?.LightSettings : theme?.DarkSettings;

            var tint = SettingsService.Current.Appearance[requested].Type;
            if (tint == TelegramThemeType.Classic || (tint == TelegramThemeType.Custom && requested == TelegramTheme.Light))
            {
                tint = TelegramThemeType.Day;
            }
            else if (tint == TelegramThemeType.Custom)
            {
                tint = TelegramThemeType.Tinted;
            }

            var accent = settings.AccentColor.ToColor();
            var outgoing = settings.OutgoingMessageAccentColor.ToColor();

            Update(ThemeAccentInfo.FromAccent(tint, accent, outgoing));
        }

        public void Update(string path)
        {
            Update(ThemeCustomInfo.FromFile(path));
        }

        public void Update(ThemeAccentInfo info)
        {
            Update(info.Parent, info.Values, info.Shades);
        }

        private void Update(TelegramTheme requested, IDictionary<string, Color> values = null, IDictionary<AccentShade, Color> shades = null)
        {
            try
            {
                ThemeOutgoing.Update(requested, values);
                ThemeIncoming.Update(requested, values);

                var target = GetOrCreateResources(requested, out bool create);
                var lookup = ThemeService.GetLookup(requested);

                var themeParameters = new Dictionary<string, int>
                {
                    { "CardBackgroundFillColorDefaultBrush", 0 },
                    { "CardBackgroundFillColorSecondaryBrush", 0 },
                    { "TextFillColorPrimaryBrush", 0 },
                    { "AccentButtonBackground", 0 },
                    { "AccentButtonForeground", 0 },
                    { "SystemControlDisabledChromeDisabledLowBrush", 0 },
                    { "CardStrokeColorDefaultSolidBrush", 0 },
                    { "DangerButtonBackground", 0xD13438 }
                };

                Color GetShade(AccentShade shade)
                {
                    if (shades != null && shades.TryGetValue(shade, out Color accent))
                    {
                        return accent;
                    }
                    else
                    {
                        return ThemeInfoBase.Accents[TelegramThemeType.Day][shade];
                    }
                }

                if (_isPrimary)
                {
                    Accent = GetShade(AccentShade.Default);
                }

                foreach (var item in lookup)
                {
                    if (item.Value is AccentShade or Color)
                    {
                        Color value;
                        if (item.Value is AccentShade shade)
                        {
                            value = GetShade(shade);
                        }
                        else if (values != null && values.TryGetValue(item.Key, out Color themed))
                        {
                            value = themed;
                        }
                        else if (item.Value is Color color)
                        {
                            value = color;
                        }

                        if (themeParameters.ContainsKey(item.Key))
                        {
                            themeParameters[item.Key] = value.ToValue();
                        }

                        AddOrUpdate<SolidColorBrush>(target, item.Key, create,
                            update => update.Color = value);
                    }
                    else
                    {
                        Color tintColor;
                        double tintOpacity;
                        double? tintLuminosityOpacity;
                        Color fallbackColor;
                        if (item.Value is Acrylic<Color> acrylicColor)
                        {
                            tintColor = acrylicColor.TintColor;
                            tintOpacity = acrylicColor.TintOpacity;
                            tintLuminosityOpacity = acrylicColor.TintLuminosityOpacity;
                            fallbackColor = acrylicColor.FallbackColor;
                        }
                        else if (item.Value is Acrylic<AccentShade> acrylicShade)
                        {
                            tintColor = GetShade(acrylicShade.TintColor);
                            tintOpacity = acrylicShade.TintOpacity;
                            tintLuminosityOpacity = acrylicShade.TintLuminosityOpacity;
                            fallbackColor = GetShade(acrylicShade.FallbackColor);
                        }
                        else
                        {
                            continue;
                        }

                        AddOrUpdate<AcrylicBrush>(target, item.Key, create, update =>
                        {
                            update.TintColor = tintColor;
                            update.TintOpacity = tintOpacity;
                            update.TintLuminosityOpacity = tintLuminosityOpacity;
                            update.FallbackColor = fallbackColor;
                            update.AlwaysUseFallback = !PowerSavingPolicy.AreMaterialsEnabled;
                        });
                    }
                }

                PatchTextControlElevationBorderFocusedBrush(requested, target, lookup, "TextControlElevationBorderFocusedBrush", create, GetShade);
                PatchTextControlElevationBorderFocusedBrush(requested, target, lookup, "TextControlBorderBrushFocused", create, GetShade);

                if (create)
                {
                    ThemeDictionaries.Add(requested == TelegramTheme.Light ? "Light" : "Dark", target);
                }

                _parameters[requested == TelegramTheme.Light ? ElementTheme.Light : ElementTheme.Dark] = new ThemeParameters
                {
                    BackgroundColor = themeParameters["CardBackgroundFillColorDefaultBrush"],
                    SecondaryBackgroundColor = themeParameters["CardBackgroundFillColorSecondaryBrush"],
                    BottomBarBackgroundColor = themeParameters["CardBackgroundFillColorDefaultBrush"],
                    TextColor = themeParameters["TextFillColorPrimaryBrush"],
                    ButtonColor = themeParameters["AccentButtonBackground"],
                    ButtonTextColor = themeParameters["AccentButtonForeground"],
                    HintColor = themeParameters["SystemControlDisabledChromeDisabledLowBrush"],
                    LinkColor = themeParameters["AccentButtonBackground"],
                    AccentTextColor = themeParameters["AccentButtonBackground"],
                    DestructiveTextColor = themeParameters["DangerButtonBackground"],
                    HeaderBackgroundColor = themeParameters["CardBackgroundFillColorDefaultBrush"],
                    SectionBackgroundColor = themeParameters["CardBackgroundFillColorDefaultBrush"],
                    SectionHeaderTextColor = themeParameters["TextFillColorPrimaryBrush"],
                    SubtitleTextColor = themeParameters["SystemControlDisabledChromeDisabledLowBrush"],
                    SectionSeparatorColor = themeParameters["CardStrokeColorDefaultSolidBrush"],
                };
            }
            catch (UnauthorizedAccessException)
            {
                // Some times access denied is thrown,
                // this seems to happen after the application
                // is resumed, but unfortunately I can't see
                // any fix to this. The exception is going
                // to be thrown any time - even minutes after 
                // the resume - if the theme changes.

                // The exception MIGHT be related to StaticResources
                // but I'm not able to confirm this.
            }
        }

        private void PatchTextControlElevationBorderFocusedBrush(TelegramTheme requested, ResourceDictionary target, Dictionary<string, object> lookup, string key, bool create, Func<AccentShade, Color> getShade)
        {
            // TextControlElevationBorderFocusedBrush is the only gradient that requires theming,
            // Hence we hardcode the logic to update this brush as it's not worth it to support this scenario.
            AddOrUpdate(target, key, create, (LinearGradientBrush brush) =>
            {
                if (create)
                {
                    brush.MappingMode = BrushMappingMode.Absolute;
                    brush.StartPoint = new Windows.Foundation.Point(0, 0);
                    brush.EndPoint = new Windows.Foundation.Point(0, 2);
                    brush.RelativeTransform = new ScaleTransform
                    {
                        ScaleY = -1,
                        CenterY = 0.5
                    };
                    brush.GradientStops = new GradientStopCollection
                    {
                        new GradientStop
                        {
                            Offset = 1.0
                        },
                        new GradientStop
                        {
                            Offset = 1.0
                        }
                    };
                }

                if (lookup.TryGet("ControlStrokeColorDefaultBrush", out Color stroke))
                {
                    brush.GradientStops[0].Color = getShade(requested == TelegramTheme.Light ? AccentShade.Dark1 : AccentShade.Light1);
                    brush.GradientStops[1].Color = stroke;
                }
            });
        }

        private void AddOrUpdate<T>(ResourceDictionary target, string key, bool create, Action<T> callback) where T : new()
        {
            if (create)
            {
                var value = new T();
                callback(value);
                target[key] = value;
            }
            else if (target.TryGet(key, out T update))
            {
                try
                {
                    callback(update);
                }
                catch (UnauthorizedAccessException)
                {
                    // Some times access denied is thrown,
                    // this seems to happen after the application
                    // is resumed, but unfortunately I can't see
                    // any fix to this. The exception is going
                    // to be thrown any time - even minutes after 
                    // the resume - if the theme changes.

                    // The exception MIGHT be related to StaticResources
                    // but I'm not able to confirm this.
                }
            }
        }

        private ResourceDictionary GetOrCreateResources(TelegramTheme requested, out bool create)
        {
            if (ThemeDictionaries.TryGet(requested == TelegramTheme.Light ? "Light" : "Dark", out ResourceDictionary target))
            {
                create = false;
            }
            else
            {
                create = true;
                target = new ResourceDictionary();
            }

            return target;
        }

        #endregion

        #region Settings

        private int? _messageFontSize;
        public int MessageFontSize
        {
            get => _messageFontSize ??= (int)GetValueOrDefault("MessageFontSize", 14d);
            set => AddOrUpdateValue("MessageFontSize", (double)(_messageFontSize = value));
        }

        public int CaptionFontSize => MessageFontSize - 2;

        public bool AddOrUpdateValue(string key, object value)
        {
            bool valueChanged = false;

            if (_isolatedStore.Values.ContainsKey(key))
            {
                if (_isolatedStore.Values[key] != value)
                {
                    _isolatedStore.Values[key] = value;
                    valueChanged = true;
                }
            }
            else
            {
                _isolatedStore.Values.Add(key, value);
                valueChanged = true;
            }

            if (valueChanged)
            {
                try
                {
                    if (this.ContainsKey(key))
                    {
                        this[key] = value;
                    }
                    else
                    {
                        this.Add(key, value);
                    }
                }
                catch { }
            }

            return valueChanged;
        }

        public valueType GetValueOrDefault<valueType>(string key, valueType defaultValue)
        {
            valueType value;

            if (_isolatedStore.Values.ContainsKey(key))
            {
                value = (valueType)_isolatedStore.Values[key];
            }
            else
            {
                value = defaultValue;
            }

            return value;
        }

        #endregion
    }

    public partial class ThemeOutgoing : ResourceDictionary
    {
        [ThreadStatic]
        private static Dictionary<string, (Color Color, SolidColorBrush Brush)> _light;
        public static Dictionary<string, (Color Color, SolidColorBrush Brush)> Light => _light ??= new()
        {
            { "MessageForegroundBrush", (Color.FromArgb(0xFF, 0x00, 0x00, 0x00), new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x00, 0x00))) },
            { "MessageForegroundLinkBrush", (Color.FromArgb(0xFF, 0x16, 0x8A, 0xCD), new SolidColorBrush(Color.FromArgb(0xFF, 0x16, 0x8A, 0xCD))) },
            { "MessageBackgroundBrush", (Color.FromArgb(0xFF, 0xF0, 0xFD, 0xDF), new SolidColorBrush(Color.FromArgb(0xFF, 0xF0, 0xFD, 0xDF))) },
            { "MessageElevationBrush", (Color.FromArgb(0x1D, 0x3A, 0xC3, 0x46), new SolidColorBrush(Color.FromArgb(0x1D, 0x3A, 0xC3, 0x46))) },
            { "MessageSubtleLabelBrush", (Color.FromArgb(0xFF, 0x6D, 0xC2, 0x64), new SolidColorBrush(Color.FromArgb(0xFF, 0x6D, 0xC2, 0x64))) },
            { "MessageSubtleGlyphBrush", (Color.FromArgb(0xFF, 0x5D, 0xC4, 0x52), new SolidColorBrush(Color.FromArgb(0xFF, 0x5D, 0xC4, 0x52))) },
            { "MessageSubtleForegroundBrush", (Color.FromArgb(0xFF, 0x6D, 0xC2, 0x64), new SolidColorBrush(Color.FromArgb(0xFF, 0x6D, 0xC2, 0x64))) },
            { "MessageHeaderForegroundBrush", (Color.FromArgb(0xFF, 0x3A, 0x8E, 0x26), new SolidColorBrush(Color.FromArgb(0xFF, 0x3A, 0x8E, 0x26))) },
            { "MessageHeaderBorderBrush", (Color.FromArgb(0xFF, 0x5D, 0xC4, 0x52), new SolidColorBrush(Color.FromArgb(0xFF, 0x5D, 0xC4, 0x52))) },
            { "MessageMediaForegroundBrush", (Color.FromArgb(0xFF, 0xF0, 0xFD, 0xDF), new SolidColorBrush(Color.FromArgb(0xFF, 0xF0, 0xFD, 0xDF))) },
            { "MessageMediaBackgroundBrush", (Color.FromArgb(0xFF, 0x78, 0xC6, 0x7F), new SolidColorBrush(Color.FromArgb(0xFF, 0x78, 0xC6, 0x7F))) },
            { "MessageOverlayBackgroundBrush", (Color.FromArgb(0x54, 0x00, 0x00, 0x00), new SolidColorBrush(Color.FromArgb(0x54, 0x00, 0x00, 0x00))) },
            { "MessageCallForegroundBrush", (Color.FromArgb(0xFF, 0x2A, 0xB3, 0x2A), new SolidColorBrush(Color.FromArgb(0xFF, 0x2A, 0xB3, 0x2A))) },
            { "MessageCallMissedForegroundBrush", (Color.FromArgb(0xFF, 0xDD, 0x58, 0x49), new SolidColorBrush(Color.FromArgb(0xFF, 0xDD, 0x58, 0x49))) },
            { "MessageReactionBackgroundBrush", (Color.FromArgb(0xFF, 0xD5, 0xF1, 0xC9), new SolidColorBrush(Color.FromArgb(0xFF, 0xD5, 0xF1, 0xC9))) },
            { "MessageReactionForegroundBrush", (Color.FromArgb(0xFF, 0x45, 0xA3, 0x2D), new SolidColorBrush(Color.FromArgb(0xFF, 0x45, 0xA3, 0x2D))) },
            { "MessageReactionChosenBackgroundBrush", (Color.FromArgb(0xFF, 0x5F, 0xBE, 0x67), new SolidColorBrush(Color.FromArgb(0xFF, 0x5F, 0xBE, 0x67))) },
            { "MessageReactionChosenForegroundBrush", (Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF))) },
        };

        [ThreadStatic]
        private static Dictionary<string, (Color Color, SolidColorBrush Brush)> _dark;
        public static Dictionary<string, (Color Color, SolidColorBrush Brush)> Dark => _dark ??= new()
        {
            { "MessageForegroundBrush", (Color.FromArgb(0xFF, 0xE4, 0xEC, 0xF2), new SolidColorBrush(Color.FromArgb(0xFF, 0xE4, 0xEC, 0xF2))) },
            { "MessageForegroundLinkBrush", (Color.FromArgb(0xFF, 0x71, 0xBB, 0xE7), new SolidColorBrush(Color.FromArgb(0xFF, 0x71, 0xBB, 0xE7))) },
            { "MessageBackgroundBrush", (Color.FromArgb(0xFF, 0x2B, 0x52, 0x78), new SolidColorBrush(Color.FromArgb(0xFF, 0x2B, 0x52, 0x78))) },
            { "MessageElevationBrush", (Color.FromArgb(0x1D, 0x3A, 0xC3, 0x46), new SolidColorBrush(Color.FromArgb(0x1D, 0x3A, 0xC3, 0x46))) },
            { "MessageSubtleLabelBrush", (Color.FromArgb(0xFF, 0x7D, 0xA8, 0xD3), new SolidColorBrush(Color.FromArgb(0xFF, 0x7D, 0xA8, 0xD3))) },
            { "MessageSubtleGlyphBrush", (Color.FromArgb(0xFF, 0x72, 0xBC, 0xFD), new SolidColorBrush(Color.FromArgb(0xFF, 0x72, 0xBC, 0xFD))) },
            { "MessageSubtleForegroundBrush", (Color.FromArgb(0xFF, 0x7D, 0xA8, 0xD3), new SolidColorBrush(Color.FromArgb(0xFF, 0x7D, 0xA8, 0xD3))) },
            { "MessageHeaderForegroundBrush", (Color.FromArgb(0xFF, 0x90, 0xCA, 0xFF), new SolidColorBrush(Color.FromArgb(0xFF, 0x90, 0xCA, 0xFF))) },
            { "MessageHeaderBorderBrush", (Color.FromArgb(0xFF, 0x65, 0xB9, 0xF4), new SolidColorBrush(Color.FromArgb(0xFF, 0x65, 0xB9, 0xF4))) },
            { "MessageMediaForegroundBrush", (Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF))) },
            { "MessageMediaBackgroundBrush", (Color.FromArgb(0xFF, 0x4C, 0x9C, 0xE2), new SolidColorBrush(Color.FromArgb(0xFF, 0x4C, 0x9C, 0xE2))) },
            { "MessageOverlayBackgroundBrush", (Color.FromArgb(0x54, 0x00, 0x00, 0x00), new SolidColorBrush(Color.FromArgb(0x54, 0x00, 0x00, 0x00))) },
            { "MessageCallForegroundBrush", (Color.FromArgb(0xFF, 0x49, 0xA2, 0xF0), new SolidColorBrush(Color.FromArgb(0xFF, 0x49, 0xA2, 0xF0))) },
            { "MessageCallMissedForegroundBrush", (Color.FromArgb(0xFF, 0xED, 0x50, 0x50), new SolidColorBrush(Color.FromArgb(0xFF, 0xED, 0x50, 0x50))) },
            { "MessageReactionBackgroundBrush", (Color.FromArgb(0xFF, 0x2B, 0x41, 0x53), new SolidColorBrush(Color.FromArgb(0xFF, 0x2B, 0x41, 0x53))) },
            { "MessageReactionForegroundBrush", (Color.FromArgb(0xFF, 0x7A, 0xC3, 0xF4), new SolidColorBrush(Color.FromArgb(0xFF, 0x7A, 0xC3, 0xF4))) },
            { "MessageReactionChosenBackgroundBrush", (Color.FromArgb(0xFF, 0x31, 0x8E, 0xE4), new SolidColorBrush(Color.FromArgb(0xFF, 0x31, 0x8E, 0xE4))) },
            { "MessageReactionChosenForegroundBrush", (Color.FromArgb(0xFF, 0x33, 0x39, 0x3F), new SolidColorBrush(Color.FromArgb(0xFF, 0x33, 0x39, 0x3F))) },
        };

        public static void Release()
        {
            _light = null;
            _dark = null;
        }

        public ThemeOutgoing()
        {
            var light = new ResourceDictionary();
            var dark = new ResourceDictionary();

            foreach (var item in Light)
            {
                light[item.Key] = item.Value.Brush;
            }

            foreach (var item in Dark)
            {
                dark[item.Key] = item.Value.Brush;
            }

            ThemeDictionaries["Light"] = light;
            ThemeDictionaries["Default"] = dark;
        }

        public static void Update(TelegramTheme parent, IDictionary<string, Color> values = null)
        {
            if (values == null)
            {
                Update(parent);
                return;
            }

            var target = parent == TelegramTheme.Dark ? Dark : Light;

            foreach (var value in target)
            {
                var key = value.Key[..^5];
                if (values.TryGetValue($"{key}Outgoing", out Color color))
                {
                    value.Value.Brush.Color = color;
                }
                else
                {
                    value.Value.Brush.Color = value.Value.Color;
                }
            }
        }

        public static void Update(TelegramTheme parent)
        {
            if (parent == TelegramTheme.Light)
            {
                foreach (var value in Light)
                {
                    value.Value.Brush.Color = value.Value.Color;
                }
            }
            else
            {
                foreach (var value in Dark)
                {
                    value.Value.Brush.Color = value.Value.Color;
                }
            }
        }
    }

    public partial class ThemeIncoming : ResourceDictionary
    {
        [ThreadStatic]
        private static Dictionary<string, (Color Color, SolidColorBrush Brush)> _light;
        public static Dictionary<string, (Color Color, SolidColorBrush Brush)> Light => _light ??= new()
        {
            { "MessageForegroundBrush", (Color.FromArgb(0xFF, 0x00, 0x00, 0x00), new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x00, 0x00))) },
            { "MessageForegroundLinkBrush", (Color.FromArgb(0xFF, 0x16, 0x8A, 0xCD), new SolidColorBrush(Color.FromArgb(0xFF, 0x16, 0x8A, 0xCD))) },
            { "MessageBackgroundBrush", (Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF))) },
            { "MessageElevationBrush", (Color.FromArgb(0x29, 0x74, 0x8E, 0xA2), new SolidColorBrush(Color.FromArgb(0x29, 0x74, 0x8E, 0xA2))) },
            { "MessageSubtleLabelBrush", (Color.FromArgb(0xFF, 0xA1, 0xAD, 0xB6), new SolidColorBrush(Color.FromArgb(0xFF, 0xA1, 0xAD, 0xB6))) },
            { "MessageSubtleGlyphBrush", (Color.FromArgb(0xFF, 0xA1, 0xAD, 0xB6), new SolidColorBrush(Color.FromArgb(0xFF, 0xA1, 0xAD, 0xB6))) },
            { "MessageSubtleForegroundBrush", (Color.FromArgb(0xFF, 0xA1, 0xAD, 0xB6), new SolidColorBrush(Color.FromArgb(0xFF, 0xA1, 0xAD, 0xB6))) },
            { "MessageHeaderForegroundBrush", (Color.FromArgb(0xFF, 0x15, 0x8D, 0xCD), new SolidColorBrush(Color.FromArgb(0xFF, 0x15, 0x8D, 0xCD))) },
            { "MessageHeaderBorderBrush", (Color.FromArgb(0xFF, 0x37, 0xA4, 0xDE), new SolidColorBrush(Color.FromArgb(0xFF, 0x37, 0xA4, 0xDE))) },
            { "MessageMediaForegroundBrush", (Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF))) },
            { "MessageMediaBackgroundBrush", (Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3), new SolidColorBrush(Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3))) },
            { "MessageOverlayBackgroundBrush", (Color.FromArgb(0x54, 0x00, 0x00, 0x00), new SolidColorBrush(Color.FromArgb(0x54, 0x00, 0x00, 0x00))) },
            { "MessageCallForegroundBrush", (Color.FromArgb(0xFF, 0x2A, 0xB3, 0x2A), new SolidColorBrush(Color.FromArgb(0xFF, 0x2A, 0xB3, 0x2A))) },
            { "MessageCallMissedForegroundBrush", (Color.FromArgb(0xFF, 0xDD, 0x58, 0x49), new SolidColorBrush(Color.FromArgb(0xFF, 0xDD, 0x58, 0x49))) },
            { "MessageReactionBackgroundBrush", (Color.FromArgb(0xFF, 0xE8, 0xF5, 0xFC), new SolidColorBrush(Color.FromArgb(0xFF, 0xE8, 0xF5, 0xFC))) },
            { "MessageReactionForegroundBrush", (Color.FromArgb(0xFF, 0x16, 0x8D, 0xCD), new SolidColorBrush(Color.FromArgb(0xFF, 0x16, 0x8D, 0xCD))) },
            { "MessageReactionChosenBackgroundBrush", (Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3), new SolidColorBrush(Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3))) },
            { "MessageReactionChosenForegroundBrush", (Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF))) },
        };

        [ThreadStatic]
        private static Dictionary<string, (Color Color, SolidColorBrush Brush)> _dark;
        public static Dictionary<string, (Color Color, SolidColorBrush Brush)> Dark => _dark ??= new()
        {
            { "MessageForegroundBrush", (Color.FromArgb(0xFF, 0xF5, 0xF5, 0xF5), new SolidColorBrush(Color.FromArgb(0xFF, 0xF5, 0xF5, 0xF5))) },
            { "MessageForegroundLinkBrush", (Color.FromArgb(0xFF, 0x71, 0xBB, 0xE7), new SolidColorBrush(Color.FromArgb(0xFF, 0x71, 0xBB, 0xE7))) },
            { "MessageBackgroundBrush", (Color.FromArgb(0xFF, 0x18, 0x25, 0x33), new SolidColorBrush(Color.FromArgb(0xFF, 0x18, 0x25, 0x33))) },
            { "MessageElevationBrush", (Color.FromArgb(0x29, 0x74, 0x8E, 0xA2), new SolidColorBrush(Color.FromArgb(0x29, 0x74, 0x8E, 0xA2))) },
            { "MessageSubtleLabelBrush", (Color.FromArgb(0xFF, 0x6D, 0x7F, 0x8F), new SolidColorBrush(Color.FromArgb(0xFF, 0x6D, 0x7F, 0x8F))) },
            { "MessageSubtleGlyphBrush", (Color.FromArgb(0xFF, 0x6D, 0x7F, 0x8F), new SolidColorBrush(Color.FromArgb(0xFF, 0x6D, 0x7F, 0x8F))) },
            { "MessageSubtleForegroundBrush", (Color.FromArgb(0xFF, 0x6D, 0x7F, 0x8F), new SolidColorBrush(Color.FromArgb(0xFF, 0x6D, 0x7F, 0x8F))) },
            { "MessageHeaderForegroundBrush", (Color.FromArgb(0xFF, 0x71, 0xBA, 0xFA), new SolidColorBrush(Color.FromArgb(0xFF, 0x71, 0xBA, 0xFA))) },
            { "MessageHeaderBorderBrush", (Color.FromArgb(0xFF, 0x42, 0x9B, 0xDB), new SolidColorBrush(Color.FromArgb(0xFF, 0x42, 0x9B, 0xDB))) },
            { "MessageMediaForegroundBrush", (Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF))) },
            { "MessageMediaBackgroundBrush", (Color.FromArgb(0xFF, 0x3F, 0x96, 0xD0), new SolidColorBrush(Color.FromArgb(0xFF, 0x3F, 0x96, 0xD0))) },
            { "MessageOverlayBackgroundBrush", (Color.FromArgb(0x54, 0x00, 0x00, 0x00), new SolidColorBrush(Color.FromArgb(0x54, 0x00, 0x00, 0x00))) },
            { "MessageCallForegroundBrush", (Color.FromArgb(0xFF, 0x49, 0xA2, 0xF0), new SolidColorBrush(Color.FromArgb(0xFF, 0x49, 0xA2, 0xF0))) },
            { "MessageCallMissedForegroundBrush", (Color.FromArgb(0xFF, 0xED, 0x50, 0x50), new SolidColorBrush(Color.FromArgb(0xFF, 0xED, 0x50, 0x50))) },
            { "MessageReactionBackgroundBrush", (Color.FromArgb(0xFF, 0x3A, 0x47, 0x54), new SolidColorBrush(Color.FromArgb(0xFF, 0x3A, 0x47, 0x54))) },
            { "MessageReactionForegroundBrush", (Color.FromArgb(0xFF, 0x67, 0xBB, 0xF3), new SolidColorBrush(Color.FromArgb(0xFF, 0x67, 0xBB, 0xF3))) },
            { "MessageReactionChosenBackgroundBrush", (Color.FromArgb(0xFF, 0x6E, 0xB2, 0xEE), new SolidColorBrush(Color.FromArgb(0xFF, 0x6E, 0xB2, 0xEE))) },
            { "MessageReactionChosenForegroundBrush", (Color.FromArgb(0xFF, 0x33, 0x39, 0x3F), new SolidColorBrush(Color.FromArgb(0xFF, 0x33, 0x39, 0x3F))) },
        };

        public static void Release()
        {
            _light = null;
            _dark = null;
        }

        public ThemeIncoming()
        {
            var light = new ResourceDictionary();
            var dark = new ResourceDictionary();

            foreach (var item in Light)
            {
                light[item.Key] = item.Value.Brush;
            }

            foreach (var item in Dark)
            {
                dark[item.Key] = item.Value.Brush;
            }

            ThemeDictionaries["Light"] = light;
            ThemeDictionaries["Default"] = dark;
        }

        public static void Update(TelegramTheme parent, IDictionary<string, Color> values = null)
        {
            if (values == null)
            {
                Update(parent);
                return;
            }

            var target = parent == TelegramTheme.Dark ? Dark : Light;

            foreach (var value in target)
            {
                var key = value.Key[..^5];
                if (values.TryGetValue($"{key}Incoming", out Color color))
                {
                    value.Value.Brush.Color = color;
                }
                else
                {
                    value.Value.Brush.Color = value.Value.Color;
                }
            }
        }

        public static void Update(TelegramTheme parent)
        {
            if (parent == TelegramTheme.Light)
            {
                foreach (var value in Light)
                {
                    value.Value.Brush.Color = value.Value.Color;
                }
            }
            else
            {
                foreach (var value in Dark)
                {
                    value.Value.Brush.Color = value.Value.Color;
                }
            }
        }
    }
}
