//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Services;
using Windows.System;
using Windows.System.Power;
using Windows.UI.Composition;
using Windows.UI.ViewManagement;

namespace Telegram.Common
{
    public enum PowerSavingMode
    {
        Off,
        Auto
    }

    public enum PowerSavingStatus
    {
        Off,
        On
    }

    public partial class PowerSavingPolicy
    {
        private static bool m_isDisabledByPolicy;
        private static bool m_isPowerSavingMode;

        private static readonly bool m_energySaverStatusChangedRevokerValid;
        private static readonly CompositionCapabilities m_compositionCapabilities;
        private static readonly UISettings m_uiSettings;

        private static readonly DispatcherQueue m_dispatcher;


        static PowerSavingPolicy()
        {
            m_dispatcher = DispatcherQueue.GetForCurrentThread();

            try
            {
                PowerManager.EnergySaverStatusChanged += PowerManager_EnergySaverStatusChanged;
                m_energySaverStatusChangedRevokerValid = true;
            }
            catch
            {

            }

            m_compositionCapabilities = CompositionCapabilities.GetForCurrentView();
            m_compositionCapabilities.Changed += CompositionCapabilities_Changed;

            m_uiSettings = new UISettings();
            m_uiSettings.AdvancedEffectsEnabledChanged += UISettings_AdvancedEffectsEnabledChanged;

            m_areMaterialsEnabled = AreMaterialsEnabled;

            UpdatePolicy();
        }

        private static void PowerManager_EnergySaverStatusChanged(object sender, object e)
        {
            UpdatePolicyByDispatcher();
        }

        private static void CompositionCapabilities_Changed(CompositionCapabilities sender, object args)
        {
            UpdatePolicyByDispatcher();
        }

        private static void UISettings_AdvancedEffectsEnabledChanged(UISettings sender, object args)
        {
            UpdatePolicyByDispatcher();
        }

        private static void UpdatePolicyByDispatcher()
        {
            if (m_dispatcher.HasThreadAccess)
            {
                UpdatePolicy();
            }
            else
            {
                m_dispatcher.TryEnqueue(DispatcherQueuePriority.Normal, UpdatePolicy);
            }
        }

        // Internal MUX logic: https://github.com/microsoft/microsoft-ui-xaml/blob/main/dev/Lights/MaterialHelper.cpp
        private static void UpdatePolicy()
        {
            var isEnergySaverMode = !m_energySaverStatusChangedRevokerValid || PowerManager.EnergySaverStatus == EnergySaverStatus.On;
            var areEffectsFast = m_compositionCapabilities != null && m_compositionCapabilities.AreEffectsFast();
            var advancedEffectsEnabled = m_uiSettings == null || m_uiSettings.AdvancedEffectsEnabled;

            // This applies only to visual effects
            var isDisabledByPolicy = Mode switch
            {
                PowerSavingMode.Auto => isEnergySaverMode || !areEffectsFast || !advancedEffectsEnabled,
                _ => false
            };

            // This applies to all the rest
            var isPowerSavingMode = Mode switch
            {
                PowerSavingMode.Auto => isEnergySaverMode,
                _ => false
            };

            if (m_isDisabledByPolicy != isDisabledByPolicy)
            {
                m_isDisabledByPolicy = isDisabledByPolicy;
                m_isPowerSavingMode = isPowerSavingMode;
                Changed?.Invoke(null, EventArgs.Empty);

                RaiseAreMaterialsEnabledChanged();
            }
            else if (m_isPowerSavingMode != isPowerSavingMode)
            {
                m_isPowerSavingMode = isPowerSavingMode;
                Changed?.Invoke(null, EventArgs.Empty);
            }
        }

        public static bool IsSupported => m_energySaverStatusChangedRevokerValid && PowerManager.BatteryStatus != BatteryStatus.NotPresent;

        public static PowerSavingStatus Status => m_isPowerSavingMode ? PowerSavingStatus.On : PowerSavingStatus.Off;

        public static bool IsDisabledByPolicy => m_isDisabledByPolicy;

        public static PowerSavingMode Mode
        {
            get => AppSettings.IsPowerSavingEnabled ? PowerSavingMode.Auto : PowerSavingMode.Off;
            set
            {
                AppSettings.IsPowerSavingEnabled = value == PowerSavingMode.Auto;
                UpdatePolicyByDispatcher();
            }
        }

        public static event EventHandler Changed;

        private static bool m_areMaterialsEnabled;
        public static bool AreMaterialsEnabled
        {
            get => AppSettings.AreMaterialsEnabled && !m_isDisabledByPolicy;
            set
            {
                AppSettings.AreMaterialsEnabled = value;
                RaiseAreMaterialsEnabledChanged();
            }
        }

        private static void RaiseAreMaterialsEnabledChanged()
        {
            if (m_areMaterialsEnabled != AreMaterialsEnabled)
            {
                m_areMaterialsEnabled = AreMaterialsEnabled;
                NightModeService.Current.Update(false, false);
            }
        }

        public static bool AutoPlayVideos
        {
            get => AppSettings.AutoPlayVideos && !m_isPowerSavingMode;
            set
            {
                AppSettings.AutoPlayVideos = value;
                RaisePropertyChanged();
            }
        }

        public static bool AutoPlayAnimations
        {
            get => AppSettings.AutoPlayAnimations && !m_isPowerSavingMode;
            set
            {
                AppSettings.AutoPlayAnimations = value;
                RaisePropertyChanged();
            }
        }

        public static bool AutoPlayStickers
        {
            get => AppSettings.AutoPlayStickers && !m_isPowerSavingMode;
            set
            {
                AppSettings.AutoPlayStickers = value;
                RaisePropertyChanged();
            }
        }

        public static bool AutoPlayStickersInChats
        {
            get => AppSettings.AutoPlayStickersInChats && !m_isPowerSavingMode;
            set
            {
                AppSettings.AutoPlayStickersInChats = value;
                RaisePropertyChanged();
            }
        }

        public static bool AutoPlayEmoji
        {
            get => AppSettings.AutoPlayEmoji && !m_isPowerSavingMode;
            set
            {
                AppSettings.AutoPlayEmoji = value;
                RaisePropertyChanged();
            }
        }

        public static bool AutoPlayEmojiInChats
        {
            get => AppSettings.AutoPlayEmojiInChats && !m_isPowerSavingMode;
            set
            {
                AppSettings.AutoPlayEmojiInChats = value;
                RaisePropertyChanged();
            }
        }

        public static bool AreSmoothTransitionsEnabled
        {
            get => AppSettings.AreSmoothTransitionsEnabled && m_uiSettings.AnimationsEnabled && !m_isPowerSavingMode;
            set
            {
                AppSettings.AreSmoothTransitionsEnabled = value;
                RaisePropertyChanged();
            }
        }

        public static bool AreCallsAnimated
        {
            get => AppSettings.AreCallsAnimated && !m_isPowerSavingMode;
            set
            {
                AppSettings.AreCallsAnimated = value;
                RaisePropertyChanged();
            }
        }

        private static void RaisePropertyChanged()
        {

        }
    }
}
