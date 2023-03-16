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

    public class PowerSavingPolicy
    {
        private static bool m_isDisabledByPolicy;

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

            var isDisabledByPolicy = Mode switch
            {
                PowerSavingMode.Auto => isEnergySaverMode || !areEffectsFast || !advancedEffectsEnabled,
                _ => false
            };

            if (m_isDisabledByPolicy != isDisabledByPolicy)
            {
                m_isDisabledByPolicy = isDisabledByPolicy;
                Changed?.Invoke(null, EventArgs.Empty);

                RaiseAreMaterialsEnabledChanged();
            }
        }

        public static bool IsSupported => m_energySaverStatusChangedRevokerValid && PowerManager.BatteryStatus != BatteryStatus.NotPresent;

        public static PowerSavingStatus Status => m_isDisabledByPolicy ? PowerSavingStatus.On : PowerSavingStatus.Off;

        public static PowerSavingMode Mode
        {
            get => SettingsService.Current.IsPowerSavingEnabled ? PowerSavingMode.Auto : PowerSavingMode.Off;
            set
            {
                SettingsService.Current.IsPowerSavingEnabled = value == PowerSavingMode.Auto;
                UpdatePolicyByDispatcher();
            }
        }

        public static event EventHandler Changed;

        private static bool m_areMaterialsEnabled;
        public static bool AreMaterialsEnabled
        {
            get => SettingsService.Current.AreMaterialsEnabled && !m_isDisabledByPolicy;
            set
            {
                SettingsService.Current.AreMaterialsEnabled = value;
                RaiseAreMaterialsEnabledChanged();
            }
        }

        private static void RaiseAreMaterialsEnabledChanged()
        {
            if (m_areMaterialsEnabled != AreMaterialsEnabled)
            {
                m_areMaterialsEnabled = AreMaterialsEnabled;
                SettingsService.Current.Appearance.UpdateNightMode(false, false);
            }
        }

        public static bool AutoPlayVideos
        {
            get => SettingsService.Current.AutoPlayVideos && !m_isDisabledByPolicy;
            set
            {
                SettingsService.Current.AutoPlayVideos = value;
                RaisePropertyChanged();
            }
        }

        public static bool AutoPlayAnimations
        {
            get => SettingsService.Current.AutoPlayAnimations && !m_isDisabledByPolicy;
            set
            {
                SettingsService.Current.AutoPlayAnimations = value;
                RaisePropertyChanged();
            }
        }

        public static bool AutoPlayStickers
        {
            get => SettingsService.Current.AutoPlayStickers && !m_isDisabledByPolicy;
            set
            {
                SettingsService.Current.AutoPlayStickers = value;
                RaisePropertyChanged();
            }
        }

        public static bool AutoPlayStickersInChats
        {
            get => SettingsService.Current.AutoPlayStickersInChats && !m_isDisabledByPolicy;
            set
            {
                SettingsService.Current.AutoPlayStickersInChats = value;
                RaisePropertyChanged();
            }
        }

        public static bool AutoPlayEmoji
        {
            get => SettingsService.Current.AutoPlayEmoji && !m_isDisabledByPolicy;
            set
            {
                SettingsService.Current.AutoPlayEmoji = value;
                RaisePropertyChanged();
            }
        }

        public static bool AutoPlayEmojiInChats
        {
            get => SettingsService.Current.AutoPlayEmojiInChats && !m_isDisabledByPolicy;
            set
            {
                SettingsService.Current.AutoPlayEmojiInChats = value;
                RaisePropertyChanged();
            }
        }

        public static bool AreSmoothTransitionsEnabled
        {
            get => SettingsService.Current.AreSmoothTransitionsEnabled && !m_isDisabledByPolicy;
            set
            {
                SettingsService.Current.AreSmoothTransitionsEnabled = value;
                RaisePropertyChanged();
            }
        }

        private static void RaisePropertyChanged()
        {

        }
    }
}
