// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using FluentEditorShared;
using FluentEditorShared.ColorPalette;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;
using Windows.UI;
using System.Linq;

namespace FluentEditor.ControlPalette.Model
{
    public interface IControlPaletteModel
    {
        Task InitializeData(StringProvider stringProvider, string dataPath);
        Task HandleAppSuspend();

        void AddOrReplacePreset(Preset preset);
        void ApplyPreset(Preset preset);
        ObservableList<Preset> Presets { get; }
        Preset ActivePreset { get; }
        event Action<IControlPaletteModel> ActivePresetChanged;

        IReadOnlyList<ColorMapping> LightColorMapping { get; }
        IReadOnlyList<ColorMapping> DarkColorMapping { get; }
        ColorPaletteEntry LightRegion { get; }
        ColorPaletteEntry DarkRegion { get; }
        ColorPalette LightBase { get; }
        ColorPalette DarkBase { get; }
        ColorPalette LightPrimary { get; }
        ColorPalette DarkPrimary { get; }

        ColorPaletteEntry MessageForeground { get; }
        ColorPaletteEntry MessageForegroundOut { get; }
        ColorPaletteEntry MessageBackground { get; }
        ColorPaletteEntry MessageBackgroundOut { get; }
        ColorPaletteEntry MessageSubtleLabel { get; }
        ColorPaletteEntry MessageSubtleLabelOut { get; }
        ColorPaletteEntry MessageSubtleGlyph { get; }
        ColorPaletteEntry MessageSubtleGlyphOut { get; }
        ColorPaletteEntry MessageSubtleForeground { get; }
        ColorPaletteEntry MessageSubtleForegroundOut { get; }
        ColorPaletteEntry MessageHeaderForeground { get; }
        ColorPaletteEntry MessageHeaderForegroundOut { get; }
        ColorPaletteEntry MessageHeaderBorder { get; }
        ColorPaletteEntry MessageHeaderBorderOut { get; }
        ColorPaletteEntry MessageMediaForeground { get; }
        ColorPaletteEntry MessageMediaForegroundOut { get; }
        ColorPaletteEntry MessageMediaBackground { get; }
        ColorPaletteEntry MessageMediaBackgroundOut { get; }
    }

    public class ControlPaletteModel : IControlPaletteModel
    {
        public async Task InitializeData(StringProvider stringProvider, string dataPath)
        {
            _stringProvider = stringProvider;

            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(dataPath));
            string dataString = await FileIO.ReadTextAsync(file);
            JsonObject rootObject = JsonObject.Parse(dataString);

            _whiteColor = new ColorPaletteEntry(Colors.White, _stringProvider.GetString("DarkThemeTextContrastTitle"), null, FluentEditorShared.Utils.ColorStringFormat.PoundRGB, null);
            _blackColor = new ColorPaletteEntry(Colors.Black, _stringProvider.GetString("LightThemeTextContrastTitle"), null, FluentEditorShared.Utils.ColorStringFormat.PoundRGB, null);

            var lightRegionNode = rootObject["LightRegion"].GetObject();
            _lightRegion = ColorPaletteEntry.Parse(lightRegionNode, null);

            var darkRegionNode = rootObject["DarkRegion"].GetObject();
            _darkRegion = ColorPaletteEntry.Parse(darkRegionNode, null);

            var lightBaseNode = rootObject["LightBase"].GetObject();
            _lightBase = ColorPalette.Parse(lightBaseNode, null);

            var darkBaseNode = rootObject["DarkBase"].GetObject();
            _darkBase = ColorPalette.Parse(darkBaseNode, null);

            var lightPrimaryNode = rootObject["LightPrimary"].GetObject();
            _lightPrimary = ColorPalette.Parse(lightPrimaryNode, null);

            var darkPrimaryNode = rootObject["DarkPrimary"].GetObject();
            _darkPrimary = ColorPalette.Parse(darkPrimaryNode, null);

            #region Message

            var messageForegroundNode = rootObject["MessageForeground"].GetObject();
            _messageForeground = ColorPaletteEntry.Parse(messageForegroundNode, null);
            var messageForegroundOutNode = rootObject["MessageForegroundOut"].GetObject();
            _messageForegroundOut = ColorPaletteEntry.Parse(messageForegroundOutNode, null);
            var messageBackgroundNode = rootObject["MessageBackground"].GetObject();
            _messageBackground = ColorPaletteEntry.Parse(messageBackgroundNode, null);
            var messageBackgroundOutNode = rootObject["MessageBackgroundOut"].GetObject();
            _messageBackgroundOut = ColorPaletteEntry.Parse(messageBackgroundOutNode, null);
            var messageSubtleLabelNode = rootObject["MessageSubtleLabel"].GetObject();
            _messageSubtleLabel = ColorPaletteEntry.Parse(messageSubtleLabelNode, null);
            var messageSubtleLabelOutNode = rootObject["MessageSubtleLabelOut"].GetObject();
            _messageSubtleLabelOut = ColorPaletteEntry.Parse(messageSubtleLabelOutNode, null);
            var messageSubtleGlyphNode = rootObject["MessageSubtleGlyph"].GetObject();
            _messageSubtleGlyph = ColorPaletteEntry.Parse(messageSubtleGlyphNode, null);
            var messageSubtleGlyphOutNode = rootObject["MessageSubtleGlyphOut"].GetObject();
            _messageSubtleGlyphOut = ColorPaletteEntry.Parse(messageSubtleGlyphOutNode, null);
            var messageSubtleForegroundNode = rootObject["MessageSubtleForeground"].GetObject();
            _messageSubtleForeground = ColorPaletteEntry.Parse(messageSubtleForegroundNode, null);
            var messageSubtleForegroundOutNode = rootObject["MessageSubtleForegroundOut"].GetObject();
            _messageSubtleForegroundOut = ColorPaletteEntry.Parse(messageSubtleForegroundOutNode, null);
            var messageHeaderForegroundNode = rootObject["MessageHeaderForeground"].GetObject();
            _messageHeaderForeground = ColorPaletteEntry.Parse(messageHeaderForegroundNode, null);
            var messageHeaderForegroundOutNode = rootObject["MessageHeaderForegroundOut"].GetObject();
            _messageHeaderForegroundOut = ColorPaletteEntry.Parse(messageHeaderForegroundOutNode, null);
            var messageHeaderBorderNode = rootObject["MessageHeaderBorder"].GetObject();
            _messageHeaderBorder = ColorPaletteEntry.Parse(messageHeaderBorderNode, null);
            var messageHeaderBorderOutNode = rootObject["MessageHeaderBorderOut"].GetObject();
            _messageHeaderBorderOut = ColorPaletteEntry.Parse(messageHeaderBorderOutNode, null);
            var messageMediaForegroundNode = rootObject["MessageMediaForeground"].GetObject();
            _messageMediaForeground = ColorPaletteEntry.Parse(messageMediaForegroundNode, null);
            var messageMediaForegroundOutNode = rootObject["MessageMediaForegroundOut"].GetObject();
            _messageMediaForegroundOut = ColorPaletteEntry.Parse(messageMediaForegroundOutNode, null);
            var messageMediaBackgroundNode = rootObject["MessageMediaBackground"].GetObject();
            _messageMediaBackground = ColorPaletteEntry.Parse(messageMediaBackgroundNode, null);
            var messageMediaBackgroundOutNode = rootObject["MessageMediaBackgroundOut"].GetObject();
            _messageMediaBackgroundOut = ColorPaletteEntry.Parse(messageMediaBackgroundOutNode, null);

            #endregion

            _presets = new ObservableList<Preset>();
            if (rootObject.ContainsKey("Presets"))
            {
                var presetsNode = rootObject["Presets"].GetArray();
                foreach (var presetNode in presetsNode)
                {
                    _presets.Add(Preset.Parse(presetNode.GetObject()));
                }
            }
            if (_presets.Count >= 1)
            {
                ApplyPreset(_presets[0]);
            }

            UpdateActivePreset();

            _lightRegion.ContrastColors = new List<ContrastColorWrapper>() { new ContrastColorWrapper(_whiteColor, false, false), new ContrastColorWrapper(_blackColor, true, true), new ContrastColorWrapper(_lightBase.BaseColor, true, false), new ContrastColorWrapper(_lightPrimary.BaseColor, true, false) };
            _darkRegion.ContrastColors = new List<ContrastColorWrapper>() { new ContrastColorWrapper(_whiteColor, true, true), new ContrastColorWrapper(_blackColor, false, false), new ContrastColorWrapper(_darkBase.BaseColor, true, false), new ContrastColorWrapper(_darkPrimary.BaseColor, true, false) };
            _lightBase.ContrastColors = new List<ContrastColorWrapper>() { new ContrastColorWrapper(_whiteColor, false, false), new ContrastColorWrapper(_blackColor, true, true), new ContrastColorWrapper(_lightRegion, true, false), new ContrastColorWrapper(_lightPrimary.BaseColor, true, false) };
            _darkBase.ContrastColors = new List<ContrastColorWrapper>() { new ContrastColorWrapper(_whiteColor, true, true), new ContrastColorWrapper(_blackColor, false, false), new ContrastColorWrapper(_darkRegion, true, false), new ContrastColorWrapper(_darkPrimary.BaseColor, true, false) };
            _lightPrimary.ContrastColors = new List<ContrastColorWrapper>() { new ContrastColorWrapper(_whiteColor, true, true), new ContrastColorWrapper(_blackColor, false, false), new ContrastColorWrapper(_lightRegion, true, false), new ContrastColorWrapper(_lightBase.BaseColor, true, false) };
            _darkPrimary.ContrastColors = new List<ContrastColorWrapper>() { new ContrastColorWrapper(_whiteColor, true, true), new ContrastColorWrapper(_blackColor, false, false), new ContrastColorWrapper(_darkRegion, true, false), new ContrastColorWrapper(_darkBase.BaseColor, true, false) };

            _lightColorMappings = ColorMapping.ParseList(rootObject["LightPaletteMapping"].GetArray(), _lightRegion, _darkRegion, _lightBase, _darkBase, _lightPrimary, _darkPrimary, _whiteColor, _blackColor);
            _lightColorMappings.Sort((a, b) =>
            {
                return a.Target.ToString().CompareTo(b.Target.ToString());
            });

            _darkColorMappings = ColorMapping.ParseList(rootObject["DarkPaletteMapping"].GetArray(), _lightRegion, _darkRegion, _lightBase, _darkBase, _lightPrimary, _darkPrimary, _whiteColor, _blackColor);
            _darkColorMappings.Sort((a, b) =>
            {
                return a.Target.ToString().CompareTo(b.Target.ToString());
            });

            _lightRegion.ActiveColorChanged += PaletteEntry_ActiveColorChanged;
            _darkRegion.ActiveColorChanged += PaletteEntry_ActiveColorChanged;
            _lightBase.BaseColor.ActiveColorChanged += PaletteEntry_ActiveColorChanged;
            _darkBase.BaseColor.ActiveColorChanged += PaletteEntry_ActiveColorChanged;
            _lightPrimary.BaseColor.ActiveColorChanged += PaletteEntry_ActiveColorChanged;
            _darkPrimary.BaseColor.ActiveColorChanged += PaletteEntry_ActiveColorChanged;
            foreach (var entry in _lightBase.Palette)
            {
                entry.ActiveColorChanged += PaletteEntry_ActiveColorChanged;
            }
            foreach (var entry in _darkBase.Palette)
            {
                entry.ActiveColorChanged += PaletteEntry_ActiveColorChanged;
            }
            foreach (var entry in _lightPrimary.Palette)
            {
                entry.ActiveColorChanged += PaletteEntry_ActiveColorChanged;
            }
            foreach (var entry in _darkPrimary.Palette)
            {
                entry.ActiveColorChanged += PaletteEntry_ActiveColorChanged;
            }

            if (_lightRegion.Description == null)
            {
                _lightRegion.Description = GenerateMappingDescription(_lightRegion, _lightColorMappings);
            }
            if (_darkRegion.Description == null)
            {
                _darkRegion.Description = GenerateMappingDescription(_darkRegion, _darkColorMappings);
            }
            foreach (var entry in _lightBase.Palette)
            {
                if (entry.Description == null)
                {
                    entry.Description = GenerateMappingDescription(entry, _lightColorMappings);
                }
            }
            foreach (var entry in _darkBase.Palette)
            {
                if (entry.Description == null)
                {
                    entry.Description = GenerateMappingDescription(entry, _darkColorMappings);
                }
            }
            foreach (var entry in _lightPrimary.Palette)
            {
                if (entry.Description == null)
                {
                    entry.Description = GenerateMappingDescription(entry, _lightColorMappings);
                }
            }
            foreach (var entry in _darkPrimary.Palette)
            {
                if (entry.Description == null)
                {
                    entry.Description = GenerateMappingDescription(entry, _darkColorMappings);
                }
            }
        }

        private string GenerateMappingDescription(IColorPaletteEntry paletteEntry, List<ColorMapping> mappings)
        {
            string retVal = string.Empty;

            foreach (var mapping in mappings)
            {
                if (mapping.Source == paletteEntry)
                {
                    if (retVal != string.Empty)
                    {
                        retVal += ", ";
                    }
                    retVal += mapping.Target.ToString();
                }
            }

            if (retVal != string.Empty)
            {
                return string.Format(_stringProvider.GetString("ColorFlyoutMappingDescription"), retVal);
            }
            else
            {
                return null;
            }
        }

        public Task HandleAppSuspend()
        {
            // Currently nothing to do here
            return Task.CompletedTask;
        }

        private void PaletteEntry_ActiveColorChanged(IColorPaletteEntry obj)
        {
            UpdateActivePreset();
        }

        private void UpdateActivePreset()
        {
            if (_presets != null)
            {
                for (int i = 0; i < _presets.Count; i++)
                {
                    if (_presets[i].IsPresetActive(this))
                    {
                        ActivePreset = _presets[i];
                        return;
                    }
                }
            }
            ActivePreset = null;
        }

        private StringProvider _stringProvider;

        public void AddOrReplacePreset(Preset preset)
        {
            if (!string.IsNullOrEmpty(preset.Name))
            {
                var oldPreset = _presets.FirstOrDefault<Preset>((a) => a.Id == preset.Id);
                if (oldPreset != null)
                {
                    _presets.Remove(oldPreset);
                }
            }

            _presets.Add(preset);

            UpdateActivePreset();
        }

        public void ApplyPreset(Preset preset)
        {
            if (preset == null)
            {
                ActivePreset = null;
                return;
            }

            _lightRegion.ActiveColor = preset.LightRegionColor;
            _darkRegion.ActiveColor = preset.DarkRegionColor;
            _lightBase.BaseColor.ActiveColor = preset.LightBaseColor;
            _darkBase.BaseColor.ActiveColor = preset.DarkBaseColor;
            _lightPrimary.BaseColor.ActiveColor = preset.LightPrimaryColor;
            _darkPrimary.BaseColor.ActiveColor = preset.DarkPrimaryColor;

            ApplyPresetOverrides(_lightBase.Palette, preset.LightBaseOverrides);
            ApplyPresetOverrides(_darkBase.Palette, preset.DarkBaseOverrides);
            ApplyPresetOverrides(_lightPrimary.Palette, preset.LightPrimaryOverrides);
            ApplyPresetOverrides(_darkPrimary.Palette, preset.DarkPrimaryOverrides);
        }

        private void ApplyPresetOverrides(IReadOnlyList<EditableColorPaletteEntry> palette, Dictionary<int, Color> overrides)
        {
            for (int i = 0; i < palette.Count; i++)
            {
                if (overrides != null && overrides.ContainsKey(i))
                {
                    palette[i].CustomColor = overrides[i];
                    palette[i].UseCustomColor = true;
                }
                else
                {
                    palette[i].UseCustomColor = false;
                }
            }
        }

        private ObservableList<Preset> _presets;
        public ObservableList<Preset> Presets
        {
            get { return _presets; }
        }

        private Preset _activePreset;
        public Preset ActivePreset
        {
            get { return _activePreset; }
            private set
            {
                if (_activePreset != value)
                {
                    _activePreset = value;
                    ActivePresetChanged?.Invoke(this);
                }
            }
        }

        public event Action<IControlPaletteModel> ActivePresetChanged;

        private List<ColorMapping> _lightColorMappings;
        public IReadOnlyList<ColorMapping> LightColorMapping
        {
            get { return _lightColorMappings; }
        }

        private List<ColorMapping> _darkColorMappings;
        public IReadOnlyList<ColorMapping> DarkColorMapping
        {
            get { return _darkColorMappings; }
        }

        private ColorPaletteEntry _whiteColor;
        private ColorPaletteEntry _blackColor;

        private ColorPaletteEntry _lightRegion;
        public ColorPaletteEntry LightRegion
        {
            get { return _lightRegion; }
        }

        private ColorPaletteEntry _darkRegion;
        public ColorPaletteEntry DarkRegion
        {
            get { return _darkRegion; }
        }

        private ColorPalette _lightBase;
        public ColorPalette LightBase
        {
            get { return _lightBase; }
        }

        private ColorPalette _darkBase;
        public ColorPalette DarkBase
        {
            get { return _darkBase; }
        }

        private ColorPalette _lightPrimary;
        public ColorPalette LightPrimary
        {
            get { return _lightPrimary; }
        }

        private ColorPalette _darkPrimary;
        public ColorPalette DarkPrimary
        {
            get { return _darkPrimary; }
        }



        #region Message

        private ColorPaletteEntry _messageForeground;
        public ColorPaletteEntry MessageForeground
        {
            get { return _messageForeground; }
        }

        private ColorPaletteEntry _messageForegroundOut;
        public ColorPaletteEntry MessageForegroundOut
        {
            get { return _messageForegroundOut; }
        }

        private ColorPaletteEntry _messageBackground;
        public ColorPaletteEntry MessageBackground
        {
            get { return _messageBackground; }
        }

        private ColorPaletteEntry _messageBackgroundOut;
        public ColorPaletteEntry MessageBackgroundOut
        {
            get { return _messageBackgroundOut; }
        }

        private ColorPaletteEntry _messageSubtleLabel;
        public ColorPaletteEntry MessageSubtleLabel
        {
            get { return _messageSubtleLabel; }
        }

        private ColorPaletteEntry _messageSubtleLabelOut;
        public ColorPaletteEntry MessageSubtleLabelOut
        {
            get { return _messageSubtleLabelOut; }
        }

        private ColorPaletteEntry _messageSubtleGlyph;
        public ColorPaletteEntry MessageSubtleGlyph
        {
            get { return _messageSubtleGlyph; }
        }

        private ColorPaletteEntry _messageSubtleGlyphOut;
        public ColorPaletteEntry MessageSubtleGlyphOut
        {
            get { return _messageSubtleGlyphOut; }
        }

        private ColorPaletteEntry _messageSubtleForeground;
        public ColorPaletteEntry MessageSubtleForeground
        {
            get { return _messageSubtleForeground; }
        }

        private ColorPaletteEntry _messageSubtleForegroundOut;
        public ColorPaletteEntry MessageSubtleForegroundOut
        {
            get { return _messageSubtleForegroundOut; }
        }

        private ColorPaletteEntry _messageHeaderForeground;
        public ColorPaletteEntry MessageHeaderForeground
        {
            get { return _messageHeaderForeground; }
        }

        private ColorPaletteEntry _messageHeaderForegroundOut;
        public ColorPaletteEntry MessageHeaderForegroundOut
        {
            get { return _messageHeaderForegroundOut; }
        }

        private ColorPaletteEntry _messageHeaderBorder;
        public ColorPaletteEntry MessageHeaderBorder
        {
            get { return _messageHeaderBorder; }
        }

        private ColorPaletteEntry _messageHeaderBorderOut;
        public ColorPaletteEntry MessageHeaderBorderOut
        {
            get { return _messageHeaderBorderOut; }
        }

        private ColorPaletteEntry _messageMediaForeground;
        public ColorPaletteEntry MessageMediaForeground
        {
            get { return _messageMediaForeground; }
        }

        private ColorPaletteEntry _messageMediaForegroundOut;
        public ColorPaletteEntry MessageMediaForegroundOut
        {
            get { return _messageMediaForegroundOut; }
        }

        private ColorPaletteEntry _messageMediaBackground;
        public ColorPaletteEntry MessageMediaBackground
        {
            get { return _messageMediaBackground; }
        }

        private ColorPaletteEntry _messageMediaBackgroundOut;
        public ColorPaletteEntry MessageMediaBackgroundOut
        {
            get { return _messageMediaBackgroundOut; }
        }

        #endregion
    }
}
