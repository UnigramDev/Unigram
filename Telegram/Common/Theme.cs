//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using AcrylicBrush = Microsoft.UI.Xaml.Media.AcrylicBrush;

namespace Telegram.Common
{
    public partial class Theme : ResourceDictionary
    {
        [ThreadStatic]
        public static Theme Current;

        private readonly bool _isPrimary;

        public Theme()
        {
            _isPrimary = Current == null;

            // Publish before anything that can fail. Current is the only handle the app has on
            // the theme of this view, and it is dereferenced unguarded all over the message
            // tree; leaving it null because a setting could not be read turns a lost preference
            // into a crash. ApplicationData is not always reachable this early - share target
            // activation is driven before the view is initialized.
            Current ??= this;

            try
            {
                this.Add("ThreadStackLayout", new StackLayout());

                UpdateEmojiSet();
                UpdateScrolls();
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }

            if (_isPrimary)
            {
                Update(ApplicationTheme.Light);
                Update(ApplicationTheme.Dark);
            }
        }

        public void UpdateEmojiSet()
        {
            var xamlAutoFontFamilyValue = AppSettings.Appearance.FontFamily;
            var xamlAutoFontFamilyDefault = false;

            var comma = ", ";

            if (string.IsNullOrEmpty(xamlAutoFontFamilyValue))
            {
                xamlAutoFontFamilyValue = FontFamily.XamlAutoFontFamily.Source;
                xamlAutoFontFamilyDefault = true;
            }

            if (xamlAutoFontFamilyValue == "Segoe UI Variable")
            {
                xamlAutoFontFamilyValue = "Segoe UI";
            }

            var emojiFontFamily = AppSettings.Appearance.EmojiSet switch
            {
                "microsoft" => "ms-appx:///Assets/Emoji/microsoft.ttf#Segoe UI Emoji",
                _ => "ms-appx:///Assets/Emoji/apple.ttf#Segoe UI Emoji",
            };

            // When using custom fonts we prioritize the user choice over emojis.
            // This will break all keycaps emojis, but preserves 

            if (xamlAutoFontFamilyDefault)
            {
                XamlAutoFontFamily = emojiFontFamily;
            }
            else
            {
                XamlAutoFontFamily = xamlAutoFontFamilyValue + comma + emojiFontFamily;
            }

            // Text input only (TextBox, RichEditBox, ChatTextBox), and there the emoji font can't
            // come first: the editor resolves the font once per run and it breaks runs at every
            // space, so resolving each one against a packaged font file costs about a millisecond
            // per word - seconds to paste a long text. Leading with the text font costs the emojis
            // whose base character the text font already covers (keycaps, the copyright and
            // trademark signs and so on) rendering as plain glyphs while composing. What gets
            // sent is unaffected, and so is the bubble that renders it.
            this["EmojiTextThemeFontFamily"] = new FontFamily(xamlAutoFontFamilyValue + comma + emojiFontFamily);

            this["ContentControlThemeFontFamily"] = new FontFamily(XamlAutoFontFamily);
            this["EmojiThemeFontFamily"] = new FontFamily(XamlAutoFontFamily);
            this["EmojiThemeFontFamilyWithSymbols"] = new FontFamily(XamlAutoFontFamily + comma + "ms-appx:///Assets/Fonts/Telegram.ttf#Telegram");
            this["EmojiThemeFontFamilyWithRounded"] = new FontFamily(XamlAutoFontFamily + comma + "ms-appx:///Assets/Fonts/Nunito.ttf#Nunito Bold" + comma + "ms-appx:///Assets/Fonts/Telegram.ttf#Telegram");
            this["EmojiThemeFontFamilyWithSerif"] = new FontFamily(emojiFontFamily + comma + "Times New Roman");

            // Code spans and blocks. The text fallback comes last so a character the monospace
            // faces don't cover - an emoji inside a code span - still renders.
            _monospaceFontFamily = new FontFamily("Cascadia Mono, Consolas" + comma + XamlAutoFontFamily);
        }

        // The font the user picked, which is a plain string and so genuinely global. The
        // FontFamily built from it is a DependencyObject and cannot cross threads, so that one
        // is cached per thread - the same reason DispatcherContext is, and the one kind of UI
        // ThreadStatic that is not a mistake.
        public static string XamlAutoFontFamily { get; private set; }

        [ThreadStatic]
        private static FontFamily _monospaceFontFamily;
        public static FontFamily MonospaceFontFamily => _monospaceFontFamily;

        private bool _legacyScrollBars;
        private ResourceDictionary _scrollBars;

        public void UpdateScrolls()
        {
            if (_legacyScrollBars != AppSettings.Diagnostics.LegacyScrollBars || _scrollBars == null)
            {
                if (_scrollBars != null)
                {
                    MergedDictionaries.Remove(_scrollBars);
                }

                _scrollBars = new ResourceDictionary
                {
                    Source = new Uri("ms-appx:///Themes/ScrollBar_themeresources" + (AppSettings.Diagnostics.LegacyScrollBars ? "_v1" : string.Empty) + ".xaml")
                };

                MergedDictionaries.Add(_scrollBars);

                _legacyScrollBars = AppSettings.Diagnostics.LegacyScrollBars;
            }
        }

        private static ThemeAccent _accentDark;
        private static ThemeAccent _accentLight;

        public static ThemeAccent AccentDark => _accentDark ?? new();
        public static ThemeAccent AccentLight => _accentLight ?? new();

        public class ThemeAccent
        {
            public Color Dark1 { get; set; } = Colors.Red;
            public Color Dark2 { get; set; } = Colors.Red;
            public Color Dark3 { get; set; } = Colors.Red;
            public Color Default { get; set; } = Colors.Red;
            public Color Light3 { get; set; } = Colors.Red;
            public Color Light2 { get; set; } = Colors.Red;
            public Color Light1 { get; set; } = Colors.Red;
        }

        public void Update(ElementTheme requested)
        {
            Update(requested == ElementTheme.Light
                ? ApplicationTheme.Light
                : ApplicationTheme.Dark);
        }

        private readonly Dictionary<ElementTheme, ThemeParameters> _parameters = new();

        /// <summary>
        /// The bot API's view of the theme. It differs between light and dark, so the window
        /// asking has to say which of the two it is currently showing.
        /// </summary>
        public ThemeParameters GetParameters(ElementTheme actualTheme)
        {
            return _parameters[actualTheme];
        }

        /// <summary>
        /// A dictionary of the outgoing bubble overrides, for an outgoing MessageBubble to assign
        /// to its <see cref="FrameworkElement.Resources"/>.
        /// </summary>
        /// <remarks>
        /// One per bubble, and it cannot be otherwise: FrameworkElement's Resources setter calls
        /// SetResourceOwner on the dictionary, which holds a single owner and propagates it into
        /// the merged and theme dictionaries beneath it. A shared instance throws on the second
        /// element, and merging one would leave the owner flapping between bubbles.
        ///
        /// Only the wrapper is per bubble. The brushes inside are the window's, shared and
        /// recoloured in place, which is what makes a theme change repaint every bubble without
        /// touching any of them.
        /// </remarks>
        public MessageBrushes Outgoing { get; } = new("Outgoing", ThemeOutgoing.DefaultLight, ThemeOutgoing.DefaultDark);

        public MessageBrushes Incoming { get; } = new("Incoming", ThemeIncoming.DefaultLight, ThemeIncoming.DefaultDark);

        /// <summary>
        /// The colours for one base theme, resolved from the layers that apply to it: a chat
        /// theme when one is active, otherwise the user's appearance - a custom theme file, an
        /// accent, or neither. Null is the plain theme, which carries no values of its own.
        /// </summary>
        /// <summary>
        /// The settings of the app-wide chat theme, the one SettingsAppearanceViewModel sets.
        /// Null when there is none, which leaves the appearance to decide.
        /// </summary>
        internal static ThemeSettings GetAppChatSettings(TelegramTheme requested)
        {
            var chatTheme = AppSettings.Appearance.ChatTheme;
            if (chatTheme == null)
            {
                return null;
            }

            return requested == TelegramTheme.Light ? chatTheme.LightSettings : chatTheme.DarkSettings;
        }

        internal static ThemeAccentInfo Resolve(TelegramTheme requested, ThemeSettings chat)
        {
            if (chat != null)
            {
                // A chat theme supplies the accent but still tints to the appearance the user
                // picked, so the app layer decides the shade even here.
                var tint = AppSettings.Appearance[requested].Type;
                if (tint == TelegramThemeType.Classic || (tint == TelegramThemeType.Custom && requested == TelegramTheme.Light))
                {
                    tint = TelegramThemeType.Day;
                }
                else if (tint == TelegramThemeType.Custom)
                {
                    tint = TelegramThemeType.Tinted;
                }

                //var outgoing = chat.OutgoingMessageFill switch
                //{
                //    //BackgroundFillSolid solid => solid.Color.ToColor(),
                //    BackgroundFillGradient gradient => gradient.TopColor.ToColor(),
                //    BackgroundFillFreeformGradient freeform => freeform.Colors[0].ToColor(),
                //    _ => chat.OutgoingMessageAccentColor.ToColor()
                //};

                return ThemeAccentInfo.FromAccent(tint, chat.AccentColor.ToColor(), chat.OutgoingMessageAccentColor.ToColor());
            }

            var options = AppSettings.Appearance;
            if (options[requested].Type == TelegramThemeType.Custom && System.IO.File.Exists(options[requested].Custom))
            {
                return ThemeCustomInfo.FromFile(options[requested].Custom);
            }
            else if (ThemeAccentInfo.IsAccent(options[requested].Type))
            {
                return ThemeAccentInfo.FromAccent(options[requested].Type, options.Accents[options[requested].Type]);
            }

            return null;
        }

        #region Global

        private void Update(ApplicationTheme theme)
        {
            var settings = AppSettings.Appearance;
            var requested = theme == ApplicationTheme.Light
                ? TelegramTheme.Light
                : TelegramTheme.Dark;

            var info = Resolve(requested, GetAppChatSettings(requested));
            if (info != null)
            {
                Update(info.Parent, info.Values, info.Shades);
            }
            else
            {
                Update(requested);
            }
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
                Outgoing.Update(requested, values);
                Incoming.Update(requested, values);

                var target = GetOrCreateResources(requested, out bool create);
                var lookup = ThemeService.GetLookup(requested);

                var themeParameters = new Dictionary<string, int>
                {
                    { "ApplicationPageBackgroundThemeBrush", 0 },
                    { "ContentDialogBackground", 0 },
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
                    var accent = new ThemeAccent
                    {
                        Dark1 = GetShade(AccentShade.Dark1),
                        Dark2 = GetShade(AccentShade.Dark2),
                        Dark3 = GetShade(AccentShade.Dark3),
                        Default = GetShade(AccentShade.Default),
                        Light3 = GetShade(AccentShade.Light3),
                        Light2 = GetShade(AccentShade.Light2),
                        Light1 = GetShade(AccentShade.Light1),
                    };

                    if (requested == TelegramTheme.Dark)
                    {
                        _accentDark = accent;
                    }
                    else
                    {
                        _accentLight = accent;
                    }
                }

                foreach (var item in lookup)
                {
                    var kind = item.Value.Kind;

                    if (kind is ThemeValueKind.Color or ThemeValueKind.Shade)
                    {
                        Color value;
                        if (kind == ThemeValueKind.Shade)
                        {
                            // A shade is the accent, so a theme's own value never overrides it.
                            value = GetShade(item.Value.Shade);
                        }
                        else if (values != null && values.TryGetValue(item.Key, out Color themed))
                        {
                            value = themed;
                        }
                        else
                        {
                            value = item.Value.Color;
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
                        if (kind == ThemeValueKind.AcrylicColor)
                        {
                            var acrylicColor = item.Value.AcrylicColor;

                            tintColor = acrylicColor.TintColor;
                            tintOpacity = acrylicColor.TintOpacity;
                            tintLuminosityOpacity = acrylicColor.TintLuminosityOpacity;
                            fallbackColor = acrylicColor.FallbackColor;
                        }
                        else if (kind == ThemeValueKind.AcrylicShade)
                        {
                            var acrylicShade = item.Value.AcrylicShade;

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

                // The incoming message brushes live here rather than in a dictionary of their
                // own: they are the app-wide default, resolved by every consumer outside a
                // bubble, so App.xaml merges Theme alone. References only - the brushes are
                // shared and Incoming.Update above has already recoloured them in place.
                // Written last so they win over anything the lookup put under the same key,
                // which is the precedence App.xaml gave them by merging ThemeIncoming second.
                foreach (var item in requested == TelegramTheme.Light ? Incoming.Light : Incoming.Dark)
                {
                    target[item.Key] = item.Value;
                }

                if (create)
                {
                    ThemeDictionaries.Add(requested == TelegramTheme.Light ? "Light" : "Dark", target);
                }

                _parameters[requested == TelegramTheme.Light ? ElementTheme.Light : ElementTheme.Dark] = new ThemeParameters
                {
                    BackgroundColor = themeParameters["ApplicationPageBackgroundThemeBrush"],
                    SecondaryBackgroundColor = themeParameters["ContentDialogBackground"],
                    BottomBarBackgroundColor = themeParameters["ApplicationPageBackgroundThemeBrush"],
                    TextColor = themeParameters["TextFillColorPrimaryBrush"],
                    ButtonColor = themeParameters["AccentButtonBackground"],
                    ButtonTextColor = themeParameters["AccentButtonForeground"],
                    HintColor = themeParameters["SystemControlDisabledChromeDisabledLowBrush"],
                    LinkColor = themeParameters["AccentButtonBackground"],
                    AccentTextColor = themeParameters["AccentButtonBackground"],
                    DestructiveTextColor = themeParameters["DangerButtonBackground"],
                    HeaderBackgroundColor = themeParameters["ApplicationPageBackgroundThemeBrush"],
                    SectionBackgroundColor = themeParameters["ApplicationPageBackgroundThemeBrush"],
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

        private void PatchTextControlElevationBorderFocusedBrush(TelegramTheme requested, ResourceDictionary target, ThemeLookup lookup, string key, bool create, Func<AccentShade, Color> getShade)
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

                if (lookup.TryGetColor("ControlStrokeColorDefaultBrush", out Color stroke))
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
                catch (Exception ex)
                {
                    // Some other errors seem to be randomly thrown
                    Logger.Error(ex);
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
    }

    /// <summary>
    /// The message bubble brushes for one direction, owned by the window's <see cref="Theme"/>.
    /// </summary>
    /// <remarks>
    /// Outgoing and incoming differ only in which suffix they read out of a theme's values, so
    /// they are one type with two instances rather than two near-identical classes.
    ///
    /// The brushes are shared by every bubble in the window and recoloured in place, which is
    /// what makes a theme change repaint all of them without walking the tree.
    /// </remarks>
    public sealed partial class MessageBrushes
    {
        private readonly string _suffix;

        private readonly Dictionary<string, Color> _defaultLight;
        private readonly Dictionary<string, Color> _defaultDark;

        private CompositionColorBrush _lightBackground;
        private CompositionColorBrush _darkBackground;

        public Dictionary<string, SolidColorBrush> Light { get; }

        public Dictionary<string, SolidColorBrush> Dark { get; }

        public MessageBrushes(string suffix, Dictionary<string, Color> light, Dictionary<string, Color> dark)
        {
            _suffix = suffix;
            _defaultLight = light;
            _defaultDark = dark;

            Light = Create(light);
            Dark = Create(dark);
        }

        private static Dictionary<string, SolidColorBrush> Create(Dictionary<string, Color> defaults)
        {
            var result = new Dictionary<string, SolidColorBrush>(defaults.Count);

            foreach (var item in defaults)
            {
                result[item.Key] = new SolidColorBrush(item.Value);
            }

            return result;
        }

        /// <summary>
        /// A dictionary of these brushes for an element to assign to its Resources. One per
        /// element and it cannot be otherwise - Resources takes a single owner - so only the
        /// wrapper is per bubble, never the brushes. Dark is stored under Default, which is
        /// what XAML falls back to.
        /// </summary>
        public ResourceDictionary CreateDictionary()
        {
            var dictionary = new ResourceDictionary();

            var light = new ResourceDictionary();
            var dark = new ResourceDictionary();

            foreach (var item in Light)
            {
                light[item.Key] = item.Value;
            }

            foreach (var item in Dark)
            {
                dark[item.Key] = item.Value;
            }

            dictionary.ThemeDictionaries["Light"] = light;
            dictionary.ThemeDictionaries["Default"] = dark;

            return dictionary;
        }

        // Composition mirror of MessageBackgroundBrush, for brushes that paint the bubble fill
        // through the compositor. One instance per theme, so that a colour change stays the single
        // assignment it already is for the SolidColorBrush.
        public CompositionColorBrush Background(TelegramTheme parent)
        {
            if (parent == TelegramTheme.Light)
            {
                return _lightBackground ??= BootStrapper.Current.Compositor.CreateColorBrush(Light["MessageBackgroundBrush"].Color);
            }

            return _darkBackground ??= BootStrapper.Current.Compositor.CreateColorBrush(Dark["MessageBackgroundBrush"].Color);
        }

        private void UpdateBackground(TelegramTheme parent)
        {
            if (parent == TelegramTheme.Light)
            {
                if (_lightBackground != null)
                {
                    _lightBackground.Color = Light["MessageBackgroundBrush"].Color;
                }
            }
            else if (_darkBackground != null)
            {
                _darkBackground.Color = Dark["MessageBackgroundBrush"].Color;
            }
        }

        public void Update(TelegramTheme parent, IDictionary<string, Color> values = null)
        {
            var brushes = parent == TelegramTheme.Dark ? Dark : Light;
            var defaults = parent == TelegramTheme.Dark ? _defaultDark : _defaultLight;

            foreach (var brush in brushes)
            {
                // The tables are keyed on the neutral name; a theme spells out the direction.
                var key = brush.Key[..^5] + _suffix;

                brush.Value.Color = values != null && values.TryGetValue(key, out Color color)
                    ? color
                    : defaults[brush.Key];
            }

            UpdateBackground(parent);
        }
    }


    public static partial class ThemeOutgoing
    {
        // Defaults only: plain colours, shared by every window and every thread, and the
        // fallback Outgoing brushes are reset to when a theme carries no override.
        public static readonly Dictionary<string, Color> DefaultLight = new()
        {
            { "MessageForegroundBrush", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) },
            { "MessageForegroundLinkBrush", Color.FromArgb(0xFF, 0x16, 0x8A, 0xCD) },
            { "MessageBackgroundBrush", Color.FromArgb(0xFF, 0xF0, 0xFD, 0xDF) },
            { "MessageElevationBrush", Color.FromArgb(0x1D, 0x3A, 0xC3, 0x46) },
            { "MessageSubtleLabelBrush", Color.FromArgb(0xFF, 0x6D, 0xC2, 0x64) },
            { "MessageSubtleGlyphBrush", Color.FromArgb(0xFF, 0x5D, 0xC4, 0x52) },
            { "MessageSubtleForegroundBrush", Color.FromArgb(0xFF, 0x6D, 0xC2, 0x64) },
            { "MessageHeaderForegroundBrush", Color.FromArgb(0xFF, 0x3A, 0x8E, 0x26) },
            { "MessageHeaderBorderBrush", Color.FromArgb(0xFF, 0x5D, 0xC4, 0x52) },
            { "MessageHeaderBackgroundBrush", Color.FromArgb(0x20, 0x5D, 0xC4, 0x52) },
            { "MessageMediaForegroundBrush", Color.FromArgb(0xFF, 0xF0, 0xFD, 0xDF) },
            { "MessageMediaBackgroundBrush", Color.FromArgb(0xFF, 0x78, 0xC6, 0x7F) },
            { "MessageOverlayBackgroundBrush", Color.FromArgb(0x54, 0x00, 0x00, 0x00) },
            { "MessageCallForegroundBrush", Color.FromArgb(0xFF, 0x2A, 0xB3, 0x2A) },
            { "MessageCallMissedForegroundBrush", Color.FromArgb(0xFF, 0xDD, 0x58, 0x49) },
            { "MessageReactionBackgroundBrush", Color.FromArgb(0xFF, 0xD5, 0xF1, 0xC9) },
            { "MessageReactionForegroundBrush", Color.FromArgb(0xFF, 0x45, 0xA3, 0x2D) },
            { "MessageReactionChosenBackgroundBrush", Color.FromArgb(0xFF, 0x5F, 0xBE, 0x67) },
            { "MessageReactionChosenForegroundBrush", Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF) },
        };

        public static readonly Dictionary<string, Color> DefaultDark = new()
        {
            { "MessageForegroundBrush", Color.FromArgb(0xFF, 0xE4, 0xEC, 0xF2) },
            { "MessageForegroundLinkBrush", Color.FromArgb(0xFF, 0x71, 0xBB, 0xE7) },
            { "MessageBackgroundBrush", Color.FromArgb(0xFF, 0x2B, 0x52, 0x78) },
            { "MessageElevationBrush", Color.FromArgb(0x1D, 0x3A, 0xC3, 0x46) },
            { "MessageSubtleLabelBrush", Color.FromArgb(0xFF, 0x7D, 0xA8, 0xD3) },
            { "MessageSubtleGlyphBrush", Color.FromArgb(0xFF, 0x72, 0xBC, 0xFD) },
            { "MessageSubtleForegroundBrush", Color.FromArgb(0xFF, 0x7D, 0xA8, 0xD3) },
            { "MessageHeaderForegroundBrush", Color.FromArgb(0xFF, 0x90, 0xCA, 0xFF) },
            { "MessageHeaderBorderBrush", Color.FromArgb(0xFF, 0x65, 0xB9, 0xF4) },
            { "MessageHeaderBackgroundBrush", Color.FromArgb(0x20, 0x65, 0xB9, 0xF4) },
            { "MessageMediaForegroundBrush", Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF) },
            { "MessageMediaBackgroundBrush", Color.FromArgb(0xFF, 0x4C, 0x9C, 0xE2) },
            { "MessageOverlayBackgroundBrush", Color.FromArgb(0x54, 0x00, 0x00, 0x00) },
            { "MessageCallForegroundBrush", Color.FromArgb(0xFF, 0x49, 0xA2, 0xF0) },
            { "MessageCallMissedForegroundBrush", Color.FromArgb(0xFF, 0xED, 0x50, 0x50) },
            { "MessageReactionBackgroundBrush", Color.FromArgb(0xFF, 0x2B, 0x41, 0x53) },
            { "MessageReactionForegroundBrush", Color.FromArgb(0xFF, 0x7A, 0xC3, 0xF4) },
            { "MessageReactionChosenBackgroundBrush", Color.FromArgb(0xFF, 0x31, 0x8E, 0xE4) },
            { "MessageReactionChosenForegroundBrush", Color.FromArgb(0xFF, 0x33, 0x39, 0x3F) },
        };

    }

    public static partial class ThemeIncoming
    {
        // Defaults only: plain colours, shared by every window and every thread, and the
        // fallback Incoming brushes are reset to when a theme carries no override.
        public static readonly Dictionary<string, Color> DefaultLight = new()
        {
            { "MessageForegroundBrush", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) },
            { "MessageForegroundLinkBrush", Color.FromArgb(0xFF, 0x16, 0x8A, 0xCD) },
            { "MessageBackgroundBrush", Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF) },
            { "MessageElevationBrush", Color.FromArgb(0x29, 0x74, 0x8E, 0xA2) },
            { "MessageSubtleLabelBrush", Color.FromArgb(0xFF, 0xA1, 0xAD, 0xB6) },
            { "MessageSubtleGlyphBrush", Color.FromArgb(0xFF, 0xA1, 0xAD, 0xB6) },
            { "MessageSubtleForegroundBrush", Color.FromArgb(0xFF, 0xA1, 0xAD, 0xB6) },
            { "MessageHeaderForegroundBrush", Color.FromArgb(0xFF, 0x15, 0x8D, 0xCD) },
            { "MessageHeaderBorderBrush", Color.FromArgb(0xFF, 0x37, 0xA4, 0xDE) },
            { "MessageHeaderBackgroundBrush", Color.FromArgb(0x20, 0x37, 0xA4, 0xDE) },
            { "MessageMediaForegroundBrush", Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF) },
            { "MessageMediaBackgroundBrush", Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3) },
            { "MessageOverlayBackgroundBrush", Color.FromArgb(0x54, 0x00, 0x00, 0x00) },
            { "MessageCallForegroundBrush", Color.FromArgb(0xFF, 0x2A, 0xB3, 0x2A) },
            { "MessageCallMissedForegroundBrush", Color.FromArgb(0xFF, 0xDD, 0x58, 0x49) },
            { "MessageReactionBackgroundBrush", Color.FromArgb(0xFF, 0xE8, 0xF5, 0xFC) },
            { "MessageReactionForegroundBrush", Color.FromArgb(0xFF, 0x16, 0x8D, 0xCD) },
            { "MessageReactionChosenBackgroundBrush", Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3) },
            { "MessageReactionChosenForegroundBrush", Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF) },
        };

        public static readonly Dictionary<string, Color> DefaultDark = new()
        {
            { "MessageForegroundBrush", Color.FromArgb(0xFF, 0xF5, 0xF5, 0xF5) },
            { "MessageForegroundLinkBrush", Color.FromArgb(0xFF, 0x71, 0xBB, 0xE7) },
            { "MessageBackgroundBrush", Color.FromArgb(0xFF, 0x18, 0x25, 0x33) },
            { "MessageElevationBrush", Color.FromArgb(0x29, 0x74, 0x8E, 0xA2) },
            { "MessageSubtleLabelBrush", Color.FromArgb(0xFF, 0x6D, 0x7F, 0x8F) },
            { "MessageSubtleGlyphBrush", Color.FromArgb(0xFF, 0x6D, 0x7F, 0x8F) },
            { "MessageSubtleForegroundBrush", Color.FromArgb(0xFF, 0x6D, 0x7F, 0x8F) },
            { "MessageHeaderForegroundBrush", Color.FromArgb(0xFF, 0x71, 0xBA, 0xFA) },
            { "MessageHeaderBorderBrush", Color.FromArgb(0xFF, 0x42, 0x9B, 0xDB) },
            { "MessageHeaderBackgroundBrush", Color.FromArgb(0x20, 0x42, 0x9B, 0xDB) },
            { "MessageMediaForegroundBrush", Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF) },
            { "MessageMediaBackgroundBrush", Color.FromArgb(0xFF, 0x3F, 0x96, 0xD0) },
            { "MessageOverlayBackgroundBrush", Color.FromArgb(0x54, 0x00, 0x00, 0x00) },
            { "MessageCallForegroundBrush", Color.FromArgb(0xFF, 0x49, 0xA2, 0xF0) },
            { "MessageCallMissedForegroundBrush", Color.FromArgb(0xFF, 0xED, 0x50, 0x50) },
            { "MessageReactionBackgroundBrush", Color.FromArgb(0xFF, 0x3A, 0x47, 0x54) },
            { "MessageReactionForegroundBrush", Color.FromArgb(0xFF, 0x67, 0xBB, 0xF3) },
            { "MessageReactionChosenBackgroundBrush", Color.FromArgb(0xFF, 0x6E, 0xB2, 0xEE) },
            { "MessageReactionChosenForegroundBrush", Color.FromArgb(0xFF, 0x33, 0x39, 0x3F) },
        };

    }
}
