//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Text;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels.Settings;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Telegram.Services.Settings
{
    public enum TelegramTheme
    {
        Light = 1 << 1,
        Dark = 1 << 2
    }

    public enum TelegramThemeType
    {
        Classic = 0,
        Day = 1,
        Night = 2,
        Tinted = 3,
        Custom = 4
    }

    public enum NightMode
    {
        Disabled,
        Scheduled,
        Automatic,
        System
    }

    public enum AccentShade
    {
        Default,
        Light1,
        Light2,
        Light3,
        Dark1,
        Dark2,
        Dark3
    }

    public readonly struct Acrylic
    {
        public static Acrylic<Color> Color(Color tint, Color fallback, double opacity, double? luminosity = null)
        {
            return new Acrylic<Color>(tint, fallback, opacity, luminosity);
        }

        public static Acrylic<AccentShade> Shade(AccentShade tint, AccentShade fallback, double opacity, double? luminosity = null)
        {
            return new Acrylic<AccentShade>(tint, fallback, opacity, luminosity);
        }
    }

    public readonly struct Acrylic<T> where T : struct
    {
        public T TintColor { get; }

        public T FallbackColor { get; }

        public double TintOpacity { get; }

        public double? TintLuminosityOpacity { get; }

        public Acrylic(T tint, T fallback, double opacity, double? tonality = null)
        {
            TintColor = tint;
            FallbackColor = fallback;
            TintOpacity = opacity;
            TintLuminosityOpacity = tonality;
        }
    }

    public partial class InstalledEmojiSet
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public int Version { get; set; }
    }

    public partial class AppearanceSettings : SettingsServiceBase
    {
        public AppearanceSettings()
            : base("Theme")
        {
            Migrate();
        }

        private string _emojiSet;
        public string EmojiSet
        {
            get => _emojiSet ??= GetValueOrDefault(_container, "EmojiSetId", "apple");
            set => AddOrUpdateValue(ref _emojiSet, _container, "EmojiSetId", value);
        }

        // Taken by every public method of the appearance model. UpdateDefaultBackground is called
        // on each account's TDLib thread while windows read and write the same state on theirs.
        // Always before a store's own lock, never inside one. What the methods hand out is never
        // changed in place - worn and stored settings are replaced - so it stays safe to read after.
        private readonly object _themeLock = new();

        private ThemeStore _presets;
        private ThemeVariantStore _variants;
        private ThemeStore _themeFiles;

        // A preset is not customizable: its colours are the server's or the house's, so all this
        // keeps is the background it was last worn with.
        private ThemeStore Presets
        {
            get
            {
                lock (_themeLock)
                {
                    return _presets ??= new ThemeStore(_container.GetContainer("Presets"));
                }
            }
        }

        /// <summary>
        /// The themes page's variants, keyed on the built-in and an id. Entirely the user's.
        /// </summary>
        public ThemeVariantStore Variants
        {
            get
            {
                lock (_themeLock)
                {
                    return _variants ??= new ThemeVariantStore(_container.GetContainer("Variants"));
                }
            }
        }

        // The background of each installed theme file; its colours are in the file.
        private ThemeStore ThemeFiles
        {
            get
            {
                lock (_themeLock)
                {
                    return _themeFiles ??= new ThemeStore(_container.GetContainer("ThemeFiles"));
                }
            }
        }

        private TelegramTheme? _requestedTheme;
        public TelegramTheme RequestedTheme
        {
            get => _requestedTheme ??= (TelegramTheme)GetValueOrDefault(_container, "Theme", (int)GetSystemTheme());
            set => AddOrUpdateValue(_container, "Theme", (int)(_requestedTheme = value));
        }

        private NightMode? _nightMode;
        public NightMode NightMode
        {
            get => _nightMode ??= (NightMode)GetValueOrDefault(_container, "NightMode", (int)NightMode.Disabled);
            set => AddOrUpdateValue(_container, "NightMode", (int)(_nightMode = value));
        }

        private bool? _forceNightMode;
        public bool ForceNightMode
        {
            get => _forceNightMode ??= GetValueOrDefault(_container, "ForceNightMode", false);
            set => AddOrUpdateValue(ref _forceNightMode, _container, "ForceNightMode", value);
        }

        private bool? _isLocationBased;
        public bool IsLocationBased
        {
            get => _isLocationBased ??= GetValueOrDefault(_container, "IsLocationBased", false);
            set => AddOrUpdateValue(_container, "IsLocationBased", _isLocationBased = value);
        }

        private TimeSpan? _from;
        public TimeSpan From
        {
            get
            {
                if (_from == null)
                {
                    var value = GetValueOrDefault("From", 22 * 60 + 0);
                    var currentHour = value / 60;

                    _from = new TimeSpan(currentHour, value - currentHour * 60, 0);
                }

                return _from ?? new TimeSpan(22, 0, 0);
            }
            set
            {
                _from = value;
                AddOrUpdateValue("From", value.Hours * 60 + value.Minutes);
            }
        }

        private TimeSpan? _to;
        public TimeSpan To
        {
            get
            {
                if (_to == null)
                {
                    var value = GetValueOrDefault("To", 9 * 60 + 0);
                    var currentHour = value / 60;

                    _to = new TimeSpan(currentHour, value - currentHour * 60, 0);
                }

                return _to ?? new TimeSpan(9, 0, 0);
            }
            set
            {
                _to = value;
                AddOrUpdateValue("To", value.Hours * 60 + value.Minutes);
            }
        }

        private Location _location;
        public Location Location
        {
            get => _location ??= new Location { Latitude = GetValueOrDefault("Latitude", 0d), Longitude = GetValueOrDefault("Longitude", 0d) };
            set
            {
                _location = value;
                AddOrUpdateValue("Latitude", value.Latitude);
                AddOrUpdateValue("Longitude", value.Longitude);
            }
        }

        private string _town;
        public string Town
        {
            get => _town ??= GetValueOrDefault("Town", string.Empty);
            set => AddOrUpdateValue("Town", _town = value);
        }

        // RequestedTheme defaults to whatever the system is set to, so this has to stay on the
        // settings rather than move to NightModeService: the service reads the settings, and a
        // dependency the other way would recurse through both singletons' constructors.
        public TelegramTheme GetSystemTheme()
        {
            var app = BootStrapper.Current as App;
            var current = app.UISettings.GetColorValue(UIColorType.Background);

            return current == Colors.Black ? TelegramTheme.Dark : TelegramTheme.Light;
        }

        private static bool? _useDefaultScaling;
        public bool UseDefaultScaling
        {
            get => _useDefaultScaling ??= GetValueOrDefault("UseDefaultScaling", true);
            set => AddOrUpdateValue(ref _useDefaultScaling, "UseDefaultScaling", value);
        }

        private static int? _scaling;
        public int Scaling
        {
            get => _scaling ??= GetValueOrDefault("Scaling", 0);
            set => AddOrUpdateValue(ref _scaling, "Scaling", value);
        }

        private static int? _messageFontSize;
        public int MessageFontSize
        {
            get => _messageFontSize ??= (int)GetValueOrDefault("MessageFontSize", 14d);
            set => AddOrUpdateValue("MessageFontSize", (double)(_messageFontSize = value));
        }

        public int CaptionFontSize => MessageFontSize - 2;

        private static int? _bubbleRadius;
        public int BubbleRadius
        {
            get => _bubbleRadius ??= GetValueOrDefault("BubbleRadius", 15);
            set => AddOrUpdateValue(ref _bubbleRadius, "BubbleRadius", value);
        }

        public int CornerRadius => BubbleRadius > 0 ? BubbleRadius < 15 ? BubbleRadius : 24 : 0;

        private bool? _isQuickReplySelected;
        public bool IsQuickReplySelected
        {
            get => _isQuickReplySelected ??= GetValueOrDefault("IsQuickReplySelected", true);
            set => AddOrUpdateValue(ref _isQuickReplySelected, "IsQuickReplySelected", value);
        }

        private string _fontFamily;
        public string FontFamily
        {
            get => _fontFamily ??= GetValueOrDefault("FontFamily", string.Empty);
            set => AddOrUpdateValue(ref _fontFamily, "FontFamily", value);
        }

        #region Worn theme

        private sealed class WornTheme
        {
            public ThemeIdentity Identity;
            public ThemeSettings Settings;
        }

        private WornTheme _wornLight;
        private WornTheme _wornDark;

        private WornTheme GetWornTheme(TelegramTheme requested)
        {
            if (requested == TelegramTheme.Light)
            {
                return _wornLight ??= LoadWorn(TelegramTheme.Light);
            }

            return _wornDark ??= LoadWorn(TelegramTheme.Dark);
        }

        private WornTheme LoadWorn(TelegramTheme requested)
        {
            if (_container.TryGetContainer(WornKey(requested), out ISettingsStore container))
            {
                var identity = new ThemeIdentity(
                    (ThemeKind)container.GetValueOrDefault("Kind", 0),
                    container.GetValueOrDefault("Id", string.Empty),
                    (TelegramThemeType)container.GetValueOrDefault("Type", 0));

                if (identity.Id.Length > 0)
                {
                    return new WornTheme
                    {
                        Identity = identity,
                        Settings = Rebuild(requested, identity, ThemeSettingsStore.Load(container))
                    };
                }
            }

            var house = ThemeIdentity.Preset(ThemeData.DefaultThemeId);
            return new WornTheme
            {
                Identity = house,
                Settings = Rebuild(requested, house, null)
            };
        }

        // Only what cannot be computed is read back: the house's colours are code, and a theme
        // file's path follows its name.
        private static ThemeSettings Rebuild(TelegramTheme requested, ThemeIdentity identity, ThemeSettings stored)
        {
            if (identity.Kind == ThemeKind.File)
            {
                return CreateThemeFile(identity.Id, stored, stored?.Background);
            }
            else if (identity.Kind == ThemeKind.Preset && identity.Id == ThemeData.DefaultThemeId)
            {
                return ThemeSettingsStore.WithBackground(GetHouse(requested), stored?.Background);
            }

            return stored;
        }

        private void Wear(TelegramTheme requested, ThemeIdentity identity, ThemeSettings settings)
        {
            var worn = GetWornTheme(requested);
            worn.Identity = identity;
            worn.Settings = settings;

            SaveWorn(requested, worn);

            // A change still in flight for another theme is abandoned: when no update comes back for
            // it, as when the wallpaper picked is the one TDLib already holds, the marker would
            // otherwise claim the update this theme's own background produces.
            var key = PendingKey(requested == TelegramTheme.Dark);
            if (_container.ContainsKey(key) && GetPending(key) != identity)
            {
                RemovePending(key);
            }
        }

        private void SaveWorn(TelegramTheme requested, WornTheme worn)
        {
            var container = _container.GetContainer(WornKey(requested));
            ThemeSettingsStore.Save(container, ThemeSettingsStore.Storable(worn.Settings));

            container.SetValue("Kind", (int)worn.Identity.Kind);
            container.SetValue("Id", worn.Identity.Id);
            container.SetValue("Type", (int)worn.Identity.Type);
        }

        private static string WornKey(TelegramTheme requested)
        {
            return requested == TelegramTheme.Light ? "WornLight" : "WornDark";
        }

        /// <summary>
        /// What a window is drawn with: null for the bundled theme, <see cref="CustomThemeSettings"/>
        /// for a theme file.
        /// </summary>
        /// <remarks>
        /// Read from the local store rather than from TDLib, because a window is themed at creation -
        /// long before a connection exists.
        /// </remarks>
        public ThemeSettings GetSettings(TelegramTheme requested)
        {
            lock (_themeLock)
            {
                var worn = GetWornTheme(requested);
                if (worn.Identity.Kind == ThemeKind.File)
                {
                    return System.IO.File.Exists(GetThemeFilePath(worn.Identity.Id)) ? worn.Settings : null;
                }
                else if (IsBundled(requested, worn))
                {
                    return null;
                }

                return worn.Settings;
            }
        }

        // The light house preset is the bundled theme, which carries no values of its own:
        // colorizing it is a deliberate switch, not a side effect. Keyed on the identity, because a
        // chat theme based on Classic is colorized like any other - and so is Classic's 🏠 variant,
        // which is the user's to edit.
        private static bool IsBundled(TelegramTheme requested, WornTheme worn)
        {
            return worn.Identity.Kind == ThemeKind.Preset
                && worn.Identity.Id == ThemeData.DefaultThemeId
                && ThemeSettingsStore.ToThemeType(worn.Settings.BaseTheme, requested) == TelegramThemeType.Classic;
        }

        public ThemeIdentity GetWorn(TelegramTheme requested)
        {
            lock (_themeLock)
            {
                return GetWornTheme(requested).Identity;
            }
        }

        /// <summary>
        /// The built-in <paramref name="requested"/> is drawn on, derived from what it wears:
        /// <see cref="TelegramThemeType.Custom"/> for a theme file.
        /// </summary>
        public TelegramThemeType GetBuiltIn(TelegramTheme requested)
        {
            lock (_themeLock)
            {
                var worn = GetWornTheme(requested);
                if (worn.Identity.Kind == ThemeKind.File)
                {
                    return TelegramThemeType.Custom;
                }

                return ThemeSettingsStore.ToThemeType(worn.Settings.BaseTheme, requested);
            }
        }

        #endregion

        #region Houses

        /// <summary>
        /// The house - 🏠 - of a base: its default built-in's, Classic or Night. Built in code,
        /// never stored.
        /// </summary>
        public static ThemeSettings GetHouse(TelegramTheme requested)
        {
            return GetHouse(requested == TelegramTheme.Dark ? TelegramThemeType.Night : TelegramThemeType.Classic);
        }

        /// <summary>
        /// A built-in's own defaults, which is what its 🏠 variant starts as. The values are today's
        /// and may change; what matters is that each built-in has its own.
        /// </summary>
        public static ThemeSettings GetHouse(TelegramThemeType type)
        {
            if (type == TelegramThemeType.Night)
            {
                return new ThemeSettings
                {
                    BaseTheme = new BuiltInThemeNight(),
                    AccentColor = 0x71BAFA,
                    HasOutgoingMessageAccentColor = true,
                    OutgoingMessageAccentColor = 0x2B5278,
                    OutgoingMessageFill = new BackgroundFillFreeformGradient(new[] { 0x258DE5, 0x4272DF, 0x8146D7, 0x9F3EAA }),
                    Background = GetDefaultBackground(true)
                };
            }
            else if (type == TelegramThemeType.Classic)
            {
                return new ThemeSettings
                {
                    BaseTheme = new BuiltInThemeClassic(),
                    AccentColor = 0x158DCD,
                    HasOutgoingMessageAccentColor = true,
                    OutgoingMessageAccentColor = 0xF0FDDF,
                    OutgoingMessageFill = new BackgroundFillSolid(0xF0FDDF),
                    Background = GetDefaultBackground(false)
                };
            }

            // Day and Tinted are their accent and nothing else: with no outgoing accent and no
            // fill, the colorizer derives the outgoing bubble from it.
            var accent = ThemeInfoBase.Accents[type].ToValue();

            return new ThemeSettings
            {
                BaseTheme = ThemeSettingsStore.ToBuiltInTheme(type),
                AccentColor = accent,
                OutgoingMessageAccentColor = accent,
                Background = GetDefaultBackground(GetBase(type) == TelegramTheme.Dark)
            };
        }

        public static TelegramTheme GetBase(TelegramThemeType type)
        {
            return type is TelegramThemeType.Night or TelegramThemeType.Tinted
                ? TelegramTheme.Dark
                : TelegramTheme.Light;
        }

        public static Background GetDefaultBackground(bool dark)
        {
            var freeform = dark ? new[] { 0x6C7FA6, 0x2E344B, 0x7874A7, 0x333258 } : new[] { 0xDBDDBB, 0x6BA587, 0xD5D88D, 0x88B884 };
            return new Background(0, true, dark, string.Empty,
                new Document(string.Empty, "application/x-tgwallpattern", null, null, TdExtensions.GetLocalFile("Assets\\Background.tgv", "Background")),
                new BackgroundTypePattern(new BackgroundFillFreeformGradient(freeform), dark ? 100 : 50, dark, false));
        }

        #endregion

        #region Wearing

        /// <summary>
        /// Wears 🏠 or a chat theme on one base, and says whether there was anything to wear: a chat
        /// theme may have nothing for this base.
        /// </summary>
        /// <remarks>
        /// The colours always come from <paramref name="colors"/> or the house, because a preset is
        /// not customizable. Only its background is remembered, seeded the first time from the one
        /// it comes with.
        /// </remarks>
        public bool WearPreset(TelegramTheme requested, string emoji, ThemeSettings colors)
        {
            lock (_themeLock)
            {
                ThemeSettings settings;

                if (string.IsNullOrEmpty(emoji) || emoji == ThemeData.DefaultThemeId)
                {
                    emoji = ThemeData.DefaultThemeId;
                    settings = GetHouse(requested);
                }
                else if (colors == null)
                {
                    return false;
                }
                else
                {
                    settings = colors;
                }

                var stored = Presets.Get(requested.ToString(), emoji);
                if (stored == null)
                {
                    Presets.Set(requested.ToString(), emoji, ThemeSettingsStore.WithBackground(new ThemeSettings(), settings.Background));
                    stored = Presets.Get(requested.ToString(), emoji);
                }

                Wear(requested, ThemeIdentity.Preset(emoji), ThemeSettingsStore.WithBackground(settings, stored.Background));
                return true;
            }
        }

        /// <summary>
        /// Wears a variant, storing <paramref name="seed"/> as its copy if it has none yet.
        /// </summary>
        /// <param name="recent">Whether the 🎨 card points at it: only for a pick on the themes page.</param>
        /// <remarks>
        /// Copied on wear and not only on edit: the window is themed before TDLib connects, so
        /// whatever a base wears has to be in the store already.
        /// </remarks>
        public void WearVariant(TelegramThemeType type, string id, ThemeSettings seed, bool recent)
        {
            lock (_themeLock)
            {
                var stored = Variants.Get(type, id);
                if (stored == null)
                {
                    if (seed == null)
                    {
                        return;
                    }

                    SaveVariant(type, id, seed);
                    stored = Variants.Get(type, id);
                }

                var requested = GetBase(type);
                var identity = ThemeIdentity.Variant(type, id);

                AddOrUpdateValue($"LastVariant{type}", id);

                if (recent)
                {
                    SetRecent(requested, identity);
                }

                Wear(requested, identity, ThemeSettingsStore.Copy(stored));
            }
        }

        /// <summary>
        /// Wears a built-in: the variant it was last worn with, or its 🏠 variant.
        /// </summary>
        public void WearBuiltIn(TelegramThemeType type, bool recent)
        {
            lock (_themeLock)
            {
                var id = GetLastVariant(type);
                WearVariant(type, Variants.Has(type, id) ? id : ThemeData.DefaultThemeId, GetHouse(type), recent);
            }
        }

        /// <summary>
        /// The variant a built-in was last worn with, which is also the one its list highlights.
        /// </summary>
        public string GetLastVariant(TelegramThemeType type)
        {
            return GetValueOrDefault($"LastVariant{type}", ThemeData.DefaultThemeId);
        }

        /// <param name="fallback">
        /// What a file without a background of its own starts with, the first time it is worn; null
        /// leaves it empty, so it adopts the next background TDLib sends.
        /// </param>
        public void WearThemeFile(TelegramTheme requested, string path, bool recent, Background fallback = null)
        {
            lock (_themeLock)
            {
                var id = GetThemeFileId(path);
                if (id == null)
                {
                    return;
                }

                var identity = ThemeIdentity.File(id);
                var header = ThemeCustomInfo.ReadSettings(path, requested);

                // As a chat theme seeds its preset: the file's own background the first time, and from
                // then on the one the user left it with.
                var stored = ThemeFiles.Get(FilePrefix, id);
                if (stored == null)
                {
                    ThemeFiles.Set(FilePrefix, id, ThemeSettingsStore.WithBackground(new ThemeSettings(), header?.Background ?? fallback));
                    stored = ThemeFiles.Get(FilePrefix, id);
                }

                if (recent)
                {
                    SetRecent(requested, identity);
                }

                Wear(requested, identity, CreateThemeFile(id, header, stored.Background));
            }
        }

        // A v1 file has no header, which leaves everything but the path and the background empty.
        private static CustomThemeSettings CreateThemeFile(string id, ThemeSettings header, Background background)
        {
            return new CustomThemeSettings
            {
                Path = GetThemeFilePath(id),
                BaseTheme = header?.BaseTheme,
                AccentColor = header?.AccentColor ?? 0,
                HasOutgoingMessageAccentColor = header?.HasOutgoingMessageAccentColor ?? false,
                OutgoingMessageAccentColor = header?.OutgoingMessageAccentColor ?? 0,
                OutgoingMessageFill = header?.OutgoingMessageFill,
                AnimateOutgoingMessageFill = header?.AnimateOutgoingMessageFill ?? false,
                Background = background
            };
        }

        /// <summary>
        /// The variant or theme file last applied from the themes page - the appearance page's 🎨
        /// card. A pointer, not a copy: editing the variant edits the card.
        /// </summary>
        public bool TryGetRecent(TelegramTheme requested, out ThemeIdentity identity)
        {
            var id = GetValueOrDefault($"Recent{requested}Id", string.Empty);
            if (id.Length == 0)
            {
                identity = null;
                return false;
            }

            identity = new ThemeIdentity(
                (ThemeKind)GetValueOrDefault($"Recent{requested}Kind", 0),
                id,
                (TelegramThemeType)GetValueOrDefault($"Recent{requested}Type", 0));

            return true;
        }

        private void SetRecent(TelegramTheme requested, ThemeIdentity identity)
        {
            AddOrUpdateValue($"Recent{requested}Kind", (int)(identity?.Kind ?? ThemeKind.Preset));
            AddOrUpdateValue($"Recent{requested}Id", identity?.Id ?? string.Empty);
            AddOrUpdateValue($"Recent{requested}Type", (int)(identity?.Type ?? TelegramThemeType.Classic));
        }

        /// <summary>
        /// Wears what the 🎨 card points at, and says whether there was anything to wear.
        /// </summary>
        public bool WearRecent(TelegramTheme requested)
        {
            lock (_themeLock)
            {
                if (!TryGetRecent(requested, out ThemeIdentity identity))
                {
                    return false;
                }

                if (identity.Kind == ThemeKind.File)
                {
                    var path = GetThemeFilePath(identity.Id);
                    if (!System.IO.File.Exists(path))
                    {
                        return false;
                    }

                    WearThemeFile(requested, path, true);
                }
                else if (Variants.Has(identity.Type, identity.Id))
                {
                    WearVariant(identity.Type, identity.Id, null, true);
                }
                else
                {
                    return false;
                }

                return true;
            }
        }

        #endregion

        #region Variants and theme files

        public void SaveVariant(TelegramThemeType type, string id, ThemeSettings settings)
        {
            lock (_themeLock)
            {
                // From the key, never from what was passed: the store is keyed on the very thing this
                // field names, so the two can only ever disagree by being wrong.
                var copy = ThemeSettingsStore.Copy(settings);
                copy.BaseTheme = ThemeSettingsStore.ToBuiltInTheme(type);

                Variants.Set(type, id, copy);

                var requested = GetBase(type);
                var worn = GetWornTheme(requested);

                if (worn.Identity == ThemeIdentity.Variant(type, id))
                {
                    worn.Settings = ThemeSettingsStore.Copy(Variants.Get(type, id));
                    SaveWorn(requested, worn);
                }
            }
        }

        public string CreateVariant(TelegramThemeType type, ThemeSettings settings)
        {
            lock (_themeLock)
            {
                var copy = ThemeSettingsStore.Copy(settings);
                copy.BaseTheme = ThemeSettingsStore.ToBuiltInTheme(type);

                return Variants.Create(type, copy);
            }
        }

        /// <summary>
        /// Puts a seeded variant back to <paramref name="defaults"/>. Unless it is worn, that is the
        /// same as having no copy at all.
        /// </summary>
        public void ResetVariant(TelegramThemeType type, string id, ThemeSettings defaults)
        {
            lock (_themeLock)
            {
                if (GetWorn(GetBase(type)) == ThemeIdentity.Variant(type, id))
                {
                    SaveVariant(type, id, defaults);
                }
                else
                {
                    Variants.Remove(type, id);
                }
            }
        }

        /// <summary>
        /// Deletes a variant made with "+", and says whether its base now wears something else.
        /// </summary>
        public bool DeleteVariant(TelegramThemeType type, string id)
        {
            lock (_themeLock)
            {
                var requested = GetBase(type);
                var identity = ThemeIdentity.Variant(type, id);

                Variants.Remove(type, id);

                if (GetLastVariant(type) == id)
                {
                    _container.Remove($"LastVariant{type}");
                }

                if (TryGetRecent(requested, out ThemeIdentity recent) && recent == identity)
                {
                    SetRecent(requested, null);
                }

                if (GetWorn(requested) == identity)
                {
                    WearBuiltIn(type, false);
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Forgets a deleted theme file, and says whether a base now wears something else.
        /// </summary>
        public bool DeleteThemeFile(string path)
        {
            lock (_themeLock)
            {
                var id = GetThemeFileId(path);
                if (id == null)
                {
                    return false;
                }

                ThemeFiles.Remove(FilePrefix, id);

                // Not short-circuited: either base may wear it, and both have to forget it.
                return ForgetThemeFile(TelegramTheme.Light, id) | ForgetThemeFile(TelegramTheme.Dark, id);
            }
        }

        private bool ForgetThemeFile(TelegramTheme requested, string id)
        {
            var identity = ThemeIdentity.File(id);

            if (TryGetRecent(requested, out ThemeIdentity recent) && recent == identity)
            {
                SetRecent(requested, null);
            }

            if (GetWorn(requested) == identity)
            {
                WearPreset(requested, ThemeData.DefaultThemeId, null);
                return true;
            }

            return false;
        }

        private const string FilePrefix = "File";

        public static string GetThemeFileId(string path)
        {
            var name = System.IO.Path.GetFileName(path);
            return string.IsNullOrEmpty(name) ? null : name;
        }

        // Where ThemeService installs them; the id is only the name so that it survives the folder
        // moving, as LocalState does on a reinstall.
        public static string GetThemeFilePath(string id)
        {
            return System.IO.Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "themes", id);
        }

        #endregion

        #region Backgrounds

        /// <summary>
        /// The background a theme remembers, and whether it remembers anything at all: a preset never
        /// worn has nothing stored, and shows the one it comes with.
        /// </summary>
        public bool TryGetBackground(TelegramTheme requested, ThemeIdentity identity, out Background background)
        {
            lock (_themeLock)
            {
                var stored = GetStored(requested, identity);
                background = stored?.Background;
                return stored != null;
            }
        }

        private ThemeSettings GetStored(TelegramTheme requested, ThemeIdentity identity)
        {
            return identity.Kind switch
            {
                ThemeKind.Preset => Presets.Get(requested.ToString(), identity.Id),
                ThemeKind.Variant => Variants.Get(identity.Type, identity.Id),
                _ => ThemeFiles.Get(FilePrefix, identity.Id)
            };
        }

        private bool SetBackground(TelegramTheme requested, ThemeIdentity identity, Background background)
        {
            var stored = GetStored(requested, identity);
            if (stored == null && identity.Kind == ThemeKind.Variant)
            {
                return false;
            }

            var updated = ThemeSettingsStore.WithBackground(stored ?? new ThemeSettings(), background);

            switch (identity.Kind)
            {
                case ThemeKind.Preset:
                    Presets.Set(requested.ToString(), identity.Id, updated);
                    break;
                case ThemeKind.Variant:
                    Variants.Set(identity.Type, identity.Id, updated);
                    break;
                default:
                    ThemeFiles.Set(FilePrefix, identity.Id, updated);
                    break;
            }

            var worn = GetWornTheme(requested);
            if (worn.Identity == identity)
            {
                worn.Settings = ThemeSettingsStore.WithBackground(worn.Settings, background);
                SaveWorn(requested, worn);
            }

            return true;
        }

        /// <summary>
        /// Records that a background change is in flight on a session, and which theme it is for, so
        /// the update it produces is written there and a session merely replaying its state is not.
        /// </summary>
        /// <remarks>
        /// The write happens in <see cref="UpdateDefaultBackground"/> rather than here: a local
        /// file uploads asynchronously and the result may only arrive on a later launch, so the
        /// marker is persisted. TDLib holds a background per account while the theme, and with it
        /// the wallpaper, is one app-wide thing here as in every other client - which is why the
        /// session has to be part of the marker rather than ignored.
        /// </remarks>
        public void SetDefaultBackground(int session, bool forDarkTheme)
        {
            lock (_themeLock)
            {
                var key = PendingKey(forDarkTheme);
                var identity = GetWorn(forDarkTheme ? TelegramTheme.Dark : TelegramTheme.Light);

                AddOrUpdateValue(key, session);
                AddOrUpdateValue(key + "Kind", (int)identity.Kind);
                AddOrUpdateValue(key + "Id", identity.Id);
                AddOrUpdateValue(key + "Type", (int)identity.Type);
            }
        }

        /// <summary>
        /// As <see cref="SetDefaultBackground"/>: deleting produces an update too.
        /// </summary>
        public void DeleteDefaultBackground(int session, bool forDarkTheme)
        {
            SetDefaultBackground(session, forDarkTheme);
        }

        /// <summary>
        /// Writes a background onto the theme it was chosen for, or adopts it onto the worn theme if
        /// that has none, and says whether it did either.
        /// </summary>
        public bool UpdateDefaultBackground(int session, bool forDarkTheme, Background background)
        {
            lock (_themeLock)
            {
                var requested = forDarkTheme ? TelegramTheme.Dark : TelegramTheme.Light;
                var key = PendingKey(forDarkTheme);

                ThemeIdentity identity;

                if (GetValueOrDefault(key, -1) == session)
                {
                    identity = GetPending(key);
                    RemovePending(key);
                }
                else
                {
                    identity = GetWorn(requested);

                    // Any other session is replaying what its account happens to hold, which must not
                    // overwrite a wallpaper the user chose. Adopting when there is none is what lets a
                    // fresh install inherit whatever the account already had.
                    if (TryGetBackground(requested, identity, out Background stored) && stored != null)
                    {
                        return false;
                    }
                }

                return SetBackground(requested, identity, background);
            }
        }

        private static string PendingKey(bool forDarkTheme)
        {
            return forDarkTheme ? "PendingBackgroundDark" : "PendingBackgroundLight";
        }

        private ThemeIdentity GetPending(string key)
        {
            return new ThemeIdentity(
                (ThemeKind)GetValueOrDefault(key + "Kind", 0),
                GetValueOrDefault(key + "Id", string.Empty),
                (TelegramThemeType)GetValueOrDefault(key + "Type", 0));
        }

        private void RemovePending(string key)
        {
            _container.Remove(key);
            _container.Remove(key + "Kind");
            _container.Remove(key + "Id");
            _container.Remove(key + "Type");
        }

        #endregion

        #region Migration

        /// <summary>
        /// Moves a production install onto worn themes. Only production keys are read; what beta
        /// builds wrote is dropped. The production keys are left in place, so a downgrade still
        /// finds them.
        /// </summary>
        private void Migrate()
        {
            if (GetValueOrDefault("AppearanceVersion", 0) >= 1)
            {
                return;
            }

            MigrateThemePath();
            DropBeta();

            var light = MigrateBase(TelegramTheme.Light);
            var dark = MigrateBase(TelegramTheme.Dark);

            // An accent picked for a built-in that is not worn is still the user's choice.
            foreach (var type in new[] { TelegramThemeType.Day, TelegramThemeType.Night, TelegramThemeType.Tinted })
            {
                if (type != light && type != dark && TryGetLegacyAccent(type, out int accent))
                {
                    Variants.Create(type, CreateLegacyVariant(type, accent));
                }
            }

            AddOrUpdateValue("AppearanceVersion", 1);
        }

        // The single theme setting of old builds, before light and dark were kept apart.
        private void MigrateThemePath()
        {
            if (TryGetValue(_container, "ThemePath", out string path))
            {
                if (path.EndsWith("Assets\\Themes\\DarkBlue.unigram-theme"))
                {
                    RequestedTheme = TelegramTheme.Dark;
                    AddOrUpdateValue("ThemeTypeDark", (int)TelegramThemeType.Tinted);
                }
                else if (path.Length > 0 && System.IO.File.Exists(path))
                {
                    AddOrUpdateValue($"ThemeType{RequestedTheme}", (int)TelegramThemeType.Custom);
                    AddOrUpdateValue($"ThemeCustom{RequestedTheme}", path);
                }

                _container.Remove("ThemePath");
            }
            else if (TryGetValue(_container, "ThemeType", out int type))
            {
                AddOrUpdateValue($"ThemeType{RequestedTheme}", type);

                if ((TelegramThemeType)type == TelegramThemeType.Custom && TryGetValue(_container, "ThemeCustom", out string custom))
                {
                    AddOrUpdateValue($"ThemeCustom{RequestedTheme}", custom);
                }

                _container.Remove("ThemeCustom");
                _container.Remove("ThemeType");
            }
        }

        // Only beta builds wrote these, and under this model nothing in them is worth carrying.
        private void DropBeta()
        {
            // Copied: deleting while enumerating the live container names is not allowed.
            foreach (var name in new List<string>(_container.ContainerNames))
            {
                if (name == "Variants" || IsBetaPreset(name))
                {
                    _container.DeleteContainer(name);
                }
            }

            foreach (var requested in new[] { TelegramTheme.Light, TelegramTheme.Dark })
            {
                for (var type = TelegramThemeType.Classic; type <= TelegramThemeType.Tinted; type++)
                {
                    _container.Remove($"ThemeEmoji{requested}{type}");
                    _container.Remove($"ThemeVariant{requested}{type}");
                }

                _container.Remove($"ThemeRecentType{requested}");
                _container.Remove($"ThemeRecentId{requested}");

                var pending = PendingKey(requested == TelegramTheme.Dark);
                _container.Remove(pending);
                _container.Remove(pending + "Type");
                _container.Remove(pending + "Id");
                _container.Remove(pending + "Variant");
            }

            _container.Remove("VariantsStore");
            _container.Remove("LegacyAccentsMigrated");
        }

        // "DaySettings", "Day_1F981Settings" and so on: one per built-in and chat theme.
        private static bool IsBetaPreset(string name)
        {
            if (!name.EndsWith("Settings"))
            {
                return false;
            }

            for (var type = TelegramThemeType.Classic; type <= TelegramThemeType.Custom; type++)
            {
                if (name.StartsWith(type.ToString()))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Wears what production showed on one base, and returns the built-in whose accent that
        /// used, if any.
        /// </summary>
        private TelegramThemeType? MigrateBase(TelegramTheme requested)
        {
            var type = (TelegramThemeType)GetValueOrDefault($"ThemeType{requested}", 0);

            if (requested == TelegramTheme.Dark && type is TelegramThemeType.Classic or TelegramThemeType.Day)
            {
                type = TelegramThemeType.Night;
            }
            else if (requested == TelegramTheme.Light && type is TelegramThemeType.Night or TelegramThemeType.Tinted)
            {
                type = TelegramThemeType.Classic;
            }

            // The chat theme came first: it was app-wide, cached without a built-in, and drawn on
            // the base's own, with Classic and theme files tinted as Day and Tinted.
            var name = GetValueOrDefault<string>("ChatThemeName", null);
            if (name != null && _container.TryGetContainer(requested == TelegramTheme.Light ? "ChatThemeLight" : "ChatThemeDark", out ISettingsStore cached))
            {
                var half = ThemeSettingsStore.Load(cached);
                half.BaseTheme = ThemeSettingsStore.ToBuiltInTheme(type switch
                {
                    TelegramThemeType.Classic => TelegramThemeType.Day,
                    TelegramThemeType.Custom => requested == TelegramTheme.Light ? TelegramThemeType.Day : TelegramThemeType.Tinted,
                    _ => type
                });

                WearPreset(requested, name, half);
                return null;
            }

            if (type == TelegramThemeType.Custom)
            {
                var id = GetThemeFileId(GetValueOrDefault($"ThemeCustom{requested}", string.Empty));
                if (id != null && System.IO.File.Exists(GetThemeFilePath(id)))
                {
                    WearThemeFile(requested, GetThemeFilePath(id), true);
                    return null;
                }

                type = requested == TelegramTheme.Light ? TelegramThemeType.Classic : TelegramThemeType.Night;
            }

            if (TryGetLegacyAccent(type, out int accent))
            {
                WearVariant(type, Variants.Create(type, CreateLegacyVariant(type, accent)), null, true);
            }
            else if (type == ThemeSettingsStore.ToThemeType(GetHouse(requested).BaseTheme, requested))
            {
                WearPreset(requested, ThemeData.DefaultThemeId, null);
            }
            else
            {
                WearBuiltIn(type, false);
            }

            return type;
        }

        // ARGB as an int: ColorEx.ToHex's output, despite the name. Equal to the default is no choice.
        private bool TryGetLegacyAccent(TelegramThemeType type, out int accent)
        {
            if (TryGetValue(_container, $"{type}Accent", out int argb))
            {
                accent = ColorEx.FromHex(argb).ToValue();
                return accent != ThemeInfoBase.Accents[type].ToValue();
            }

            accent = 0;
            return false;
        }

        // How production drew an accent: the outgoing bubble derived from it, Night's on its
        // own gradient.
        private static ThemeSettings CreateLegacyVariant(TelegramThemeType type, int accent)
        {
            var settings = new ThemeSettings
            {
                BaseTheme = ThemeSettingsStore.ToBuiltInTheme(type),
                AccentColor = accent,
                OutgoingMessageAccentColor = accent
            };

            if (type == TelegramThemeType.Night)
            {
                settings.OutgoingMessageFill = new BackgroundFillFreeformGradient(new[] { 0x258DE5, 0x4272DF, 0x8146D7, 0x9F3EAA });
            }

            return settings;
        }

        #endregion
    }

    /// <summary>
    /// A theme read from a file. It carries the path and, for a v2 file, the settings of its header;
    /// the key overrides behind it are parsed by <see cref="ThemeCustomInfo"/> on demand and have no
    /// business in settings.dat.
    /// </summary>
    public partial class CustomThemeSettings : ThemeSettings
    {
        public string Path { get; set; }
    }

    /// <summary>
    /// Reads and writes the fields of a <see cref="ThemeSettings"/> that survive a restart, for the
    /// presets, the variants and the theme files.
    /// </summary>
    public static class ThemeSettingsStore
    {
        public static void Save(ISettingsStore container, ThemeSettings settings)
        {
            if (settings != null)
            {
                container.SetValue("BaseTheme", ToId(settings.BaseTheme));
                container.SetValue("AccentColor", settings.AccentColor);
                container.SetValue("HasOutgoingMessageAccentColor", settings.HasOutgoingMessageAccentColor);
                container.SetValue("OutgoingMessageAccentColor", settings.OutgoingMessageAccentColor);
                container.SetValue("OutgoingMessageFill", TdBackground.ToString(settings.OutgoingMessageFill) ?? string.Empty);
                container.SetValue("AnimateOutgoingMessageFill", settings.AnimateOutgoingMessageFill);

                // The link, because a Background carries a Document and that cannot live here. The
                // document is resolved from the name when it is needed; a fill needs no resolving.
                container.SetValue("Background", settings.Background != null
                    ? TdBackground.GetBackgroundLink(settings.Background.Name, settings.Background.Type) ?? string.Empty
                    : string.Empty);

                // The id is not in the link, and it is what InputBackgroundRemote takes: applying
                // this wallpaper to another account is a send, not a search, because of it.
                container.SetValue("BackgroundId", settings.Background?.Id ?? 0L);
                container.SetValue("BackgroundIsDark", settings.Background?.IsDark ?? false);
            }
            else
            {
                container.Remove("BaseTheme");
                container.Remove("AccentColor");
                container.Remove("HasOutgoingMessageAccentColor");
                container.Remove("OutgoingMessageAccentColor");
                container.Remove("OutgoingMessageFill");
                container.Remove("AnimateOutgoingMessageFill");
                container.Remove("Background");
                container.Remove("BackgroundId");
                container.Remove("BackgroundIsDark");
            }
        }

        /// <summary>
        /// What may be stored of <paramref name="settings"/>. The bundled background is only what a
        /// card draws when a theme has none: stored, it would make the theme look as if it had one,
        /// and the theme would never adopt the next updateDefaultBackground.
        /// </summary>
        /// <remarks>
        /// A copy rather than a change: the caller's object is usually the card still on screen.
        /// </remarks>
        public static ThemeSettings Storable(ThemeSettings settings)
        {
            if (settings?.Background is not Background background || !IsBundledBackground(background))
            {
                return settings;
            }

            return WithBackground(settings, null);
        }

        /// <summary>
        /// A copy, keeping a theme file's path: stored and worn settings are never an object the
        /// caller still holds.
        /// </summary>
        public static ThemeSettings Copy(ThemeSettings settings)
        {
            if (settings is CustomThemeSettings custom)
            {
                return new CustomThemeSettings
                {
                    Path = custom.Path,
                    BaseTheme = custom.BaseTheme,
                    AccentColor = custom.AccentColor,
                    HasOutgoingMessageAccentColor = custom.HasOutgoingMessageAccentColor,
                    OutgoingMessageAccentColor = custom.OutgoingMessageAccentColor,
                    OutgoingMessageFill = custom.OutgoingMessageFill,
                    AnimateOutgoingMessageFill = custom.AnimateOutgoingMessageFill,
                    Background = custom.Background
                };
            }

            return new ThemeSettings(settings.BaseTheme, settings.AccentColor, settings.Background, settings.OutgoingMessageFill, settings.AnimateOutgoingMessageFill, settings.HasOutgoingMessageAccentColor, settings.OutgoingMessageAccentColor);
        }

        public static ThemeSettings WithBackground(ThemeSettings settings, Background background)
        {
            var copy = Copy(settings);
            copy.Background = background;
            return copy;
        }

        // Every pattern has a slug except the one bundled with the app, and a stored link without
        // one could not be resolved back into anything. Not the id: the backgrounds page gives the
        // same pattern a sentinel instead of zero.
        public static bool IsBundledBackground(Background background)
        {
            return string.IsNullOrEmpty(background.Name) && background.Type is BackgroundTypePattern;
        }

        public static ThemeSettings Load(ISettingsStore container)
        {
            // Zero is a valid colour, so it cannot double as absent: an outgoing accent that was
            // never written has to read back as the accent, not as black.
            var accent = container.GetValueOrDefault("AccentColor", 0);
            var outgoing = container.GetValueOrDefault("OutgoingMessageAccentColor", accent);

            return new ThemeSettings
            {
                BaseTheme = FromId(container.GetValueOrDefault("BaseTheme", 0)),
                AccentColor = accent,
                // A cache written before td_api carried the flag falls back to what we inferred
                // from it then: an outbox accent that differs from the theme's was a real one.
                HasOutgoingMessageAccentColor = container.GetValueOrDefault("HasOutgoingMessageAccentColor", accent != outgoing),
                OutgoingMessageAccentColor = outgoing,
                OutgoingMessageFill = TdBackground.FromString(container.GetValueOrDefault("OutgoingMessageFill", string.Empty)),
                AnimateOutgoingMessageFill = container.GetValueOrDefault("AnimateOutgoingMessageFill", false),
                Background = LoadBackground(container.GetValueOrDefault("Background", string.Empty),
                    container.GetValueOrDefault("BackgroundId", 0L),
                    container.GetValueOrDefault("BackgroundIsDark", false))
            };
        }

        /// <summary>
        /// The background a link describes, with no document: whoever draws it resolves that from
        /// <see cref="Background.Name"/>, and a fill has nothing to resolve.
        /// </summary>
        /// <summary>
        /// Which built-in a set of settings is based on, falling back to the slot when the server
        /// names one we have no palette for - Arctic, today.
        /// </summary>
        public static TelegramThemeType ToThemeType(BuiltInTheme baseTheme, TelegramTheme requested)
        {
            return baseTheme switch
            {
                BuiltInThemeClassic => TelegramThemeType.Classic,
                BuiltInThemeDay => TelegramThemeType.Day,
                BuiltInThemeNight => TelegramThemeType.Night,
                BuiltInThemeTinted => TelegramThemeType.Tinted,
                _ => requested == TelegramTheme.Dark
                    ? TelegramThemeType.Night
                    : TelegramThemeType.Classic
            };
        }

        public static BuiltInTheme ToBuiltInTheme(TelegramThemeType type)
        {
            return type switch
            {
                TelegramThemeType.Classic => new BuiltInThemeClassic(),
                TelegramThemeType.Day => new BuiltInThemeDay(),
                TelegramThemeType.Night => new BuiltInThemeNight(),
                TelegramThemeType.Tinted => new BuiltInThemeTinted(),
                _ => null
            };
        }

        public static Background LoadBackground(string link, long id, bool dark)
        {
            var type = TdBackground.FromLink(link, out string name);
            if (type == null)
            {
                return null;
            }

            var background = new Background(id, false, dark, name ?? string.Empty, null, type);

            // Builds before Storable wrote the bundled pattern as a nameless link; reading it back
            // as absent is what puts those themes back to adopting.
            return IsBundledBackground(background) ? null : background;
        }

        // An explicit table rather than an ordinal: BuiltInTheme is generated from td_api, so a new
        // member landing in the middle would silently repoint every value already stored. Zero is
        // "absent", which is what a theme saved before this was written reads back as.
        private static int ToId(BuiltInTheme baseTheme)
        {
            return baseTheme switch
            {
                BuiltInThemeClassic => 1,
                BuiltInThemeDay => 2,
                BuiltInThemeNight => 3,
                BuiltInThemeTinted => 4,
                BuiltInThemeArctic => 5,
                _ => 0
            };
        }

        private static BuiltInTheme FromId(int id)
        {
            return id switch
            {
                1 => new BuiltInThemeClassic(),
                2 => new BuiltInThemeDay(),
                3 => new BuiltInThemeNight(),
                4 => new BuiltInThemeTinted(),
                5 => new BuiltInThemeArctic(),
                _ => null
            };
        }
    }

    public enum ThemeKind
    {
        Preset,
        Variant,
        File
    }

    /// <summary>
    /// Which theme a base wears or a card shows: what the UI selects, and where an edit or a
    /// background is written back. <see cref="Type"/> only means something for a variant.
    /// </summary>
    public sealed record ThemeIdentity(ThemeKind Kind, string Id, TelegramThemeType Type)
    {
        public static ThemeIdentity Preset(string emoji)
        {
            return new ThemeIdentity(ThemeKind.Preset, emoji, TelegramThemeType.Classic);
        }

        public static ThemeIdentity Variant(TelegramThemeType type, string id)
        {
            return new ThemeIdentity(ThemeKind.Variant, id, type);
        }

        public static ThemeIdentity File(string name)
        {
            return new ThemeIdentity(ThemeKind.File, name, TelegramThemeType.Classic);
        }
    }

    /// <summary>
    /// Theme settings keyed on a prefix and an id, each in a container of its own.
    /// </summary>
    /// <remarks>
    /// What <see cref="Get"/> returns is the cached instance: copy it before changing anything.
    /// </remarks>
    public partial class ThemeStore : SettingsServiceBase
    {
        private readonly Dictionary<string, ThemeSettings> _settings = new();

        // The variants store is also used directly by the view models, outside AppearanceSettings'
        // own lock, so the cache needs one of its own. Never held while taking that one.
        protected readonly object _lock = new();

        public ThemeStore(ISettingsStore container)
            : base(container)
        {
        }

        /// <summary>
        /// The stored settings, or null when there are none.
        /// </summary>
        public ThemeSettings Get(string prefix, string id)
        {
            var key = ToKey(prefix, id);

            lock (_lock)
            {
                if (_settings.TryGetValue(key, out ThemeSettings value))
                {
                    return value;
                }

                if (_container.TryGetContainer(key, out ISettingsStore container))
                {
                    value = ThemeSettingsStore.Load(container);
                }

                return _settings[key] = value;
            }
        }

        public void Set(string prefix, string id, ThemeSettings value)
        {
            var key = ToKey(prefix, id);
            value = ThemeSettingsStore.Storable(value);

            lock (_lock)
            {
                _settings[key] = value;
                ThemeSettingsStore.Save(_container.GetContainer(key), value);
            }
        }

        public void Remove(string prefix, string id)
        {
            var key = ToKey(prefix, id);

            lock (_lock)
            {
                _settings.Remove(key);
                _container.DeleteContainer(key);
            }
        }

        // Container names stop at 255 characters, and an escape is up to 7 for a single one of the
        // id's: a non-Latin file name of 40 would already throw in GetContainer. Well below that, so
        // the prefix never matters.
        private const int MaximumKeyLength = 128;

        // An id can be an emoji or a file name, and either can hold anything a container name
        // cannot, so everything but ASCII letters and digits is escaped. The trailing underscore
        // ends the escape: without it "_41" + "B" and "_41B" would be the same key.
        private static string ToKey(string prefix, string id)
        {
            var builder = new StringBuilder();
            builder.Append(prefix);
            builder.Append('_');

            for (int i = 0; i < id.Length;)
            {
                // A lone surrogate is escaped as itself: ConvertToUtf32 would throw on it.
                int point;
                if (char.IsSurrogatePair(id, i))
                {
                    point = char.ConvertToUtf32(id, i);
                    i += 2;
                }
                else
                {
                    point = id[i];
                    i++;
                }

                if (point < 0x80 && char.IsLetterOrDigit((char)point))
                {
                    builder.Append((char)point);
                }
                else
                {
                    builder.Append('_');
                    builder.Append(point.ToString("X"));
                    builder.Append('_');
                }
            }

            if (builder.Length <= MaximumKeyLength)
            {
                return builder.ToString();
            }

            // Hashed instead, with a double underscore the escaping can never produce right after
            // the prefix, so the two forms cannot collide. FNV-1a and not GetHashCode: these
            // names are on disk, and GetHashCode changes from one run to the next.
            var hash = 14695981039346656037UL;

            for (int i = 0; i < id.Length; i++)
            {
                hash ^= id[i];
                hash *= 1099511628211UL;
            }

            return prefix + "__" + hash.ToString("X16");
        }
    }

    /// <summary>
    /// The themes page's variants of each built-in. A seeded one takes the id of what it was seeded
    /// from - the house or a chat theme's name; one made with "+" has a generated id, see
    /// <see cref="IsCreated"/>, and is listed in creation order.
    /// </summary>
    public partial class ThemeVariantStore : ThemeStore
    {
        private readonly Dictionary<TelegramThemeType, List<string>> _created = new();

        public ThemeVariantStore(ISettingsStore container)
            : base(container)
        {
        }

        public static bool IsCreated(string id)
        {
            return id.StartsWith('+');
        }

        public ThemeSettings Get(TelegramThemeType type, string id)
        {
            return Get(type.ToString(), id);
        }

        public bool Has(TelegramThemeType type, string id)
        {
            return Get(type, id) != null;
        }

        public void Set(TelegramThemeType type, string id, ThemeSettings value)
        {
            Set(type.ToString(), id, value);
        }

        /// <summary>
        /// The variants made with "+", in creation order. A copy: the list can change on another
        /// thread while the caller walks it.
        /// </summary>
        public IReadOnlyList<string> GetCreated(TelegramThemeType type)
        {
            lock (_lock)
            {
                return new List<string>(GetCreatedList(type));
            }
        }

        // Under _lock only.
        private List<string> GetCreatedList(TelegramThemeType type)
        {
            if (_created.TryGetValue(type, out List<string> value))
            {
                return value;
            }

            var stored = GetValueOrDefault($"Created{type}", string.Empty);
            return _created[type] = stored.Length > 0
                ? new List<string>(stored.Split(','))
                : new List<string>();
        }

        public string Create(TelegramThemeType type, ThemeSettings settings)
        {
            lock (_lock)
            {
                var next = GetValueOrDefault("NextId", 1);
                AddOrUpdateValue("NextId", next + 1);

                // Never reused, so a stale pointer to a deleted variant cannot pick up a new one.
                var id = "+" + next;

                var created = GetCreatedList(type);
                created.Add(id);

                AddOrUpdateValue($"Created{type}", string.Join(",", created));
                Set(type, id, settings);

                return id;
            }
        }

        /// <summary>
        /// Drops the stored copy, and for a created variant the variant itself.
        /// </summary>
        public void Remove(TelegramThemeType type, string id)
        {
            lock (_lock)
            {
                Remove(type.ToString(), id);

                if (IsCreated(id))
                {
                    var created = GetCreatedList(type);
                    if (created.Remove(id))
                    {
                        AddOrUpdateValue($"Created{type}", string.Join(",", created));
                    }
                }
            }
        }
    }
}
