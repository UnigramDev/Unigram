// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using FluentEditorShared.ColorPalette;
using System;
using System.Collections.Generic;
using Windows.Data.Json;
using Windows.UI.Xaml;
using FluentEditorShared.Utils;

namespace FluentEditor.ControlPalette
{
    public enum ColorTarget { Accent, ErrorText, AltHigh, AltLow, AltMedium, AltMediumHigh, AltMediumLow, BaseHigh, BaseLow, BaseMedium, BaseMediumHigh, BaseMediumLow, ChromeAltLow, ChromeBlackHigh, ChromeBlackLow, ChromeBlackMedium, ChromeBlackMediumLow, ChromeDisabledHigh, ChromeDisabledLow, ChromeGray, ChromeHigh, ChromeLow, ChromeMedium, ChromeMediumLow, ChromeWhite, ListLow, ListMedium }
    public enum ColorSource { LightRegion, DarkRegion, LightBase, DarkBase, LightPrimary, DarkPrimary, White, Black }

    public class ColorMapping
    {
        public static ColorMapping Parse(JsonObject data, IColorPaletteEntry lightRegion, IColorPaletteEntry darkRegion, ColorPalette lightBase, ColorPalette darkBase, ColorPalette lightPrimary, ColorPalette darkPrimary, IColorPaletteEntry white, IColorPaletteEntry black)
        {
            var target = data["Target"].GetEnum<ColorTarget>();
            var source = data["Source"].GetEnum<ColorSource>();
            int index = 0;
            if (data.ContainsKey("SourceIndex"))
            {
                index = data["SourceIndex"].GetInt();
            }

            switch (source)
            {
                case ColorSource.LightRegion:
                    return new ColorMapping(lightRegion, target);
                case ColorSource.DarkRegion:
                    return new ColorMapping(darkRegion, target);
                case ColorSource.LightBase:
                    return new ColorMapping(lightBase.Palette[index], target);
                case ColorSource.DarkBase:
                    return new ColorMapping(darkBase.Palette[index], target);
                case ColorSource.LightPrimary:
                    return new ColorMapping(lightPrimary.Palette[index], target);
                case ColorSource.DarkPrimary:
                    return new ColorMapping(darkPrimary.Palette[index], target);
                case ColorSource.White:
                    return new ColorMapping(white, target);
                case ColorSource.Black:
                    return new ColorMapping(black, target);
            }

            return null;
        }

        public static List<ColorMapping> ParseList(JsonArray data, IColorPaletteEntry lightRegion, IColorPaletteEntry darkRegion, ColorPalette lightBase, ColorPalette darkBase, ColorPalette lightPrimary, ColorPalette darkPrimary, IColorPaletteEntry white, IColorPaletteEntry black)
        {
            List<ColorMapping> retVal = new List<ColorMapping>();
            foreach (var node in data)
            {
                retVal.Add(ColorMapping.Parse(node.GetObject(), lightRegion, darkRegion, lightBase, darkBase, lightPrimary, darkPrimary, white, black));
            }
            return retVal;
        }

        public ColorMapping(IColorPaletteEntry source, ColorTarget targetColor)
        {
            _source = source;
            _targetColor = targetColor;
        }

        private readonly IColorPaletteEntry _source;
        public IColorPaletteEntry Source
        {
            get { return _source; }
        }

        private readonly ColorTarget _targetColor;
        public ColorTarget Target
        {
            get { return _targetColor; }
        }

        public ColorMappingInstance CreateInstance(ColorPaletteResources targetResources)
        {
            return new ColorMappingInstance(_source, _targetColor, targetResources);
        }
    }

    public class ColorMappingInstance : IDisposable
    {
        public ColorMappingInstance(IColorPaletteEntry source, ColorTarget targetColor, ColorPaletteResources targetResources)
        {
            _source = source;
            _targetColor = targetColor;
            _targetResources = targetResources;

            Apply();

            _source.ActiveColorChanged += Source_ActiveColorChanged;
        }

        private readonly IColorPaletteEntry _source;
        private readonly ColorTarget _targetColor;
        private readonly ColorPaletteResources _targetResources;

        private static object _linkMapLock = new object();
        private static Dictionary<FrameworkElement, bool> _updateInProgress = new Dictionary<FrameworkElement, bool>();

        private FrameworkElement _linkedElement;
        public FrameworkElement LinkedElement
        {
            set
            {
                lock (_linkMapLock)
                {
                    if(_linkedElement != null)
                    {
                        if(_updateInProgress.ContainsKey(_linkedElement))
                        {
                            _updateInProgress.Remove(_linkedElement);
                        }
                        _linkedElement.Unloaded -= _linkedElement_Unloaded;
                    }

                    if (_linkedElement != value)
                    {
                        _linkedElement = value;
                        _linkedElement.Unloaded += _linkedElement_Unloaded;
                    }
                }
            }
        }

        private void _linkedElement_Unloaded(object sender, RoutedEventArgs e)
        {
            lock(_linkMapLock)
            {
                if(_linkedElement != null)
                {
                    if (_updateInProgress.ContainsKey(_linkedElement))
                    {
                        _updateInProgress.Remove(_linkedElement);
                    }
                    _linkedElement.Unloaded -= _linkedElement_Unloaded;
                    _linkedElement = null;
                }
            }
        }

        private void ForceThemeUpdateInLinkedElement()
        {
            FrameworkElement element = null;
            lock (_linkMapLock)
            {
                if(_linkedElement == null)
                {
                    return;
                }
                element = _linkedElement;
                if (_updateInProgress.ContainsKey(element))
                {
                    if (_updateInProgress[element])
                    {
                        return;
                    }
                    else
                    {
                        _updateInProgress[element] = true;
                    }
                }
                else
                {
                    _updateInProgress.Add(element, true);
                }
            }

            _ = element.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (element.RequestedTheme == ElementTheme.Light)
                {
                    element.RequestedTheme = ElementTheme.Dark;
                    element.RequestedTheme = ElementTheme.Light;
                }
                else if (element.RequestedTheme == ElementTheme.Dark)
                {
                    element.RequestedTheme = ElementTheme.Light;
                    element.RequestedTheme = ElementTheme.Dark;
                }
                else
                {
                    element.RequestedTheme = ElementTheme.Light;
                    element.RequestedTheme = ElementTheme.Dark;
                    element.RequestedTheme = ElementTheme.Default;
                }

                lock (_linkMapLock)
                {
                    if (_updateInProgress.ContainsKey(element))
                    {
                        _updateInProgress[element] = false;
                    }
                }
            });
        }

        private void Source_ActiveColorChanged(IColorPaletteEntry obj)
        {
            Apply();
            ForceThemeUpdateInLinkedElement();
        }

        public void Dispose()
        {
            _source.ActiveColorChanged -= Source_ActiveColorChanged;
        }

        public void Apply()
        {
            if (_targetResources == null)
            {
                return;
            }
            switch (_targetColor)
            {
                case ColorTarget.Accent:
                    _targetResources.Accent = _source.ActiveColor;
                    break;
                case ColorTarget.ErrorText:
                    _targetResources.ErrorText = _source.ActiveColor;
                    break;
                case ColorTarget.AltHigh:
                    _targetResources.AltHigh = _source.ActiveColor;
                    break;
                case ColorTarget.AltLow:
                    _targetResources.AltLow = _source.ActiveColor;
                    break;
                case ColorTarget.AltMedium:
                    _targetResources.AltMedium = _source.ActiveColor;
                    break;
                case ColorTarget.AltMediumHigh:
                    _targetResources.AltMediumHigh = _source.ActiveColor;
                    break;
                case ColorTarget.AltMediumLow:
                    _targetResources.AltMediumLow = _source.ActiveColor;
                    break;
                case ColorTarget.BaseHigh:
                    _targetResources.BaseHigh = _source.ActiveColor;
                    break;
                case ColorTarget.BaseLow:
                    _targetResources.BaseLow = _source.ActiveColor;
                    break;
                case ColorTarget.BaseMedium:
                    _targetResources.BaseMedium = _source.ActiveColor;
                    break;
                case ColorTarget.BaseMediumHigh:
                    _targetResources.BaseMediumHigh = _source.ActiveColor;
                    break;
                case ColorTarget.BaseMediumLow:
                    _targetResources.BaseMediumLow = _source.ActiveColor;
                    break;
                case ColorTarget.ChromeAltLow:
                    _targetResources.ChromeAltLow = _source.ActiveColor;
                    break;
                case ColorTarget.ChromeBlackHigh:
                    _targetResources.ChromeBlackHigh = _source.ActiveColor;
                    break;
                case ColorTarget.ChromeBlackLow:
                    _targetResources.ChromeBlackLow = _source.ActiveColor;
                    break;
                case ColorTarget.ChromeBlackMedium:
                    _targetResources.ChromeBlackMedium = _source.ActiveColor;
                    break;
                case ColorTarget.ChromeBlackMediumLow:
                    _targetResources.ChromeBlackMediumLow = _source.ActiveColor;
                    break;
                case ColorTarget.ChromeDisabledHigh:
                    _targetResources.ChromeDisabledHigh = _source.ActiveColor;
                    break;
                case ColorTarget.ChromeDisabledLow:
                    _targetResources.ChromeDisabledLow = _source.ActiveColor;
                    break;
                case ColorTarget.ChromeGray:
                    _targetResources.ChromeGray = _source.ActiveColor;
                    break;
                case ColorTarget.ChromeHigh:
                    _targetResources.ChromeHigh = _source.ActiveColor;
                    break;
                case ColorTarget.ChromeLow:
                    _targetResources.ChromeLow = _source.ActiveColor;
                    break;
                case ColorTarget.ChromeMedium:
                    _targetResources.ChromeMedium = _source.ActiveColor;
                    break;
                case ColorTarget.ChromeMediumLow:
                    _targetResources.ChromeMediumLow = _source.ActiveColor;
                    break;
                case ColorTarget.ChromeWhite:
                    _targetResources.ChromeWhite = _source.ActiveColor;
                    break;
                case ColorTarget.ListLow:
                    _targetResources.ListLow = _source.ActiveColor;
                    break;
                case ColorTarget.ListMedium:
                    _targetResources.ListMedium = _source.ActiveColor;
                    break;
            }
        }
    }
}
