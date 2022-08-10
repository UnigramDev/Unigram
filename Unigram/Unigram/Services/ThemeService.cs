using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services.Settings;
using Unigram.Views.Popups;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Services
{
    public interface IThemeService
    {
        IList<ThemeInfoBase> GetThemes();
        Task<IList<ThemeInfoBase>> GetCustomThemesAsync();

        Task SerializeAsync(StorageFile file, ThemeCustomInfo theme);
        Task<ThemeCustomInfo> DeserializeAsync(StorageFile file);

        Task InstallThemeAsync(StorageFile file);
        void SetTheme(ThemeInfoBase info, bool apply);

        Task CreateThemeAsync(ThemeInfoBase theme);
    }

    public partial class ThemeService : IThemeService
    {
        private readonly IProtoService _protoService;
        private readonly ISettingsService _settingsService;
        private readonly IEventAggregator _aggregator;

        public ThemeService(IProtoService protoService, ISettingsService settingsService, IEventAggregator aggregator)
        {
            _protoService = protoService;
            _settingsService = settingsService;
            _aggregator = aggregator;
        }

        public static Dictionary<string, object> GetLookup(TelegramTheme flags)
        {
            return flags == TelegramTheme.Dark ? _defaultDark : _defaultLight;
        }

        public IList<ThemeInfoBase> GetThemes()
        {
            var result = new List<ThemeInfoBase>();
            result.Add(new ThemeBundledInfo { Name = Strings.Resources.ThemeClassic, Parent = TelegramTheme.Light });
            result.Add(ThemeAccentInfo.FromAccent(TelegramThemeType.Day, _settingsService.Appearance.Accents[TelegramThemeType.Day]));
            result.Add(ThemeAccentInfo.FromAccent(TelegramThemeType.Tinted, _settingsService.Appearance.Accents[TelegramThemeType.Tinted]));
            result.Add(ThemeAccentInfo.FromAccent(TelegramThemeType.Night, _settingsService.Appearance.Accents[TelegramThemeType.Night]));

            return result;
        }

        public async Task<IList<ThemeInfoBase>> GetCustomThemesAsync()
        {
            var result = new List<ThemeInfoBase>();

            var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("themes", CreationCollisionOption.OpenIfExists);
            var files = await folder.GetFilesAsync();

            foreach (var file in files)
            {
                try
                {
                    result.Add(await DeserializeAsync(file));
                }
                catch { }
            }

            return result;
        }

        public async Task SerializeAsync(StorageFile file, ThemeCustomInfo theme)
        {
            var lines = new StringBuilder();
            lines.AppendLine("!");
            lines.AppendLine($"name: {theme.Name}");
            lines.AppendLine($"parent: {(int)theme.Parent}");

            var lastbrush = false;

            foreach (var item in theme.Values)
            {
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

        public async Task<ThemeCustomInfo> DeserializeAsync(StorageFile file)
        {
            var lines = await FileIO.ReadLinesAsync(file);
            var theme = ThemeCustomInfo.FromFile(file.Path, lines);

            return theme;
        }



        public async Task InstallThemeAsync(StorageFile file)
        {
            var info = await DeserializeAsync(file);
            if (info == null)
            {
                return;
            }

            var installed = await GetCustomThemesAsync();

            var equals = installed.FirstOrDefault(x => x is ThemeCustomInfo custom && ThemeCustomInfo.Equals(custom, info));
            if (equals != null)
            {
                SetTheme(equals, true);
                return;
            }

            var folder = await ApplicationData.Current.LocalFolder.GetFolderAsync("themes");
            var result = await file.CopyAsync(folder, file.Name, NameCollisionOption.GenerateUniqueName);

            var theme = await DeserializeAsync(result);
            if (theme != null)
            {
                SetTheme(theme, true);
            }
        }

        public void SetTheme(ThemeInfoBase info, bool apply)
        {
            if (apply)
            {
                _settingsService.Appearance.RequestedTheme = info.Parent;
            }

            if (info is ThemeCustomInfo custom)
            {
                _settingsService.Appearance[info.Parent].Type = TelegramThemeType.Custom;
                _settingsService.Appearance[info.Parent].Custom = custom.Path;
            }
            else if (info is ThemeAccentInfo accent)
            {
                _settingsService.Appearance[info.Parent].Type = accent.Type;
                _settingsService.Appearance.Accents[accent.Type] = accent.AccentColor;
            }
            else
            {
                _settingsService.Appearance[info.Parent].Type = info.Parent == TelegramTheme.Light ? TelegramThemeType.Classic : TelegramThemeType.Night;
            }

            var flags = _settingsService.Appearance.GetCalculatedElementTheme();
            var theme = flags == ElementTheme.Dark ? TelegramTheme.Dark : TelegramTheme.Light;

            if (theme != info.Parent && !apply)
            {
                return;
            }

            _settingsService.Appearance.UpdateNightMode();
        }

        public async Task CreateThemeAsync(ThemeInfoBase theme)
        {
            var confirm = await MessagePopup.ShowAsync(Strings.Resources.CreateNewThemeAlert, Strings.Resources.NewTheme, Strings.Resources.CreateTheme, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var input = new InputPopup();
            input.Title = Strings.Resources.NewTheme;
            input.Header = Strings.Resources.EnterThemeName;
            input.Text = $"{theme.Name} #2";
            input.IsPrimaryButtonEnabled = true;
            input.IsSecondaryButtonEnabled = true;
            input.PrimaryButtonText = Strings.Resources.OK;
            input.SecondaryButtonText = Strings.Resources.Cancel;

            confirm = await input.ShowQueuedAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var preparing = new ThemeCustomInfo(theme.Parent, theme.AccentColor, input.Text);
            var fileName = Client.Execute(new CleanFileName(theme.Name)) as Text;

            var lookup = ThemeService.GetLookup(theme.Parent);

            foreach (var value in lookup)
            {
                if (value.Value is Color color)
                {
                    preparing.Values[value.Key] = color;
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

            var file = await ApplicationData.Current.LocalFolder.CreateFileAsync("themes\\" + fileName.TextValue + ".unigram-theme", CreationCollisionOption.GenerateUniqueName);
            await SerializeAsync(file, preparing);

            preparing.Path = file.Path;

            SetTheme(preparing, true);

            if (Window.Current.Content is Views.Host.RootPage root)
            {
                root.ShowEditor(preparing);
            }
        }
    }
}
