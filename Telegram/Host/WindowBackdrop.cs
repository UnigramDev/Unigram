//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Navigation;
using Windows.System;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Telegram.Host
{
    /// <summary>
    /// Mica behind a window, with WinUI 2's <c>BackdropMaterial</c> fallbacks - which is the part
    /// worth copying. The effect itself is one DWM call; what makes it behave is knowing when not
    /// to use it, and what to put there instead.
    ///
    /// The rules, read out of BackdropMaterial.cpp and MicaController.cpp:
    ///
    /// - Not supported at all -> a solid colour, from the theme.
    /// - High contrast -> the system's own background colour, never the Mica tint.
    /// - Transparency effects off in Settings -> the same solid fallback.
    /// - Supported and on -> the target's Background goes fully **transparent**, deliberately, so
    ///   that hit testing behaves the same with the material as without it.
    ///
    /// The fallback colours are Mica's own tints, so the window looks the same shape either way:
    /// #F3F3F3 light, #202020 dark - MicaController::sc_lightThemeColor and sc_darkThemeColor.
    ///
    /// One of these per window. It holds the UISettings and AccessibilitySettings instances rather
    /// than fetching them per call: their events stop firing if the object is collected.
    /// </summary>
    internal sealed class WindowBackdrop
    {
        private static readonly Color LightTint = Color.FromArgb(255, 243, 243, 243);
        private static readonly Color DarkTint = Color.FromArgb(255, 32, 32, 32);

        private readonly IntPtr _hwnd;
        private readonly WindowPresenter _presenter;

        private readonly DispatcherQueue _queue = DispatcherQueue.GetForCurrentThread();

        private readonly UISettings _settings = new();
        private readonly AccessibilitySettings _accessibility = new();

        private bool _active;

        public WindowBackdrop(IntPtr hwnd, WindowPresenter presenter)
        {
            _hwnd = hwnd;
            _presenter = presenter;

            _settings.AdvancedEffectsEnabledChanged += OnAdvancedEffectsEnabledChanged;
            _accessibility.HighContrastChanged += OnHighContrastChanged;
            _presenter.ActualThemeChanged += OnActualThemeChanged;

            Update();
        }

        public void Release()
        {
            _settings.AdvancedEffectsEnabledChanged -= OnAdvancedEffectsEnabledChanged;
            _accessibility.HighContrastChanged -= OnHighContrastChanged;
            _presenter.ActualThemeChanged -= OnActualThemeChanged;

            if (_active)
            {
                Win32.SetBackdrop(_hwnd, false);
                _active = false;
            }
        }

        // These arrive on a system thread, so they are marshalled before touching XAML.
        private void OnAdvancedEffectsEnabledChanged(UISettings sender, object args)
        {
            Post();
        }

        private void OnHighContrastChanged(AccessibilitySettings sender, object args)
        {
            Post();
        }

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            Update();
        }

        private void Post()
        {
            if (_queue == null || !_queue.TryEnqueue(Update))
            {
                Update();
            }
        }

        private void Update()
        {
            // Before the backdrop, and whether or not it is on: this is what tells DWM which tint
            // to draw Mica in, and what colours the frame it still draws on three sides. Left
            // alone it follows the system theme, so an app forced to dark against a light system
            // gets a light Mica behind a dark window.
            Win32.SetDarkMode(_hwnd, _presenter.ActualTheme == ElementTheme.Dark);

            var wanted = Win32.IsBackdropSupported
                && !_accessibility.HighContrast
                && _settings.AdvancedEffectsEnabled;

            if (wanted != _active)
            {
                // The XAML backstop only has to come off once per thread, and it never goes back
                // on: turning it off is what lets the backdrop reach the client area at all.
                if (wanted)
                {
                    WindowPrivate.TrySetTransparentBackground(true);
                }

                _active = Win32.SetBackdrop(_hwnd, wanted) && wanted;
            }

            _presenter.Background = new SolidColorBrush(_active ? Colors.Transparent : Fallback());
        }

        /// <summary>
        /// High contrast wins over the theme: the system's background colour is the only one that
        /// respects the user's scheme. Otherwise Mica's own tint, so that turning the effect off
        /// does not change the window's colour, only its depth.
        /// </summary>
        private Color Fallback()
        {
            if (_accessibility.HighContrast)
            {
                return _settings.GetColorValue(UIColorType.Background);
            }

            return _presenter.ActualTheme == ElementTheme.Dark ? DarkTint : LightTint;
        }
    }
}
