// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using Windows.Data.Json;
using Windows.UI;
using FluentEditorShared.Utils;
using FluentEditorShared.ColorPalette;

namespace FluentEditor.ControlPalette.Model
{
    public class Preset
    {
        public static Preset Parse(JsonObject data, string id = null, string name = null)
        {
            if(id == null)
            {
                id = data["Id"].GetString();
            }
            if (name == null)
            {
                name = data["Name"].GetString();
            }
            Color lightRegionColor = data["LightRegionColor"].GetColor();
            Color darkRegionColor = data["DarkRegionColor"].GetColor();
            Color lightBaseColor = data["LightBaseColor"].GetColor();
            Color darkBaseColor = data["DarkBaseColor"].GetColor();
            Color lightPrimaryColor = data["LightPrimaryColor"].GetColor();
            Color darkPrimaryColor = data["DarkPrimaryColor"].GetColor();

            Dictionary<int, Color> lightBaseOverrides = new Dictionary<int, Color>();
            if (data.ContainsKey("LightBaseOverrides"))
            {
                var lightBaseOverridesNode = data["LightBaseOverrides"].GetArray();
                foreach (var v in lightBaseOverridesNode)
                {
                    var node = v.GetObject();

                    int index = node["Index"].GetInt();
                    Color color = node["Color"].GetColor();
                    lightBaseOverrides.Add(index, color);
                }
            }

            Dictionary<int, Color> darkBaseOverrides = new Dictionary<int, Color>();
            if (data.ContainsKey("DarkBaseOverrides"))
            {
                var darkBaseOverridesNode = data["DarkBaseOverrides"].GetArray();
                foreach (var v in darkBaseOverridesNode)
                {
                    var node = v.GetObject();

                    int index = node["Index"].GetInt();
                    Color color = node["Color"].GetColor();
                    darkBaseOverrides.Add(index, color);
                }
            }

            Dictionary<int, Color> lightPrimaryOverrides = new Dictionary<int, Color>();
            if (data.ContainsKey("LightPrimaryOverrides"))
            {
                var lightPrimaryOverridesNode = data["LightPrimaryOverrides"].GetArray();
                foreach (var v in lightPrimaryOverridesNode)
                {
                    var node = v.GetObject();

                    int index = node["Index"].GetInt();
                    Color color = node["Color"].GetColor();
                    lightPrimaryOverrides.Add(index, color);
                }
            }

            Dictionary<int, Color> darkPrimaryOverrides = new Dictionary<int, Color>();
            if (data.ContainsKey("DarkPrimaryOverrides"))
            {
                var darkPrimaryOverridesNode = data["DarkPrimaryOverrides"].GetArray();
                foreach (var v in darkPrimaryOverridesNode)
                {
                    var node = v.GetObject();

                    int index = node["Index"].GetInt();
                    Color color = node["Color"].GetColor();
                    darkPrimaryOverrides.Add(index, color);
                }
            }

            return new Preset(id, name, lightRegionColor, darkRegionColor, lightBaseColor, lightBaseOverrides, darkBaseColor, darkBaseOverrides, lightPrimaryColor, lightPrimaryOverrides, darkPrimaryColor, darkPrimaryOverrides);
        }

        public static JsonObject Serialize(Preset preset)
        {
            JsonObject data = new JsonObject();
            if (!string.IsNullOrEmpty(preset.Id))
            {
                data["Id"] = JsonValue.CreateStringValue(preset.Id);
            }
            if (!string.IsNullOrEmpty(preset.Name))
            {
                data["Name"] = JsonValue.CreateStringValue(preset.Name);
            }
            data["LightRegionColor"] = JsonValue.CreateStringValue(preset.LightRegionColor.ToString());
            data["DarkRegionColor"] = JsonValue.CreateStringValue(preset.DarkRegionColor.ToString());
            data["LightBaseColor"] = JsonValue.CreateStringValue(preset.LightBaseColor.ToString());
            data["DarkBaseColor"] = JsonValue.CreateStringValue(preset.DarkBaseColor.ToString());
            data["LightPrimaryColor"] = JsonValue.CreateStringValue(preset.LightPrimaryColor.ToString());
            data["DarkPrimaryColor"] = JsonValue.CreateStringValue(preset.DarkPrimaryColor.ToString());

            var lightBaseOverridesNode = new JsonArray();
            foreach (int index in preset.LightBaseOverrides.Keys)
            {
                JsonObject node = new JsonObject();
                node["Index"] = JsonValue.CreateNumberValue(index);
                node["Color"] = JsonValue.CreateStringValue(preset.LightBaseOverrides[index].ToString());
                lightBaseOverridesNode.Add(node);
            }
            data["LightBaseOverrides"] = lightBaseOverridesNode;

            var darkBaseOverridesNode = new JsonArray();
            foreach (int index in preset.DarkBaseOverrides.Keys)
            {
                JsonObject node = new JsonObject();
                node["Index"] = JsonValue.CreateNumberValue(index);
                node["Color"] = JsonValue.CreateStringValue(preset.DarkBaseOverrides[index].ToString());
                darkBaseOverridesNode.Add(node);
            }
            data["DarkBaseOverrides"] = darkBaseOverridesNode;

            var lightPrimaryOverridesNode = new JsonArray();
            foreach (int index in preset.LightPrimaryOverrides.Keys)
            {
                JsonObject node = new JsonObject();
                node["Index"] = JsonValue.CreateNumberValue(index);
                node["Color"] = JsonValue.CreateStringValue(preset.LightPrimaryOverrides[index].ToString());
                lightPrimaryOverridesNode.Add(node);
            }
            data["LightPrimaryOverrides"] = lightPrimaryOverridesNode;

            var darkPrimaryOverridesNode = new JsonArray();
            foreach (int index in preset.DarkPrimaryOverrides.Keys)
            {
                JsonObject node = new JsonObject();
                node["Index"] = JsonValue.CreateNumberValue(index);
                node["Color"] = JsonValue.CreateStringValue(preset.DarkPrimaryOverrides[index].ToString());
                darkPrimaryOverridesNode.Add(node);
            }
            data["DarkPrimaryOverrides"] = darkPrimaryOverridesNode;

            return data;
        }

        public Preset(string id, string name, Color lightRegionColor, Color darkRegionColor, Color lightBaseColor, Dictionary<int, Color> lightBaseOverrides, Color darkBaseColor, Dictionary<int, Color> darkBaseOverrides, Color lightPrimaryColor, Dictionary<int, Color> lightPrimaryOverrides, Color darkPrimaryColor, Dictionary<int, Color> darkPrimaryOverrides)
        {
            _id = id;
            _name = name;
            _lightRegionColor = lightRegionColor;
            _darkRegionColor = darkRegionColor;
            _lightBaseColor = lightBaseColor;
            _lightBaseOverrides = lightBaseOverrides;
            _darkBaseColor = darkBaseColor;
            _darkBaseOverrides = darkBaseOverrides;
            _lightPrimaryColor = lightPrimaryColor;
            _lightPrimaryOverrides = lightPrimaryOverrides;
            _darkPrimaryColor = darkPrimaryColor;
            _darkPrimaryOverrides = darkPrimaryOverrides;
        }

        public Preset(string id, string name, IControlPaletteModel model)
        {
            _id = id;
            _name = name;

            _lightRegionColor = model.LightRegion.ActiveColor;
            _darkRegionColor = model.DarkRegion.ActiveColor;

            _lightBaseColor = model.LightBase.BaseColor.ActiveColor;
            _lightBaseOverrides = new Dictionary<int, Color>();
            for (int i = 0; i < model.LightBase.Palette.Count; i++)
            {
                if (model.LightBase.Palette[i].UseCustomColor)
                {
                    _lightBaseOverrides.Add(i, model.LightBase.Palette[i].ActiveColor);
                }
            }

            _darkBaseColor = model.DarkBase.BaseColor.ActiveColor;
            _darkBaseOverrides = new Dictionary<int, Color>();
            for (int i = 0; i < model.DarkBase.Palette.Count; i++)
            {
                if (model.DarkBase.Palette[i].UseCustomColor)
                {
                    _darkBaseOverrides.Add(i, model.DarkBase.Palette[i].ActiveColor);
                }
            }

            _lightPrimaryColor = model.LightPrimary.BaseColor.ActiveColor;
            _lightPrimaryOverrides = new Dictionary<int, Color>();
            for (int i = 0; i < model.LightPrimary.Palette.Count; i++)
            {
                if (model.LightPrimary.Palette[i].UseCustomColor)
                {
                    _lightPrimaryOverrides.Add(i, model.LightPrimary.Palette[i].ActiveColor);
                }
            }

            _darkPrimaryColor = model.DarkPrimary.BaseColor.ActiveColor;
            _darkPrimaryOverrides = new Dictionary<int, Color>();
            for (int i = 0; i < model.DarkPrimary.Palette.Count; i++)
            {
                if (model.DarkPrimary.Palette[i].UseCustomColor)
                {
                    _darkPrimaryOverrides.Add(i, model.DarkPrimary.Palette[i].ActiveColor);
                }
            }
        }

        private readonly string _id;
        public string Id { get { return _id; } }

        private readonly string _name;
        public string Name { get { return _name; } }

        private readonly Color _lightRegionColor;
        public Color LightRegionColor { get { return _lightRegionColor; } }

        private readonly Color _darkRegionColor;
        public Color DarkRegionColor { get { return _darkRegionColor; } }

        private readonly Color _lightBaseColor;
        public Color LightBaseColor { get { return _lightBaseColor; } }

        private readonly Dictionary<int, Color> _lightBaseOverrides;
        public Dictionary<int, Color> LightBaseOverrides { get { return _lightBaseOverrides; } }

        private readonly Color _darkBaseColor;
        public Color DarkBaseColor { get { return _darkBaseColor; } }

        private readonly Dictionary<int, Color> _darkBaseOverrides;
        public Dictionary<int, Color> DarkBaseOverrides { get { return _darkBaseOverrides; } }

        private readonly Color _lightPrimaryColor;
        public Color LightPrimaryColor { get { return _lightPrimaryColor; } }

        private readonly Dictionary<int, Color> _lightPrimaryOverrides;
        public Dictionary<int, Color> LightPrimaryOverrides { get { return _lightPrimaryOverrides; } }

        private readonly Color _darkPrimaryColor;
        public Color DarkPrimaryColor { get { return _darkPrimaryColor; } }

        private readonly Dictionary<int, Color> _darkPrimaryOverrides;
        public Dictionary<int, Color> DarkPrimaryOverrides { get { return _darkPrimaryOverrides; } }

        public bool IsPresetActive(IControlPaletteModel model)
        {
            if (model == null)
            {
                return false;
            }
            if (model.LightRegion.ActiveColor != _lightRegionColor)
            {
                return false;
            }
            if (model.DarkRegion.ActiveColor != _darkRegionColor)
            {
                return false;
            }
            if (model.LightBase.BaseColor.ActiveColor != _lightBaseColor)
            {
                return false;
            }
            if (model.DarkBase.BaseColor.ActiveColor != _darkBaseColor)
            {
                return false;
            }
            if (model.LightPrimary.BaseColor.ActiveColor != _lightPrimaryColor)
            {
                return false;
            }
            if (model.DarkPrimary.BaseColor.ActiveColor != _darkPrimaryColor)
            {
                return false;
            }
            if (!CompareOverrides(_lightBaseOverrides, model.LightBase.Palette))
            {
                return false;
            }
            if (!CompareOverrides(_darkBaseOverrides, model.DarkBase.Palette))
            {
                return false;
            }
            if (!CompareOverrides(_lightPrimaryOverrides, model.LightPrimary.Palette))
            {
                return false;
            }
            if (!CompareOverrides(_darkPrimaryOverrides, model.DarkPrimary.Palette))
            {
                return false;
            }

            return true;
        }

        private bool CompareOverrides(Dictionary<int, Color> presetDictionary, IReadOnlyList<EditableColorPaletteEntry> palette)
        {
            if (presetDictionary == null || palette == null)
            {
                return false;
            }

            for (int i = 0; i < palette.Count; i++)
            {
                if (palette[i].UseCustomColor)
                {
                    if (!presetDictionary.ContainsKey(i) || presetDictionary[i] != palette[i].ActiveColor)
                    {
                        return false;
                    }
                }
            }

            var keys = presetDictionary.Keys;
            foreach (var index in keys)
            {
                if (index >= palette.Count || index < 0)
                {
                    return false;
                }
                if (!palette[index].UseCustomColor || palette[index].ActiveColor != presetDictionary[index])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
