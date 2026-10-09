//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services.Settings;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels.Settings;
using Telegram.Views.Popups;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Services
{
    public interface IThemeService
    {
        IList<ThemeInfoBase> GetThemes();
        Task<IList<ThemeInfoBase>> GetCustomThemesAsync();

        Task SerializeAsync(StorageFile file, ThemeCustomInfo theme);

        Task InstallThemeAsync(StorageFile file, XamlRoot xamlRoot);
        void SetTheme(ThemeInfoBase info, bool apply, XamlRoot xamlRoot);
        void SendDefaultBackground(TelegramTheme requested);

        Task CreateThemeAsync(INavigationService navigation, ThemeInfoBase theme, ThemeSettings settings = null);
    }

    public partial class ThemeService : IThemeService
    {
        private readonly IClientService _clientService;
        private readonly ISettingsService _settingsService;
        private readonly IEventAggregator _aggregator;

        public ThemeService(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
        {
            _clientService = clientService;
            _settingsService = settingsService;
            _aggregator = aggregator;
        }

        public static ThemeLookup GetLookup(ElementTheme flags)
        {
            return flags == ElementTheme.Dark ? ThemeDefaults.Dark : ThemeDefaults.Light;
        }

        public static ThemeLookup GetLookup(TelegramTheme flags)
        {
            return flags == TelegramTheme.Dark ? ThemeDefaults.Dark : ThemeDefaults.Light;
        }

        public IList<ThemeInfoBase> GetThemes()
        {
            var result = new List<ThemeInfoBase>
            {
                new ThemeBundledInfo { Name = Strings.ThemeClassic, Parent = TelegramTheme.Light },
                ThemeAccentInfo.FromSettings(TelegramTheme.Light, AppearanceSettings.GetHouse(TelegramThemeType.Day)),
                ThemeAccentInfo.FromSettings(TelegramTheme.Dark, AppearanceSettings.GetHouse(TelegramThemeType.Tinted)),
                ThemeAccentInfo.FromSettings(TelegramTheme.Dark, AppearanceSettings.GetHouse(TelegramThemeType.Night))
            };

            return result;
        }

        public async Task<IList<ThemeInfoBase>> GetCustomThemesAsync()
        {
            var result = new List<ThemeInfoBase>();

            try
            {
                var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("themes", CreationCollisionOption.OpenIfExists);
                var files = await folder.GetFilesAsync();

                foreach (var file in files)
                {
                    // A file that is not a theme is skipped, not listed as one.
                    if (await ThemeCustomInfo.FromFileAsync(_clientService, file) is ThemeCustomInfo theme)
                    {
                        result.Add(theme);
                    }
                }
            }
            catch
            {
                // GetFilesAsync seems to throw at times
            }

            return result;
        }

        public async Task SerializeAsync(StorageFile file, ThemeCustomInfo theme)
        {
            var lines = new StringBuilder();
            lines.AppendLine("!");
            lines.AppendLine($"name: {theme.Name}");
            lines.AppendLine($"parent: {(int)theme.Parent}");

            Dictionary<string, Color> colorized = null;

            if (theme.Settings is ThemeSettings settings)
            {
                var type = ThemeSettingsStore.ToThemeType(settings.BaseTheme, theme.Parent);

                lines.AppendLine($"base: {type.ToString().ToLowerInvariant()}");
                lines.AppendLine($"accent: #FF{settings.AccentColor:X6}");

                if (settings.HasOutgoingMessageAccentColor)
                {
                    lines.AppendLine($"outgoing: #FF{settings.OutgoingMessageAccentColor:X6}");
                }

                if (settings.OutgoingMessageFill != null)
                {
                    lines.AppendLine($"fill: {TdBackground.ToString(settings.OutgoingMessageFill)}");
                }

                if (settings.AnimateOutgoingMessageFill)
                {
                    lines.AppendLine("animate: 1");
                }

                if (settings.Background != null && !ThemeSettingsStore.IsBundledBackground(settings.Background))
                {
                    var link = TdBackground.GetBackgroundLink(settings.Background.Name, settings.Background.Type);
                    if (link != null)
                    {
                        lines.AppendLine($"background: {link}");
                    }
                }

                // Values holds what the theme looks like; the file keeps only what the header does
                // not already produce, so a later change to the colorizer still reaches the rest.
                colorized = ThemeAccentInfo.FromSettings(theme.Parent, settings).Values;
            }
            else if (theme.AccentColor != default)
            {
                var accent = (theme.AccentColor.A << 24) + (theme.AccentColor.R << 16) + (theme.AccentColor.G << 8) + theme.AccentColor.B;
                lines.AppendLine(string.Format("accent: #{0:X8}", accent));
            }

            var lastbrush = false;

            foreach (var item in theme.Values)
            {
                if (colorized != null && colorized.TryGetValue(item.Key, out Color produced) && produced == item.Value)
                {
                    continue;
                }

                if (item.Value is Color color)
                {
                    if (!lastbrush)
                    {
                        lines.AppendLine("#");
                    }

                    var hexValue = (color.A << 24) + (color.R << 16) + (color.G << 8) + (color.B & 0xff);

                    lastbrush = true;
                    lines.AppendLine(string.Format("{0}: #{1:X8}", item.Key, hexValue));
                }
            }

            await FileIO.WriteTextAsync(file, lines.ToString());
        }



        public async Task InstallThemeAsync(StorageFile file, XamlRoot xamlRoot)
        {
            var info = await ThemeCustomInfo.FromFileAsync(_clientService, file);
            if (info == null)
            {
                return;
            }

            var installed = await GetCustomThemesAsync();

            var equals = installed.FirstOrDefault(x => x is ThemeCustomInfo custom && ThemeCustomInfo.Equals(custom, info));
            if (equals != null)
            {
                SetTheme(equals, true, xamlRoot);
                return;
            }

            var folder = await ApplicationData.Current.LocalFolder.GetFolderAsync("themes");
            var result = await file.CopyAsync(folder, file.Name, NameCollisionOption.GenerateUniqueName);

            var theme = await ThemeCustomInfo.FromFileAsync(_clientService, result);
            if (theme != null)
            {
                SetTheme(theme, true, xamlRoot);
            }
        }

        /// <summary>
        /// Wears a theme file or a built-in. With <paramref name="apply"/> the app switches to its
        /// base if that is not the one on screen; without, as on the night mode page, the base is
        /// only configured.
        /// </summary>
        public void SetTheme(ThemeInfoBase info, bool apply, XamlRoot xamlRoot)
        {
            if (info is ThemeCustomInfo custom)
            {
                // A theme without a background keeps the wallpaper on screen, which is also what
                // its preview showed, instead of resetting it to the bundled one.
                AppSettings.Appearance.WearThemeFile(info.Parent, custom.Path, apply, _clientService.GetDefaultBackground(info.Parent == TelegramTheme.Dark));
            }
            else if (info is ThemeAccentInfo accent)
            {
                AppSettings.Appearance.WearBuiltIn(accent.Type, apply);
            }
            else
            {
                AppSettings.Appearance.WearBuiltIn(info.Parent == TelegramTheme.Light ? TelegramThemeType.Classic : TelegramThemeType.Night, apply);
            }

            if (apply)
            {
                NightModeService.Current.Show(info.Parent, xamlRoot);
            }
            else if (NightModeService.Current.GetCalculatedTelegramTheme() == info.Parent)
            {
                NightModeService.Current.Update();
            }

            SendDefaultBackground(info.Parent);
        }

        // The worn theme's own background, resolved first: a stored one is only a link, and TDLib
        // takes a pattern or a wallpaper by id.
        public async void SendDefaultBackground(TelegramTheme requested)
        {
            var identity = AppSettings.Appearance.GetWorn(requested);
            AppSettings.Appearance.TryGetBackground(requested, identity, out Background background);

            var house = AppearanceSettings.GetHouse(requested);
            var resolved = await ThemeData.LoadThemeSettings(_clientService, house.BaseTheme, identity.Id, ThemeSettingsStore.WithBackground(house, background), null);

            ThemeData.SendDefaultBackground(_clientService, resolved.Background, requested == TelegramTheme.Dark);
        }

        /// <summary>
        /// Writes a new theme file from <paramref name="theme"/>, and opens the editor on it. With
        /// <paramref name="settings"/> - or a v2 file to copy - it is a v2 file: those settings as
        /// its header and nothing else, so it loses nothing of the theme it came from.
        /// </summary>
        public async Task CreateThemeAsync(INavigationService navigation, ThemeInfoBase theme, ThemeSettings settings = null)
        {
            var confirm = await navigation.ShowPopupAsync(Strings.CreateNewThemeAlert, Strings.NewTheme, Strings.CreateTheme, Strings.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var input = new InputPopup();
            input.Title = Strings.NewTheme;
            input.Header = Strings.EnterThemeName;
            input.Text = $"{theme.Name} #2";
            input.IsPrimaryButtonEnabled = true;
            input.IsSecondaryButtonEnabled = true;
            input.PrimaryButtonText = Strings.OK;
            input.SecondaryButtonText = Strings.Cancel;

            confirm = await navigation.ShowPopupAsync(input);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var preparing = new ThemeCustomInfo(theme.Parent, theme.AccentColor, input.Text);
            var fileName = Client.Execute(new CleanFileName(theme.Name)) as Text;

            settings ??= (theme as ThemeCustomInfo)?.Settings;

            if (settings != null)
            {
                // Plain settings: a CustomThemeSettings would carry the old file's path along.
                preparing.Settings = new ThemeSettings(settings.BaseTheme, settings.AccentColor, settings.Background, settings.OutgoingMessageFill, settings.AnimateOutgoingMessageFill, settings.HasOutgoingMessageAccentColor, settings.OutgoingMessageAccentColor);

                // What the editor opens on and the window shows; only the overrides reach the file.
                var colorized = ThemeAccentInfo.FromSettings(theme.Parent, preparing.Settings);

                foreach (var item in colorized.Values)
                {
                    preparing.Values[item.Key] = item.Value;
                }

                foreach (var item in colorized.Shades)
                {
                    preparing.Shades[item.Key] = item.Value;
                }

                if (theme is ThemeCustomInfo source)
                {
                    foreach (var item in source.Values)
                    {
                        preparing.Values[item.Key] = item.Value;
                    }
                }
            }
            else
            {
                var lookup = GetLookup(theme.Parent);

                foreach (var value in lookup)
                {
                    if (value.Value.Kind == ThemeValueKind.Color)
                    {
                        preparing.Values[value.Key] = value.Value.Color;
                    }
                }

                if (theme is ThemeCustomInfo custom)
                {
                    foreach (var item in custom.Values)
                    {
                        preparing.Values[item.Key] = item.Value;
                    }
                }
                else if (theme is ThemeAccentInfo accent)
                {
                    foreach (var item in accent.Values)
                    {
                        preparing.Values[item.Key] = item.Value;
                    }
                }
            }

            var file = await ApplicationData.Current.LocalFolder.CreateFileAsync("themes\\" + fileName.TextValue + ".unigram-theme", CreationCollisionOption.GenerateUniqueName);
            await SerializeAsync(file, preparing);

            preparing.Path = file.Path;

            SetTheme(preparing, true, navigation.XamlRoot);

            if (navigation.XamlRoot.Content is WindowPresenter { Content: Views.Host.RootWindow root })
            {
                root.ShowEditor(preparing);
            }
        }
    }
}
