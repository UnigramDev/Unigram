using FluentEditor.ControlPalette;
using FluentEditor.ControlPalette.Model;
using FluentEditorShared;
using FluentEditorShared.ColorPalette;
using FluentEditorShared.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Provider;
using Windows.System.Profile;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls.Views
{
    public sealed partial class ThemeEditorView : UserControl
    {
        public ControlPaletteViewModel ViewModel => DataContext as ControlPaletteViewModel;

        public ThemeEditorView()
        {
            InitializeComponent();
            InitializeTitleBar();
        }

        public void Update()
        {
            Bindings.Update();
        }

        private void InitializeTitleBar()
        {
            var sender = CoreApplication.GetCurrentView().TitleBar;

            if (string.Equals(AnalyticsInfo.VersionInfo.DeviceFamily, "Windows.Desktop") && UIViewSettings.GetForCurrentView().UserInteractionMode == UserInteractionMode.Mouse)
            {
                // If running on PC and tablet mode is disabled, then titlebar is most likely visible
                // So we're going to force it
                Header.Padding = new Thickness(0, 32, 0, 0);
            }
            else
            {
                Header.Padding = new Thickness(0, sender.IsVisible ? sender.Height : 0, 0, 0);
            }

            sender.ExtendViewIntoTitleBar = true;
            sender.IsVisibleChanged += CoreTitleBar_LayoutMetricsChanged;
            sender.LayoutMetricsChanged += CoreTitleBar_LayoutMetricsChanged;
        }

        private void CoreTitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            Header.Padding = new Thickness(0, sender.IsVisible ? sender.Height : 0, 0, 0);

            var popups = VisualTreeHelper.GetOpenPopups(Window.Current);
            foreach (var popup in popups)
            {
                if (popup.Child is OverlayPage contentDialog)
                {
                    contentDialog.Padding = new Thickness(0, sender.IsVisible ? sender.Height : 0, 0, 0);
                }
            }
        }
    }

    public class ControlPaletteViewModel : INotifyPropertyChanged
    {
        public static ControlPaletteViewModel Parse(IStringProvider stringProvider, JsonObject data, IControlPaletteModel paletteModel, IControlPaletteExportProvider exportProvider)
        {
            return new ControlPaletteViewModel(stringProvider, paletteModel, data["Id"].GetOptionalString(), data["Title"].GetOptionalString(), data["Glyph"].GetOptionalString(), exportProvider);
        }

        public ControlPaletteViewModel(IStringProvider stringProvider, IControlPaletteModel paletteModel, string id, string title, string glyph, IControlPaletteExportProvider exportProvider)
        {
            _stringProvider = stringProvider;
            _id = id;
            _title = title;
            _glyph = glyph;

            _paletteModel = paletteModel;
            _exportProvider = exportProvider;

            _lightRegionBrush = new SolidColorBrush(_paletteModel.LightRegion.ActiveColor);
            _darkRegionBrush = new SolidColorBrush(_paletteModel.DarkRegion.ActiveColor);

            _paletteModel.LightRegion.ActiveColorChanged += LightRegion_ActiveColorChanged;
            _paletteModel.DarkRegion.ActiveColorChanged += DarkRegion_ActiveColorChanged;

            _paletteModel.ActivePresetChanged += OnActivePresetChanged;

            _paletteModel.MessageForeground.ActiveColorChanged += Message_ActiveColorChanged;
            _paletteModel.MessageForegroundOut.ActiveColorChanged += Message_ActiveColorChanged;
            _paletteModel.MessageBackground.ActiveColorChanged += Message_ActiveColorChanged;
            _paletteModel.MessageBackgroundOut.ActiveColorChanged += Message_ActiveColorChanged;
            _paletteModel.MessageSubtleLabel.ActiveColorChanged += Message_ActiveColorChanged;
            _paletteModel.MessageSubtleLabelOut.ActiveColorChanged += Message_ActiveColorChanged;
            _paletteModel.MessageSubtleGlyph.ActiveColorChanged += Message_ActiveColorChanged;
            _paletteModel.MessageSubtleGlyphOut.ActiveColorChanged += Message_ActiveColorChanged;
            _paletteModel.MessageSubtleForeground.ActiveColorChanged += Message_ActiveColorChanged;
            _paletteModel.MessageSubtleForegroundOut.ActiveColorChanged += Message_ActiveColorChanged;
            _paletteModel.MessageHeaderForeground.ActiveColorChanged += Message_ActiveColorChanged;
            _paletteModel.MessageHeaderForegroundOut.ActiveColorChanged += Message_ActiveColorChanged;
            _paletteModel.MessageHeaderBorder.ActiveColorChanged += Message_ActiveColorChanged;
            _paletteModel.MessageHeaderBorderOut.ActiveColorChanged += Message_ActiveColorChanged;
            _paletteModel.MessageMediaForeground.ActiveColorChanged += Message_ActiveColorChanged;
            _paletteModel.MessageMediaForegroundOut.ActiveColorChanged += Message_ActiveColorChanged;
            _paletteModel.MessageMediaBackground.ActiveColorChanged += Message_ActiveColorChanged;
            _paletteModel.MessageMediaBackgroundOut.ActiveColorChanged += Message_ActiveColorChanged;
        }

        private async void Message_ActiveColorChanged(IColorPaletteEntry obj)
        {
            var theme = new ResourceDictionary();
            theme["MessageForegroundColor"] = _paletteModel.MessageForeground.ActiveColor;
            theme["MessageForegroundOutColor"] = _paletteModel.MessageForegroundOut.ActiveColor;
            theme["MessageBackgroundColor"] = _paletteModel.MessageBackground.ActiveColor;
            theme["MessageBackgroundOutColor"] = _paletteModel.MessageBackgroundOut.ActiveColor;
            theme["MessageSubtleLabelColor"] = _paletteModel.MessageSubtleLabel.ActiveColor;
            theme["MessageSubtleLabelOutColor"] = _paletteModel.MessageSubtleLabelOut.ActiveColor;
            theme["MessageSubtleGlyphColor"] = _paletteModel.MessageSubtleGlyph.ActiveColor;
            theme["MessageSubtleGlyphOutColor"] = _paletteModel.MessageSubtleGlyphOut.ActiveColor;
            theme["MessageSubtleForegroundColor"] = _paletteModel.MessageSubtleForeground.ActiveColor;
            theme["MessageSubtleForegroundOutColor"] = _paletteModel.MessageSubtleForegroundOut.ActiveColor;
            theme["MessageHeaderForegroundColor"] = _paletteModel.MessageHeaderForeground.ActiveColor;
            theme["MessageHeaderForegroundOutColor"] = _paletteModel.MessageHeaderForegroundOut.ActiveColor;
            theme["MessageHeaderBorderColor"] = _paletteModel.MessageHeaderBorder.ActiveColor;
            theme["MessageHeaderBorderOutColor"] = _paletteModel.MessageHeaderBorderOut.ActiveColor;
            theme["MessageMediaForegroundColor"] = _paletteModel.MessageMediaForeground.ActiveColor;
            theme["MessageMediaForegroundOutColor"] = _paletteModel.MessageMediaForegroundOut.ActiveColor;
            theme["MessageMediaBackgroundColor"] = _paletteModel.MessageMediaBackground.ActiveColor;
            theme["MessageMediaBackgroundOutColor"] = _paletteModel.MessageMediaBackgroundOut.ActiveColor;

            theme["MessageOverlayBackgroundColor"] = Windows.UI.Color.FromArgb(0x54, 00, 00, 00);
            theme["MessageOverlayBackgroundOutColor"] = Windows.UI.Color.FromArgb(0x54, 00, 00, 00);
            theme["MessageCallForegroundColor"] = Windows.UI.Color.FromArgb(0x54, 00, 00, 00);
            theme["MessageCallForegroundOutColor"] = Windows.UI.Color.FromArgb(0x54, 00, 00, 00);
            theme["MessageCallMissedForegroundColor"] = Windows.UI.Color.FromArgb(0x54, 00, 00, 00);
            theme["MessageCallMissedForegroundOutColor"] = Windows.UI.Color.FromArgb(0x54, 00, 00, 00);


            var dictionary = new ResourceDictionary();
            dictionary.ThemeDictionaries["Light"] = theme;
            dictionary["MessageForegroundBrush"] = new SolidColorBrush(_paletteModel.MessageForeground.ActiveColor);
            dictionary["MessageBackgroundBrush"] = new SolidColorBrush(_paletteModel.MessageBackground.ActiveColor);
            dictionary["MessageSubtleLabelBrush"] = new SolidColorBrush(_paletteModel.MessageSubtleLabel.ActiveColor);
            dictionary["MessageSubtleGlyphBrush"] = new SolidColorBrush(_paletteModel.MessageSubtleGlyph.ActiveColor);
            dictionary["MessageSubtleForegroundBrush"] = new SolidColorBrush(_paletteModel.MessageSubtleForeground.ActiveColor);
            dictionary["MessageHeaderForegroundBrush"] = new SolidColorBrush(_paletteModel.MessageHeaderForeground.ActiveColor);
            dictionary["MessageHeaderBorderBrush"] = new SolidColorBrush(_paletteModel.MessageHeaderBorder.ActiveColor);
            dictionary["MessageMediaForegroundBrush"] = new SolidColorBrush(_paletteModel.MessageMediaForeground.ActiveColor);
            dictionary["MessageMediaBackgroundBrush"] = new SolidColorBrush(_paletteModel.MessageMediaBackground.ActiveColor);

            theme["MessageOverlayBackgroundBrush"] = new SolidColorBrush(Windows.UI.Color.FromArgb(0x54, 00, 00, 00));
            theme["MessageCallForegroundBrush"] = new SolidColorBrush(Windows.UI.Color.FromArgb(0x54, 00, 00, 00));
            theme["MessageCallMissedForegroundBrush"] = new SolidColorBrush(Windows.UI.Color.FromArgb(0x54, 00, 00, 00));
            theme["MessageHyperlinkForegroundBrush"] = new SolidColorBrush(Windows.UI.Colors.Black);

            await Unigram.Common.Theme.Current.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                Unigram.Common.Theme.Current.MergedDictionaries.Clear();
                Unigram.Common.Theme.Current.MergedDictionaries.Add(dictionary);
            });

            foreach (Common.TLWindowContext window in Template10.Common.WindowContext.ActiveWrappers)
            {
                window.Dispatcher.Dispatch(() =>
                {
                    window.UpdateTitleBar();

                    if (window.Content is FrameworkElement element)
                    {
                        element.RequestedTheme = ElementTheme.Dark;
                        element.RequestedTheme = ElementTheme.Light;
                    }
                });
            }
        }

        private IStringProvider _stringProvider;

        private readonly string _id;
        public string Id
        {
            get { return _id; }
        }

        private string _title;
        public string Title
        {
            get { return _title; }
            set
            {
                if (_title != value)
                {
                    _title = value;
                    RaisePropertyChangedFromSource();
                }
            }
        }

        private string _glyph;
        public string Glyph
        {
            get { return _glyph; }
            set
            {
                if (_glyph != value)
                {
                    _glyph = value;
                    RaisePropertyChangedFromSource();
                }
            }
        }

        public void OnSaveDataRequested(object sender, RoutedEventArgs e)
        {
            _ = SaveData();
        }

        private async Task SaveData()
        {
            StorageFile file = await FilePickerAdapters.ShowSaveFilePicker("ColorData", ".json", new Tuple<string, IList<string>>[] { new Tuple<string, IList<string>>("JSON", new List<string>() { ".json" }) }, null, Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary, true, true);
            if (file == null)
            {
                return;
            }
            CachedFileManager.DeferUpdates(file);

            Preset savePreset = new Preset(file.Path, file.DisplayName, _paletteModel);
            var saveData = Preset.Serialize(savePreset);
            var saveString = saveData.Stringify();

            await FileIO.WriteTextAsync(file, saveString);
            FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
            if (status == FileUpdateStatus.Complete)
            {
                _paletteModel.AddOrReplacePreset(savePreset);
                _paletteModel.ApplyPreset(savePreset);
            }
            else
            {
                if (file == null || file.Path == null)
                {
                    return;
                }
                var message = string.Format(_stringProvider.GetString("ControlPaletteSaveError"), file.Path);
                ContentDialog saveFailedDialog = new ContentDialog()
                {
                    CloseButtonText = _stringProvider.GetString("ControlPaletteErrorOkButtonCaption"),
                    Title = _stringProvider.GetString("ControlPaletteSaveErrorTitle"),
                    Content = message
                };
                _ = saveFailedDialog.ShowAsync();
                return;
            }
        }

        public void OnLoadDataRequested(object sender, RoutedEventArgs e)
        {
            _ = LoadData();
        }

        private async Task LoadData()
        {
            StorageFile file = await FilePickerAdapters.ShowLoadFilePicker(new string[] { ".json" }, Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary, Windows.Storage.Pickers.PickerViewMode.List, true, true);
            if (file == null)
            {
                return;
            }
            string dataString = await FileIO.ReadTextAsync(file);
            JsonObject rootObject = JsonObject.Parse(dataString);
            Preset loadedPreset = null;
            try
            {
                loadedPreset = Preset.Parse(rootObject, file.Path, file.DisplayName);
            }
            catch
            {
                loadedPreset = null;
            }

            if (loadedPreset == null)
            {
                if (file == null || file.Path == null)
                {
                    return;
                }
                var message = string.Format(_stringProvider.GetString("ControlPaletteLoadError"), file.Path);
                ContentDialog loadFailedDialog = new ContentDialog()
                {
                    CloseButtonText = _stringProvider.GetString("ControlPaletteErrorOkButtonCaption"),
                    Title = _stringProvider.GetString("ControlPaletteLoadErrorTitle"),
                    Content = message
                };
                _ = loadFailedDialog.ShowAsync();
                return;
            }

            _paletteModel.AddOrReplacePreset(loadedPreset);
            _paletteModel.ApplyPreset(loadedPreset);
        }

        private void OnActivePresetChanged(IControlPaletteModel obj)
        {
            RaisePropertyChanged("ActivePreset");
        }

        private readonly IControlPaletteModel _paletteModel;
        private readonly IControlPaletteExportProvider _exportProvider;

        public Preset ActivePreset
        {
            get { return _paletteModel.ActivePreset; }
            set
            {
                _paletteModel.ApplyPreset(value);
            }
        }

        public IReadOnlyList<Preset> Presets
        {
            get { return _paletteModel.Presets; }
        }

        public ColorPaletteEntry LightRegion
        {
            get { return _paletteModel.LightRegion; }
        }

        private void LightRegion_ActiveColorChanged(IColorPaletteEntry obj)
        {
            _lightRegionBrush.Color = obj.ActiveColor;
        }

        private SolidColorBrush _lightRegionBrush;
        public SolidColorBrush LightRegionBrush
        {
            get { return _lightRegionBrush; }
        }

        public ColorPaletteEntry DarkRegion
        {
            get { return _paletteModel.DarkRegion; }
        }

        private void DarkRegion_ActiveColorChanged(IColorPaletteEntry obj)
        {
            _darkRegionBrush.Color = obj.ActiveColor;
        }

        private SolidColorBrush _darkRegionBrush;
        public SolidColorBrush DarkRegionBrush
        {
            get { return _darkRegionBrush; }
        }

        public ColorPalette LightBase
        {
            get { return _paletteModel.LightBase; }
        }

        public ColorPalette DarkBase
        {
            get { return _paletteModel.DarkBase; }
        }

        public ColorPalette LightPrimary
        {
            get { return _paletteModel.LightPrimary; }
        }

        public ColorPalette DarkPrimary
        {
            get { return _paletteModel.DarkPrimary; }
        }

        public IReadOnlyList<ColorMapping> LightColorMapping
        {
            get { return _paletteModel.LightColorMapping; }
        }

        public IReadOnlyList<ColorMapping> DarkColorMapping
        {
            get { return _paletteModel.DarkColorMapping; }
        }

        public void OnExportRequested(object sender, RoutedEventArgs e)
        {
            _exportProvider.ShowExportView(_exportProvider.GenerateExportData(_paletteModel));
        }

        #region Message

        public ColorPaletteEntry MessageForeground
        {
            get { return _paletteModel.MessageForeground; }
        }

        public ColorPaletteEntry MessageForegroundOut
        {
            get { return _paletteModel.MessageForegroundOut; }
        }

        public ColorPaletteEntry MessageBackground
        {
            get { return _paletteModel.MessageBackground; }
        }

        public ColorPaletteEntry MessageBackgroundOut
        {
            get { return _paletteModel.MessageBackgroundOut; }
        }

        public ColorPaletteEntry MessageSubtleLabel
        {
            get { return _paletteModel.MessageSubtleLabel; }
        }

        public ColorPaletteEntry MessageSubtleLabelOut
        {
            get { return _paletteModel.MessageSubtleLabelOut; }
        }

        public ColorPaletteEntry MessageSubtleGlyph
        {
            get { return _paletteModel.MessageSubtleGlyph; }
        }

        public ColorPaletteEntry MessageSubtleGlyphOut
        {
            get { return _paletteModel.MessageSubtleGlyphOut; }
        }

        public ColorPaletteEntry MessageSubtleForeground
        {
            get { return _paletteModel.MessageSubtleForeground; }
        }

        public ColorPaletteEntry MessageSubtleForegroundOut
        {
            get { return _paletteModel.MessageSubtleForegroundOut; }
        }

        public ColorPaletteEntry MessageHeaderForeground
        {
            get { return _paletteModel.MessageHeaderForeground; }
        }

        public ColorPaletteEntry MessageHeaderForegroundOut
        {
            get { return _paletteModel.MessageHeaderForegroundOut; }
        }

        public ColorPaletteEntry MessageHeaderBorder
        {
            get { return _paletteModel.MessageHeaderBorder; }
        }

        public ColorPaletteEntry MessageHeaderBorderOut
        {
            get { return _paletteModel.MessageHeaderBorderOut; }
        }

        public ColorPaletteEntry MessageMediaForeground
        {
            get { return _paletteModel.MessageMediaForeground; }
        }

        public ColorPaletteEntry MessageMediaForegroundOut
        {
            get { return _paletteModel.MessageMediaForegroundOut; }
        }

        public ColorPaletteEntry MessageMediaBackground
        {
            get { return _paletteModel.MessageMediaBackground; }
        }

        public ColorPaletteEntry MessageMediaBackgroundOut
        {
            get { return _paletteModel.MessageMediaBackgroundOut; }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void RaisePropertyChangedFromSource([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion
    }
}
