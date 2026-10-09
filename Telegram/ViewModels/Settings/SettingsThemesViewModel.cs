//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.Storage;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings
{
    public partial class SettingsThemesViewModel : ViewModelBase
    {
        private readonly IThemeService _themeService;
        private readonly bool _darkOnly;

        public SettingsThemesViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, IThemeService themeService, bool darkOnly = false)
            : base(clientService, settingsService, aggregator)
        {
            _themeService = themeService;
            _darkOnly = darkOnly;

            Themes = new RangeObservableCollection<ThemeData>();
            Items = new RangeObservableCollection<ThemeInfoBase>();
            Custom = new RangeObservableCollection<ThemeInfoBase>();
        }

        // Built once, and only for the themes page: each one loads its variants, backgrounds
        // included, which the night mode page never shows.
        private ThemeData[] _builtIns;

        /// <summary>
        /// The built-ins, then the installed theme files.
        /// </summary>
        public RangeObservableCollection<ThemeData> Themes { get; }

        private ThemeData _selectedItem;
        public ThemeData SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (Set(ref _selectedItem, value))
                {
                    UpdateTheme(value, value?.SelectedAccent);
                }
            }
        }

        public void UpdateTheme(ThemeData theme, ThemeSettingsData settings)
        {
            if (theme == null || SelectedItem != theme)
            {
                return;
            }

            // A theme file has no variants: wearing it is all there is.
            if (theme.File != null)
            {
                _themeService.SetTheme(theme.File, true, XamlRoot);
                return;
            }

            // Picking a built-in wears the variant it was last worn with.
            if (settings == null)
            {
                var last = AppSettings.Appearance.GetLastVariant(theme.Type);

                settings = theme.Items.FirstOrDefault(x => x.Name == last) ?? theme.Items.FirstOrDefault();

                if (settings == null)
                {
                    return;
                }

                theme.Select(settings);
            }

            AppSettings.Appearance.WearVariant(theme.Type, settings.Name, settings, true);
            NightModeService.Current.Show(theme.RequestedTheme, XamlRoot);

            ThemeData.SendDefaultBackground(ClientService, settings.Background, theme.RequestedTheme == TelegramTheme.Dark);
        }

        public RangeObservableCollection<ThemeInfoBase> Items { get; private set; }
        public RangeObservableCollection<ThemeInfoBase> Custom { get; private set; }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            // Before the first await, so leaving the page while the built-ins load cannot run
            // OnNavigatedFrom ahead of it and leave the window holding this.
            if (!_darkOnly && Window != null)
            {
                Window.ChatThemeChanged += OnChatThemeChanged;
            }

            if (!_darkOnly && _builtIns == null)
            {
                _builtIns = await Task.WhenAll(
                    ThemeData.CreateAsync(this, TelegramThemeType.Classic),
                    ThemeData.CreateAsync(this, TelegramThemeType.Day),
                    ThemeData.CreateAsync(this, TelegramThemeType.Tinted),
                    ThemeData.CreateAsync(this, TelegramThemeType.Night));
            }

            await RefreshThemesAsync();
        }

        public async Task SetThemeAsync(ThemeInfoBase info)
        {
            _themeService.SetTheme(info, !_darkOnly, XamlRoot);
            await RefreshThemesAsync();
        }

        private async Task RefreshThemesAsync()
        {
            if (_darkOnly)
            {
                Items.ReplaceWith(_themeService.GetThemes().Where(x => x.Parent == TelegramTheme.Dark));
                Custom.ReplaceWith((await _themeService.GetCustomThemesAsync()).Where(x => x.Parent == TelegramTheme.Dark));
            }
            else
            {
                Items.ReplaceWith(_themeService.GetThemes());
                Custom.ReplaceWith(await _themeService.GetCustomThemesAsync());

                Themes.ReplaceWith(_builtIns.Concat(Custom.OfType<ThemeCustomInfo>().Select(x => new ThemeData(this, x))));

                // The field, not the property: reflecting what is worn must not wear anything.
                _selectedItem = FindWorn();
                RaisePropertyChanged(nameof(SelectedItem));
            }

            AreCustomThemesAvailable = Custom.Count > 0;
        }

        protected override void OnNavigatedFrom(NavigationState suspensionState, bool suspending)
        {
            if (!_darkOnly && Window != null)
            {
                Window.ChatThemeChanged -= OnChatThemeChanged;
            }
        }

        // The editor saves the variant and wears it before showing the change, and showing it is
        // what raises this: the moment a variant's stored copy can be new.
        private void OnChatThemeChanged(object sender, EventArgs e)
        {
            if (_builtIns == null)
            {
                return;
            }

            RefreshWorn(TelegramTheme.Light);
            RefreshWorn(TelegramTheme.Dark);
        }

        private void RefreshWorn(TelegramTheme requested)
        {
            var worn = AppSettings.Appearance.GetWorn(requested);
            if (worn.Kind != ThemeKind.Variant)
            {
                return;
            }

            foreach (var theme in _builtIns)
            {
                if (theme.Type == worn.Type)
                {
                    _ = theme.RefreshAsync(worn.Id);
                    break;
                }
            }
        }

        /// <summary>
        /// "+": the editor on a copy of the selected variant. The variant itself is created only if
        /// the editor saves.
        /// </summary>
        public void CreateVariant()
        {
            if (SelectedItem is not ThemeData theme || theme.File != null)
            {
                return;
            }

            var source = theme.SelectedAccent ?? theme.Items.FirstOrDefault();
            if (source == null)
            {
                return;
            }

            NavigationService.ShowPopup(new BackgroundPopup(NavigationService, true), new BackgroundParameters(theme.Type, null, ThemeSettingsStore.Copy(source)));
        }

        public async void ResetVariant(ThemeSettingsData item)
        {
            if (SelectedItem is not ThemeData theme || !theme.CanReset(item))
            {
                return;
            }

            var confirm = await ShowPopupAsync(Strings.ResetThemeAlert, Strings.ChatResetTheme, Strings.Reset, Strings.Cancel, destructive: true);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var worn = AppSettings.Appearance.GetWorn(theme.RequestedTheme) == ThemeIdentity.Variant(theme.Type, item.Name);

            AppSettings.Appearance.ResetVariant(theme.Type, item.Name, theme.GetSeed(item.Name));
            await theme.RefreshAsync(item.Name);

            if (worn)
            {
                ShowWorn(theme);
            }
        }

        public async void DeleteVariant(ThemeSettingsData item)
        {
            if (SelectedItem is not ThemeData theme || !theme.CanDelete(item))
            {
                return;
            }

            var confirm = await ShowPopupAsync(Strings.DeleteThemeAlert, Strings.DeleteThemeTitle, Strings.Delete, Strings.Cancel, destructive: true);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var changed = AppSettings.Appearance.DeleteVariant(theme.Type, item.Name);
            theme.Remove(item);

            // Its base fell back to the built-in's 🏠 variant.
            if (changed)
            {
                ShowWorn(theme);
            }
        }

        // A change to what a base wears that is not a switch: redrawn if on screen, and its
        // background handed to TDLib either way, since TDLib keeps one per base.
        private void ShowWorn(ThemeData theme)
        {
            if (NightModeService.Current.GetCalculatedTelegramTheme() == theme.RequestedTheme)
            {
                NightModeService.Current.Update(updateBackground: false);
            }

            var id = AppSettings.Appearance.GetWorn(theme.RequestedTheme).Id;
            var card = theme.Items.FirstOrDefault(x => x.Name == id);

            if (card != null)
            {
                ThemeData.SendDefaultBackground(ClientService, card.Background, theme.RequestedTheme == TelegramTheme.Dark);
            }
        }

        private ThemeData FindWorn()
        {
            var requested = NightModeService.Current.GetCalculatedTelegramTheme();
            var worn = AppSettings.Appearance.GetWorn(requested);

            if (worn.Kind == ThemeKind.File)
            {
                return Themes.FirstOrDefault(x => x.File != null && AppearanceSettings.GetThemeFileId(x.File.Path) == worn.Id);
            }

            var type = AppSettings.Appearance.GetBuiltIn(requested);
            return Themes.FirstOrDefault(x => x.File == null && x.Type == type);
        }

        public NightMode NightMode => AppSettings.Appearance.NightMode;

        private bool _areCustomThemesAvailable;
        public bool AreCustomThemesAvailable
        {
            get => _areCustomThemesAvailable;
            set => Set(ref _areCustomThemesAvailable, value);
        }



        public void NewTheme()
        {
            // Items holds the built-ins only, so this is the first one of the base on screen.
            var requested = NightModeService.Current.GetCalculatedTelegramTheme();
            var existing = Items.FirstOrDefault(x => x.Parent == requested);

            if (existing != null)
            {
                CreateTheme(existing);
            }
        }

        #region Themes

        public async void CreateTheme(ThemeInfoBase theme)
        {
            if (theme != null)
            {
                // A built-in copies what picking it would wear; bundled Classic has nothing to copy.
                ThemeSettings settings = null;
                if (theme is ThemeAccentInfo { Type: not TelegramThemeType.Custom } accent)
                {
                    settings = AppSettings.Appearance.Variants.Get(accent.Type, AppSettings.Appearance.GetLastVariant(accent.Type))
                        ?? AppearanceSettings.GetHouse(accent.Type);
                }

                await _themeService.CreateThemeAsync(NavigationService, theme, settings);
                await RefreshThemesAsync();
            }
        }

        public async void ShareTheme(ThemeCustomInfo theme)
        {
            await ShowPopupAsync(new ChooseChatsPopup(), new ChooseChatsConfigurationPostMessage(new InputMessageDocument(new InputDocument(new InputFileLocal(theme.Path), null, false), null)));
        }

        public async void EditTheme(ThemeCustomInfo theme)
        {
            await SetThemeAsync(theme);

            //NavigationService.Navigate(typeof(SettingsThemePage), theme.Path);
            if (XamlRoot.TryGetContent(out Views.Host.RootWindow root))
            {
                root.ShowEditor(theme);
            }
        }

        public async void DeleteTheme(ThemeCustomInfo theme)
        {
            var confirm = await ShowPopupAsync(Strings.DeleteThemeAlert, Strings.DeleteThemeTitle, Strings.Delete, Strings.Cancel, destructive: true);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            try
            {
                var file = await StorageFile.GetFileFromPathAsync(theme.Path);
                await file.DeleteAsync();
            }
            catch { }

            // Its base fell back to 🏠.
            if (AppSettings.Appearance.DeleteThemeFile(theme.Path))
            {
                if (NightModeService.Current.GetCalculatedTelegramTheme() == theme.Parent)
                {
                    NightModeService.Current.Update(updateBackground: false);
                }

                _themeService.SendDefaultBackground(theme.Parent);
            }

            await RefreshThemesAsync();
        }

        #endregion
    }

    public class ThemeData : BindableBase
    {
        private readonly SettingsThemesViewModel _viewModel;

        public IClientService ClientService => _viewModel.ClientService;

        private ThemeData(SettingsThemesViewModel viewModel, TelegramThemeType type, List<ThemeSettingsData> items)
        {
            _viewModel = viewModel;

            Type = type;
            Info = ThemeAccentInfo.IsAccent(type) ? ThemeAccentInfo.FromSettings(RequestedTheme, AppearanceSettings.GetHouse(type)) : new ThemeBundledInfo { Name = Strings.ThemeClassic, Parent = TelegramTheme.Light };
            Items = new RangeObservableCollection<ThemeSettingsData>(items);
        }

        public ThemeData(SettingsThemesViewModel viewModel, ThemeCustomInfo file)
        {
            _viewModel = viewModel;

            Type = TelegramThemeType.Custom;
            Info = file;
            Items = new RangeObservableCollection<ThemeSettingsData>();
        }

        /// <summary>
        /// The theme file this stands for, or null for a built-in.
        /// </summary>
        public ThemeCustomInfo File => Info as ThemeCustomInfo;

        public bool HasVariants => File == null;

        public TelegramThemeType Type { get; }

        public TelegramTheme RequestedTheme => File?.Parent ?? AppearanceSettings.GetBase(Type);

        public ThemeInfoBase Info { get; }

        public RangeObservableCollection<ThemeSettingsData> Items { get; }

        private ThemeSettingsData _selectedAccent;
        public ThemeSettingsData SelectedAccent
        {
            get => _selectedAccent;
            set
            {
                if (value != null)
                {
                    if (Set(ref _selectedAccent, value))
                    {
                        _viewModel.UpdateTheme(this, value);
                    }
                }
                else
                {
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>
        /// Moves the selection without wearing anything, for when the caller already is.
        /// </summary>
        public void Select(ThemeSettingsData value)
        {
            _selectedAccent = value;
            RaisePropertyChanged(nameof(SelectedAccent));
        }

        // What each seeded variant starts as, and what Reset puts back.
        private Dictionary<string, ThemeSettings> _seeds;

        // The stored copy each card was drawn from: the store replaces the instance on every save,
        // so a different one is a change, and an unchanged card is never reloaded.
        private Dictionary<string, ThemeSettings> _sources;

        /// <summary>
        /// Whether a seeded variant was changed. Having a stored copy is not enough: wearing one
        /// stores it, because a window is themed before TDLib connects.
        /// </summary>
        public bool CanReset(ThemeSettingsData item)
        {
            var stored = AppSettings.Appearance.Variants.Get(Type, item.Name);
            return stored != null && GetSeed(item.Name) is ThemeSettings seed && !IsClean(stored, seed);
        }

        // Against what the store keeps, not what a card holds: BaseTheme is the variant's own by
        // construction, the background is only its link, and the bundled one is stored as none.
        private static bool IsClean(ThemeSettings stored, ThemeSettings seed)
        {
            return stored.AccentColor == seed.AccentColor
                && stored.HasOutgoingMessageAccentColor == seed.HasOutgoingMessageAccentColor
                && (!stored.HasOutgoingMessageAccentColor || stored.OutgoingMessageAccentColor == seed.OutgoingMessageAccentColor)
                && stored.AnimateOutgoingMessageFill == seed.AnimateOutgoingMessageFill
                && stored.OutgoingMessageFill.AreTheSame(seed.OutgoingMessageFill)
                && GetLink(stored.Background) == GetLink(seed.Background);
        }

        private static string GetLink(Background background)
        {
            return background == null || ThemeSettingsStore.IsBundledBackground(background)
                ? null
                : TdBackground.GetBackgroundLink(background.Name, background.Type);
        }

        public bool CanDelete(ThemeSettingsData item)
        {
            return ThemeVariantStore.IsCreated(item.Name);
        }

        public ThemeSettings GetSeed(string id)
        {
            return _seeds != null && _seeds.TryGetValue(id, out ThemeSettings seed) ? seed : null;
        }

        /// <summary>
        /// Redraws one variant's card if its stored copy changed, adding it if it is new.
        /// </summary>
        public async Task RefreshAsync(string id)
        {
            if (_sources == null)
            {
                return;
            }

            var stored = AppSettings.Appearance.Variants.Get(Type, id);
            if (_sources.TryGetValue(id, out ThemeSettings source) && source == stored)
            {
                return;
            }

            _sources[id] = stored;

            var data = await LoadVariant(_viewModel.ClientService, Type, ThemeSettingsStore.ToBuiltInTheme(Type), id, GetSeed(id));
            if (data == null)
            {
                return;
            }

            var index = IndexOf(id);
            if (index < 0)
            {
                Items.Add(data);
            }
            else
            {
                var selected = Items[index] == _selectedAccent;
                Items[index] = data;

                if (!selected)
                {
                    return;
                }
            }

            // The selection follows the card it was on, or moves to a new one being worn.
            if (index >= 0 || AppSettings.Appearance.GetLastVariant(Type) == id)
            {
                Select(data);
            }
        }

        public void Remove(ThemeSettingsData item)
        {
            _sources?.Remove(item.Name);
            Items.Remove(item);

            if (item == _selectedAccent)
            {
                var variant = AppSettings.Appearance.GetLastVariant(Type);
                Select(Items.FirstOrDefault(x => x.Name == variant) ?? Items.FirstOrDefault());
            }
        }

        private int IndexOf(string id)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i].Name == id)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// A built-in with all of its variants loaded, so nothing reads a list still filling up.
        /// </summary>
        public static async Task<ThemeData> CreateAsync(SettingsThemesViewModel viewModel, TelegramThemeType type)
        {
            var clientService = viewModel.ClientService;
            var baseTheme = ThemeSettingsStore.ToBuiltInTheme(type);
            var light = type is TelegramThemeType.Classic or TelegramThemeType.Day;

            // Seeded variants first, each falling back to what it was seeded from until it has a
            // copy of its own; then the ones made with "+", which always have one.
            var seeds = new Dictionary<string, ThemeSettings>
            {
                [DefaultThemeId] = AppearanceSettings.GetHouse(type)
            };

            var ids = new List<string> { DefaultThemeId };

            foreach (var theme in clientService.ChatThemes)
            {
                seeds[theme.Name] = light ? theme.LightSettings : theme.DarkSettings;
                ids.Add(theme.Name);
            }

            ids.AddRange(AppSettings.Appearance.Variants.GetCreated(type));

            // Side by side - each may wait on a background search - and kept in this order.
            var tasks = new Task<ThemeSettingsData>[ids.Count];
            var sources = new Dictionary<string, ThemeSettings>(ids.Count);

            for (int i = 0; i < ids.Count; i++)
            {
                seeds.TryGetValue(ids[i], out ThemeSettings seed);

                tasks[i] = LoadVariant(clientService, type, baseTheme, ids[i], seed);
                sources[ids[i]] = AppSettings.Appearance.Variants.Get(type, ids[i]);
            }

            var loaded = await Task.WhenAll(tasks);
            var data = new ThemeData(viewModel, type, loaded.Where(x => x != null).ToList())
            {
                _seeds = seeds,
                _sources = sources
            };

            // The one picking this built-in would wear, whether or not it is worn right now.
            var variant = AppSettings.Appearance.GetLastVariant(type);
            data._selectedAccent = data.Items.FirstOrDefault(x => x.Name == variant) ?? data.Items.FirstOrDefault();

            return data;

            //var backgrounds = await clientService.SendAsync(new GetInstalledBackgrounds(dark)) as Backgrounds;
            //var template = backgrounds.BackgroundsValue.FirstOrDefault(x => x.Type is BackgroundTypeFill { Fill: BackgroundFillFreeformGradient });

            //foreach (var accent in _defaultAccents[type])
            //{
            //    var color = accent.ToValue();
            //    var settings = new ThemeSettings(baseTheme, color, null, null, false, false, color);
            //    var baseColor = ColorEx.FromHex(dark ? 0xFF6C7FA6 : 0xFFDBDDBB);

            //    var colorizer = ThemeColorizer.FromTheme(type, baseColor, accent);
            //    var gradient1 = colorizer.Colorize(ColorEx.FromHex(dark ? 0xFF6C7FA6 : 0xFFDBDDBB)).ToValue();
            //    var gradient2 = colorizer.Colorize(ColorEx.FromHex(dark ? 0xFF2E344B : 0xFF6BA587)).ToValue();
            //    var gradient3 = colorizer.Colorize(ColorEx.FromHex(dark ? 0xFF7874A7 : 0xFFD5D88D)).ToValue();
            //    var gradient4 = colorizer.Colorize(ColorEx.FromHex(dark ? 0xFF333258 : 0xFF88B884)).ToValue();

            //    var freeform = new[] { gradient1, gradient2, gradient3, gradient4 };
            //    //settings.Background = new Background(0, true, dark, string.Empty,
            //    //    new Document(string.Empty, "application/x-tgwallpattern", null, null, TdExtensions.GetLocalFile("Assets\\Background.tgv", "Background")),
            //    //    new BackgroundTypePattern(new BackgroundFillFreeformGradient(freeform), dark ? 100 : 50, dark, false));
            //    settings.Background = new Background(0, true, dark, string.Empty, null, new BackgroundTypeFill(new BackgroundFillFreeformGradient(freeform)));

            //    Items.Add(settings);
            //}
        }

        public const string DefaultThemeId = "\U0001F3E0";
        public const string CustomThemeId = "\U0001F3A8";

        private static Task<ThemeSettingsData> LoadVariant(IClientService clientService, TelegramThemeType type, BuiltInTheme baseTheme, string id, ThemeSettings seed)
        {
            return LoadThemeSettings(clientService, baseTheme, id, AppSettings.Appearance.Variants.Get(type, id), seed);
        }

        /// <summary>
        /// Hands TDLib the background of a theme being switched to. <paramref name="background"/>
        /// must come through <see cref="LoadThemeSettings(IClientService, BuiltInTheme, string, ThemeSettings, ThemeSettings)"/>:
        /// a stored one has no document.
        /// </summary>
        /// <remarks>
        /// No pending marker: the background is already the theme's own, so the update it produces
        /// has nothing to write.
        /// </remarks>
        public static void SendDefaultBackground(IClientService clientService, Background background, bool forDarkTheme)
        {
            if (background != null && background.Id != 0)
            {
                var input = background.Document != null
                    ? new InputBackgroundRemote(background.Id)
                    : null;

                clientService.Send(new SetDefaultBackground(input, background.Type, forDarkTheme));
            }
            else
            {
                clientService.Send(new DeleteDefaultBackground(forDarkTheme));
            }
        }

        /// <summary>
        /// Stored settings made drawable: the store keeps a wallpaper or pattern as its link alone,
        /// so the document is searched for unless <paramref name="target"/> already has it.
        /// </summary>
        public static async Task<ThemeSettingsData> LoadThemeSettings(IClientService clientService, BuiltInTheme baseTheme, string name, ThemeSettings settings, ThemeSettings target)
        {
            var source = settings ?? target;
            if (source == null)
            {
                return null;
            }

            var background = source.Background;

            // Only a stored one needs resolving: a target came from TDLib with its document.
            if (settings != null && background is { Type: BackgroundTypeWallpaper or BackgroundTypePattern })
            {
                if (background.AreTheSame(target?.Background))
                {
                    background = target.Background;
                }
                else if (await clientService.SendAsync(new SearchBackground(background.Name)) is Background resolved)
                {
                    // A theme file's header keeps only the link, so its id is the search's: without one
                    // the background could only ever be sent as a delete.
                    var id = background.Id != 0 ? background.Id : resolved.Id;
                    background = new Background(id, background.IsDefault, background.IsDark, background.Name, resolved.Document, background.Type);
                }
            }

            // Never null, so every card can be drawn and switched to. Only on the copy this returns:
            // a stored copy without a background is what lets the next updateDefaultBackground be
            // adopted, so the default must not reach it.
            background ??= AppearanceSettings.GetDefaultBackground(baseTheme is BuiltInThemeNight or BuiltInThemeTinted);

            return new ThemeSettingsData(name, baseTheme, source.AccentColor, background, source.OutgoingMessageFill, source.AnimateOutgoingMessageFill, source.HasOutgoingMessageAccentColor, source.OutgoingMessageAccentColor);
        }
    }

    public class ThemeSettingsData : ThemeSettings
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Describes theme settings
        /// </summary>
        /// <param name="baseTheme">Base theme for this theme</param>
        /// <param name="accentColor">Theme accent color in ARGB format</param>
        /// <param name="background">The background to be used in chats; may be null</param>
        /// <param name="outgoingMessageFill">The fill to be used as a background for outgoing messages; may be null if the fill from the base theme must be used instead</param>
        /// <param name="animateOutgoingMessageFill">If true, the freeform gradient fill needs to be animated on every sent message</param>
        /// <param name="hasOutgoingMessageAccentColor">True, if there is chosen accent color for outgoing messages; otherwise, the color must be deduced from other theme settings</param>
        /// <param name="outgoingMessageAccentColor">Accent color of outgoing messages in ARGB format; must be ignored if has_outgoing_message_accent_color is false</param>
        public ThemeSettingsData(string name, BuiltInTheme baseTheme, int accentColor, Background? background, BackgroundFill? outgoingMessageFill, bool animateOutgoingMessageFill, bool hasOutgoingMessageAccentColor, int outgoingMessageAccentColor)
        {
            Name = name;
            BaseTheme = baseTheme;
            AccentColor = accentColor;
            Background = background;
            OutgoingMessageFill = outgoingMessageFill;
            AnimateOutgoingMessageFill = animateOutgoingMessageFill;
            HasOutgoingMessageAccentColor = hasOutgoingMessageAccentColor;
            OutgoingMessageAccentColor = outgoingMessageAccentColor;
        }
    }
}
