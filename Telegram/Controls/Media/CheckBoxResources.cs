//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Services.Settings;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Media
{
    public class CheckBoxResources : ResourceDictionary
    {
        public CheckBoxResources()
        {

        }

        public CheckBoxResources(Color color)
        {
            Create(color);
        }

        private Color _color;
        public Color Color
        {
            get => _color;
            set => Create(_color = value);
        }

        private void Create(Color value)
        {
            Theme.AddCheckBoxPalette(this, value, false);
        }
    }

    public class RadioButtonResources : ResourceDictionary
    {
        public RadioButtonResources()
        {

        }

        public RadioButtonResources(Color color)
        {
            Create(color);
        }

        private Color _color;
        public Color Color
        {
            get => _color;
            set => Create(_color = value);
        }

        private void Create(Color value)
        {
            if (_lightNormal == null || _darkNormal == null)
            {
                AddRadioButtonPalette(this, value, false);
            }
            else
            {
                UpdateRadioButtonPalette(value, false);
            }
        }

        private static readonly string[] _radioButtonParts =
        {
            "RadioButtonOuterEllipseCheckedFill",
            "RadioButtonOuterEllipseCheckedStroke",
        };

        private void AddRadioButtonPalette(ResourceDictionary dictionary, Color accent, bool useShade)
        {
            dictionary.ThemeDictionaries["Light"] = CreateRadioButtonPalette(accent, TelegramTheme.Light, useShade);
            dictionary.ThemeDictionaries["Default"] = CreateRadioButtonPalette(accent, TelegramTheme.Dark, useShade);
        }

        private void UpdateRadioButtonPalette(Color accent, bool useShade)
        {
            UpdateAccentPalette(accent, TelegramTheme.Light, useShade);
            UpdateAccentPalette(accent, TelegramTheme.Dark, useShade);
        }

        private ResourceDictionary CreateRadioButtonPalette(Color accent, TelegramTheme requested, bool useShade)
        {
            return CreateAccentPalette(_radioButtonParts, accent, requested, useShade);
        }

        private SolidColorBrush _lightNormal;
        private SolidColorBrush _lightPointerOver;
        private SolidColorBrush _lightPressed;

        private SolidColorBrush _darkNormal;
        private SolidColorBrush _darkPointerOver;
        private SolidColorBrush _darkPressed;

        private ResourceDictionary CreateAccentPalette(string[] parts, Color accent, TelegramTheme requested, bool useShade)
        {
            var shade = useShade ? SystemAccentPalette.GetShade(accent, requested == TelegramTheme.Light
                ? AccentShade.Dark1
                : AccentShade.Light2) : accent;

            var normal = new SolidColorBrush(shade);
            var pointerOver = new SolidColorBrush(shade.WithAlpha(230));
            var pressed = new SolidColorBrush(shade.WithAlpha(204));

            var dictionary = new ResourceDictionary();

            foreach (var key in parts)
            {
                dictionary[key] = normal;
                dictionary[key + "PointerOver"] = pointerOver;
                dictionary[key + "Pressed"] = pressed;
            }

            if (requested == TelegramTheme.Light)
            {
                _lightNormal = normal;
                _lightPointerOver = pointerOver;
                _lightPressed = pressed;
            }
            else
            {
                _darkNormal = normal;
                _darkPointerOver = pointerOver;
                _darkPressed = pressed;
            }

            return dictionary;
        }

        private void UpdateAccentPalette(Color accent, TelegramTheme requested, bool useShade)
        {
            var shade = useShade ? SystemAccentPalette.GetShade(accent, requested == TelegramTheme.Light
                ? AccentShade.Dark1
                : AccentShade.Light2) : accent;

            if (requested == TelegramTheme.Light)
            {
                if (requested == TelegramTheme.Light)
                {
                    _lightNormal.Color = shade;
                    _lightPointerOver.Color = shade.WithAlpha(230);
                    _lightPressed.Color = shade.WithAlpha(204);
                }
                else
                {
                    _darkNormal.Color = shade;
                    _darkPointerOver.Color = shade.WithAlpha(230);
                    _darkPressed.Color = shade.WithAlpha(204);
                }
            }
        }
    }
}
