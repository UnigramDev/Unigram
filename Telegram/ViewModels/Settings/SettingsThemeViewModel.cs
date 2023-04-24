//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Rg.DiffUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Views.Popups;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.ViewModels.Settings
{
    public class SettingsThemeViewModel : TLViewModelBase
    {
        private readonly IThemeService _themeService;

        private ThemeCustomInfo _theme;
        private ThemeBrush[] _index;
        private HashSet<string> _keys;

        private StorageFile _file;

        public SettingsThemeViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, IThemeService themeService)
            : base(clientService, settingsService, aggregator)
        {
            _themeService = themeService;

            Items = new DiffObservableCollection<ThemeBrush>(new ThemeBrushDiffHandler(), Constants.DiffOptions);

            EditTitleCommand = new RelayCommand(EditTitleExecute);
            EditBrushCommand = new RelayCommand<ThemeBrush>(EditBrushExecute);
        }

        private string _title;
        public string Title
        {
            get => _title;
            set => Set(ref _title, value);
        }

        private string _filter;
        public string Filter
        {
            get => _filter;
            set => SetFilter(value);
        }

        public void SetFilter(string value)
        {
            Set(ref _filter, value, nameof(Filter));
            Items.ReplaceDiff(_index.Where(x => x != null && x.Key.Contains(value, StringComparison.OrdinalIgnoreCase)));
        }

        public DiffObservableCollection<ThemeBrush> Items { get; }

        public void Initialize(ThemeCustomInfo theme)
        {
            _theme = theme;

            Title = theme.Name;
            Items.Clear();

            var incoming = theme.Parent == TelegramTheme.Light ? ThemeIncoming.Light : ThemeIncoming.Dark;
            var outgoing = theme.Parent == TelegramTheme.Light ? ThemeOutgoing.Light : ThemeOutgoing.Dark;

            var lookup = ThemeService.GetLookup(theme.Parent);
            var i = 0;

            _index = new ThemeBrush[lookup.Count + incoming.Count + outgoing.Count];
            _keys = new HashSet<string>(incoming.Count + outgoing.Count);

            ProcessDictionary(theme, incoming, "Incoming", ref i);
            ProcessDictionary(theme, outgoing, "Outgoing", ref i);

            foreach (var value in lookup)
            {
                if (_keys.Contains(value.Key))
                {
                    continue;
                }

                if (theme.Values.TryGetValue(value.Key, out Color custom))
                {
                    if (value.Value is Color reference)
                    {
                        _index[i] = new ThemeBrush(value.Key, custom, reference.A < 255);
                    }
                    else
                    {
                        _index[i] = new ThemeBrush(value.Key, custom, false);
                    }
                }
                else if (value.Value is Color color)
                {
                    _index[i] = new ThemeBrush(value.Key, color, color.A < 255);
                }

                if (_index[i] != null)
                {
                    Items.Add(_index[i++]);
                }
            }
        }

        private void ProcessDictionary(ThemeCustomInfo theme, Dictionary<string, (Color Color, SolidColorBrush)> lookup, string suffix, ref int i)
        {
            foreach (var value in lookup)
            {
                var key = value.Key;
                if (key.EndsWith("Brush"))
                {
                    key = value.Key.Substring(0, value.Key.Length - "Brush".Length) + suffix;
                }

                if (theme.Values.TryGetValue(key, out Color custom))
                {
                    _index[i] = new ThemeBrush(key, custom, value.Value.Color.A < 255);
                }
                else if (value.Value.Color is Color color)
                {
                    _index[i] = new ThemeBrush(key, color, value.Value.Color.A < 255);
                }

                if (_index[i] != null)
                {
                    _keys.Add(key);
                    Items.Add(_index[i++]);
                }
            }
        }

        public RelayCommand EditTitleCommand { get; }
        private async void EditTitleExecute()
        {
            var dialog = new InputPopup();
            dialog.Title = Strings.EditName;
            dialog.Text = _theme.Name;
            dialog.PrimaryButtonText = Strings.OK;
            dialog.SecondaryButtonText = Strings.Cancel;

            var confirm = await ShowPopupAsync(dialog);
            if (confirm == ContentDialogResult.Primary)
            {
                _theme.Name = dialog.Text;
                Title = dialog.Text;

                await CommitAsync();
            }

        }

        public RelayCommand<ThemeBrush> EditBrushCommand { get; }
        private async void EditBrushExecute(ThemeBrush brush)
        {
            var dialog = new ChooseColorPopup();
            dialog.IsTransparencyEnabled = brush.HasTransparency;
            dialog.IsAccentColorVisible = false;
            dialog.Title = brush.Key;
            dialog.Color = brush.Color;

            var confirm = await ShowPopupAsync(dialog);
            if (confirm == ContentDialogResult.Primary)
            {
                brush.Color = dialog.Color;

                _theme.Values[brush.Key] = dialog.Color;
                Theme.Current.Update(_theme);

                await CommitAsync();
            }
        }

        private async Task CommitAsync()
        {
            _file ??= await StorageFile.GetFileFromPathAsync(_theme.Path);
            await _themeService.SerializeAsync(_file, _theme);
        }
    }

    public class ThemeBrush : BindableBase
    {
        public ThemeBrush(string key, Color color, bool alpha)
        {
            if (key.EndsWith("Brush"))
            {
                Name = key.Substring(0, key.Length - "Brush".Length);
            }

            _color = color;

            Name ??= key;
            Key = key;
            HasTransparency = alpha;
        }

        public string Key { get; set; }

        public string Name { get; set; }

        private Color _color;
        public Color Color
        {
            get => _color;
            set => SetColor(value);
        }

        private void SetColor(Color value)
        {
            Set(ref _color, value, nameof(Color));
            RaisePropertyChanged(nameof(HexValue));
        }

        public string HexValue
        {
            get
            {
                if (_color.A < 255)
                {
                    return string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}", _color.A, _color.R, _color.G, _color.B);
                }

                return string.Format("#{0:X2}{1:X2}{2:X2}", _color.R, _color.G, _color.B);
            }
        }

        public bool HasTransparency { get; }

        public string Description { get; }

        public override string ToString()
        {
            return "{" + Key + ", " + HexValue + "}";
        }
    }

    public class ThemeBrushDiffHandler : IDiffHandler<ThemeBrush>
    {
        public bool CompareItems(ThemeBrush oldItem, ThemeBrush newItem)
        {
            return oldItem.Key == newItem.Key;
        }

        public void UpdateItem(ThemeBrush oldItem, ThemeBrush newItem)
        {

        }
    }
}
