//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Telegram.Services
{
    // Night mode is behaviour, not a setting: a timer, a system-theme subscription and a broadcast
    // to every open window. AppearanceSettings keeps the values it reads, and nothing else.
    public partial class NightModeService
    {
        private static NightModeService _current;
        public static NightModeService Current => _current ??= new NightModeService();

        private readonly UISettings _uiSettings;
        private readonly Timer _nightModeTimer;

        private NightModeService()
        {
            _uiSettings = new UISettings();
            _uiSettings.ColorValuesChanged += OnColorValuesChanged;

            _nightModeTimer = new Timer(CheckNightModeConditions, null, Timeout.Infinite, Timeout.Infinite);
            UpdateTimer();
        }

        private void OnColorValuesChanged(UISettings sender, object args)
        {
            Update(null);
        }

        public void UpdateTimer()
        {
            if (AppSettings.Appearance.NightMode == NightMode.Scheduled && AppSettings.Appearance.RequestedTheme == TelegramTheme.Light)
            {
                var start = DateTime.Today;
                var end = DateTime.Today;

                if (AppSettings.Appearance.IsLocationBased && AppSettings.Appearance.Location.Latitude != 0 && AppSettings.Appearance.Location.Longitude != 0)
                {
                    var t = SunDate.CalculateSunriseSunset(AppSettings.Appearance.Location.Latitude, AppSettings.Appearance.Location.Longitude);
                    var sunrise = new TimeSpan(t[0] / 60, t[0] - (t[0] / 60) * 60, 0);
                    var sunset = new TimeSpan(t[1] / 60, t[1] - (t[1] / 60) * 60, 0);

                    start = start.Add(sunset);
                    end = end.Add(sunrise);

                    if (sunrise > DateTime.Now.TimeOfDay)
                    {
                        start = start.AddDays(-1);
                    }
                    else if (sunrise < sunset)
                    {
                        end = end.AddDays(1);
                    }
                }
                else
                {
                    start = start.Add(AppSettings.Appearance.From);
                    end = end.Add(AppSettings.Appearance.To);

                    if (AppSettings.Appearance.From < DateTime.Now.TimeOfDay)
                    {
                        start = start.AddDays(-1);
                    }
                    else if (AppSettings.Appearance.To < AppSettings.Appearance.From)
                    {
                        end = end.AddDays(1);
                    }
                }

                var now = DateTime.Now;
                if (now < start)
                {
                    _nightModeTimer.Change(start - now, TimeSpan.Zero);
                }
                else if (now < end)
                {
                    _nightModeTimer.Change(end - now, TimeSpan.Zero);
                }
                else
                {
                    _nightModeTimer.Change(Timeout.Infinite, Timeout.Infinite);
                }
            }
            else
            {
                _nightModeTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }

        private void CheckNightModeConditions(object state)
        {
            Update(false);
        }

        public async void Update(bool? force = false, bool updateBackground = true, bool updateEmojiSet = false)
        {
            // Same theme:
            // - false: update dictionaries
            // - null:  do nothing
            // - true:  as different theme
            // Different theme:
            // - false: update dictionaries, switch theme
            // - null:  switch theme
            // - true.  update dictionaries, double switch theme

            UpdateTimer();

            var conditions = CheckNightModeConditions();
            var theme = conditions == null
                ? GetActualTheme()
                : conditions == true
                ? ElementTheme.Dark
                : ElementTheme.Light;

            await WindowContext.ForEachAsync(window =>
            {
                if (force is not null)
                {
                    if (updateBackground)
                    {
                        window.Theme.UpdateEmojiSet();
                    }

                    window.Theme.Update(theme);
                }

                if (window.ActualTheme != theme || force is true)
                {
                    window.UpdateTitleBar();

                    // This should be no longer needed
                    if (force is true)
                    {
                        window.RequestedTheme = theme == ElementTheme.Dark
                            ? ElementTheme.Light
                            : ElementTheme.Dark;
                    }

                    window.RequestedTheme = theme;

                    foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(window.XamlRoot))
                    {
                        if (popup.Child is ContentPopup contentPopup && contentPopup.RequestedTheme != ElementTheme.Default)
                        {
                            // This should be no longer needed
                            if (force is true)
                            {
                                contentPopup.RequestedTheme = theme == ElementTheme.Dark
                                    ? ElementTheme.Light
                                    : ElementTheme.Dark;
                            }

                            contentPopup.RequestedTheme = theme;
                        }
                    }
                }
            });

            if (updateBackground)
            {
                var aggregator = LifetimeService.Current.ActiveItem.Resolve<IEventAggregator>();
                var clientService = LifetimeService.Current.ActiveItem.Resolve<IClientService>();

                if (aggregator != null && clientService != null)
                {
                    var dark = theme == ElementTheme.Dark;
                    var background = clientService.GetDefaultBackground(dark);

                    aggregator.Publish(new UpdateDefaultBackground(dark, background));
                }
            }
        }

        public bool? CheckNightModeConditions()
        {
            if (AppSettings.Appearance.ForceNightMode)
            {
                return true;
            }
            else if (AppSettings.Appearance.NightMode == NightMode.Scheduled && AppSettings.Appearance.RequestedTheme == TelegramTheme.Light)
            {
                TimeSpan start = default;
                TimeSpan end = default;

                if (AppSettings.Appearance.IsLocationBased && AppSettings.Appearance.Location.Latitude != 0 && AppSettings.Appearance.Location.Longitude != 0)
                {
                    var t = SunDate.CalculateSunriseSunset(AppSettings.Appearance.Location.Latitude, AppSettings.Appearance.Location.Longitude);
                    start = new TimeSpan(t[1] / 60, t[1] - (t[1] / 60) * 60, 0);
                    end = new TimeSpan(t[0] / 60, t[0] - (t[0] / 60) * 60, 0);
                }
                else
                {
                    start = start.Add(AppSettings.Appearance.From);
                    end = end.Add(AppSettings.Appearance.To);
                }

                return DateTime.Now.TimeOfDay.IsBetween(start, end);
            }
            else if (AppSettings.Appearance.NightMode == NightMode.System)
            {
                return AppSettings.Appearance.GetSystemTheme() == TelegramTheme.Dark;
            }

            return null;
        }

        public ElementTheme GetCalculatedElementTheme()
        {
            var conditions = CheckNightModeConditions();
            var theme = conditions == null
                ? GetActualTheme()
                : conditions == true
                ? ElementTheme.Dark
                : ElementTheme.Light;

            return theme;
        }

        public ElementTheme GetActualTheme()
        {
            var theme = AppSettings.Appearance.RequestedTheme;
            return theme == TelegramTheme.Dark
                ? ElementTheme.Dark
                : ElementTheme.Light;
        }

        public bool IsLightTheme()
        {
            return GetCalculatedApplicationTheme() == ApplicationTheme.Light;
        }

        public bool IsDarkTheme()
        {
            return GetCalculatedApplicationTheme() == ApplicationTheme.Dark;
        }

        public ApplicationTheme GetCalculatedApplicationTheme()
        {
            var conditions = CheckNightModeConditions();
            var theme = conditions == null
                ? GetApplicationTheme()
                : conditions == true
                ? ApplicationTheme.Dark
                : ApplicationTheme.Light;

            return theme;
        }

        public ApplicationTheme GetApplicationTheme()
        {
            var theme = AppSettings.Appearance.RequestedTheme;
            return theme == TelegramTheme.Dark
                ? ApplicationTheme.Dark
                : ApplicationTheme.Light;
        }
    }
}
