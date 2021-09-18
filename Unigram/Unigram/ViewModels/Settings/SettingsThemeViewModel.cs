using Rg.DiffUtils;
using System;
using System.Linq;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Navigation;
using Unigram.Services;
using Unigram.Views.Popups;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml.Controls;

namespace Unigram.ViewModels.Settings
{
    public class SettingsThemeViewModel : TLViewModelBase
    {
        private readonly IThemeService _themeService;

        private ThemeCustomInfo _theme;
        private ThemeBrush[] _index;

        private StorageFile _file;

        public SettingsThemeViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, IThemeService themeService)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _themeService = themeService;

            Items = new DiffObservableCollection<ThemeBrush>(new ThemeBrushDiffHandler(), new DiffOptions { AllowBatching = false, DetectMoves = false });

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
            Items.ReplaceDiff(_index.Where(x => x.Key.Contains(value, StringComparison.OrdinalIgnoreCase)));
        }

        public DiffObservableCollection<ThemeBrush> Items { get; }

        public void Initialize(ThemeCustomInfo theme)
        {
            _theme = theme;

            Title = theme.Name;
            Items.Clear();

            var lookup = ThemeService.GetLookup(theme.Parent);
            var i = 0;

            _index = new ThemeBrush[lookup.Count];

            foreach (var value in lookup)
            {
                if (theme.Values.TryGetValue(value.Key, out Color custom))
                {
                    _index[i] = new ThemeBrush(value.Key, custom);
                }
                else if (value.Value != default)
                {
                    _index[i] = new ThemeBrush(value.Key, value.Value);
                }

                if (_index[i] != null)
                {
                    Items.Add(_index[i++]);
                }
            }
        }

        public RelayCommand EditTitleCommand { get; }
        private async void EditTitleExecute()
        {
            var dialog = new InputPopup();
            dialog.Title = Strings.Resources.EditName;
            dialog.Text = _theme.Name;
            dialog.PrimaryButtonText = Strings.Resources.OK;
            dialog.SecondaryButtonText = Strings.Resources.Cancel;

            var confirm = await dialog.ShowQueuedAsync();
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
            var dialog = new SelectColorPopup();
            dialog.Title = brush.Key;
            dialog.Color = brush.Color;
            dialog.IsAccentColorVisible = false;

            var confirm = await dialog.ShowQueuedAsync();
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
        public ThemeBrush(string key, Color color)
        {
            if (key.EndsWith("Brush"))
            {
                Name = key.Substring(0, key.Length - "Brush".Length);
            }

            Name ??= key;
            Key = key;

            _color = color;
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
            RaisePropertyChanged(nameof(HasTransparency));
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

        public bool HasTransparency => _color.A < 255;

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
