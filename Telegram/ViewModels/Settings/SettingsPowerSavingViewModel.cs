//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Windows.Foundation.Metadata;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings
{
    public partial class SettingsPowerSavingViewModel : ViewModelBase
    {
        public SettingsPowerSavingViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        public bool IsAutoSupported => PowerSavingPolicy.IsSupported;

        public bool IsAutoDisabled => PowerSavingPolicy.Status == PowerSavingStatus.Off;

        public bool IsAutoEnabled
        {
            get => PowerSavingPolicy.Mode == PowerSavingMode.Auto;
            set => PowerSavingPolicy.Mode = value ? PowerSavingMode.Auto : PowerSavingMode.Off;
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            PowerSavingPolicy.Changed += OnPowerSavingPolicyChanged;

            if (ApiInformation.IsEventPresent("Windows.UI.ViewManagement.UISettings", "AnimationsEnabledChanged"))
            {
                BootStrapper.Current.UISettings.AnimationsEnabledChanged += OnAnimationsEnabledChanged;
            }
            else
            {
                NavigationService.Window.Activated += OnWindowActivated;
            }

            return Task.CompletedTask;
        }

        protected override void OnNavigatedFrom(NavigationState suspensionState, bool suspending)
        {
            PowerSavingPolicy.Changed -= OnPowerSavingPolicyChanged;

            if (ApiInformation.IsEventPresent("Windows.UI.ViewManagement.UISettings", "AnimationsEnabledChanged"))
            {
                BootStrapper.Current.UISettings.AnimationsEnabledChanged -= OnAnimationsEnabledChanged;
            }
            else
            {
                NavigationService.Window.Activated -= OnWindowActivated;
            }
        }

        private void OnPowerSavingPolicyChanged(object sender, EventArgs e)
        {
            BeginOnUIThread(() =>
            {
                RaisePropertyChanged(nameof(IsAutoDisabled));
                RaisePropertyChanged(string.Empty);
            });
        }

        private void OnAnimationsEnabledChanged(UISettings sender, UISettingsAnimationsEnabledChangedEventArgs args)
        {
            BeginOnUIThread(() =>
            {
                RaisePropertyChanged(nameof(AreAnimationsEnabled));
            });
        }

        private void OnWindowActivated(object sender, WindowActivatedEventArgs e)
        {
            RaisePropertyChanged(nameof(AreAnimationsEnabled));
        }

        #region Stickers

        public bool? AutoPlayStickersAll
        {
            get => PowerSavingPolicy.AutoPlayStickers && PowerSavingPolicy.AutoPlayStickersInChats ? true : PowerSavingPolicy.AutoPlayStickers || PowerSavingPolicy.AutoPlayStickersInChats ? null : false;
            set
            {
                if (PowerSavingPolicy.Status == PowerSavingStatus.On || value == null)
                {
                    return;
                }

                PowerSavingPolicy.AutoPlayStickers = PowerSavingPolicy.AutoPlayStickersInChats = value ?? false;
                RaiseStickersPropertyChanged();
                RaisePropertyChanged(nameof(AutoPlayStickers));
                RaisePropertyChanged(nameof(AutoPlayStickersInChats));
            }
        }

        public bool AutoPlayStickers
        {
            get => PowerSavingPolicy.AutoPlayStickers;
            set
            {
                if (PowerSavingPolicy.Status == PowerSavingStatus.On)
                {
                    return;
                }

                PowerSavingPolicy.AutoPlayStickers = value;
                RaiseStickersPropertyChanged();
                RaisePropertyChanged(nameof(AutoPlayStickersAll));
            }
        }

        public bool AutoPlayStickersInChats
        {
            get => PowerSavingPolicy.AutoPlayStickersInChats;
            set
            {
                if (PowerSavingPolicy.Status == PowerSavingStatus.On)
                {
                    return;
                }

                PowerSavingPolicy.AutoPlayStickersInChats = value;
                RaiseStickersPropertyChanged();
                RaisePropertyChanged(nameof(AutoPlayStickersAll));
            }
        }

        public string AutoPlayStickersCount
        {
            get
            {
                var count = 0;
                if (AutoPlayStickers)
                    count++;
                if (AutoPlayStickersInChats)
                    count++;

                return $"{count}/2";
            }
        }

        private void RaiseStickersPropertyChanged([CallerMemberName] string propertyName = null)
        {
            RaisePropertyChanged(propertyName);
            RaisePropertyChanged(nameof(AutoPlayStickersCount));
        }

        #endregion

        #region Emoji

        public bool? AutoPlayEmojiAll
        {
            get => PowerSavingPolicy.AutoPlayEmoji && PowerSavingPolicy.AutoPlayEmojiInChats ? true : PowerSavingPolicy.AutoPlayEmoji || PowerSavingPolicy.AutoPlayEmojiInChats ? null : false;
            set
            {
                if (PowerSavingPolicy.Status == PowerSavingStatus.On || value == null)
                {
                    return;
                }

                PowerSavingPolicy.AutoPlayEmoji = PowerSavingPolicy.AutoPlayEmojiInChats = value ?? false;
                RaiseEmojiPropertyChanged();
                RaisePropertyChanged(nameof(AutoPlayEmoji));
                RaisePropertyChanged(nameof(AutoPlayEmojiInChats));
            }
        }

        public bool AutoPlayEmoji
        {
            get => PowerSavingPolicy.AutoPlayEmoji;
            set
            {
                if (PowerSavingPolicy.Status == PowerSavingStatus.On)
                {
                    return;
                }

                PowerSavingPolicy.AutoPlayEmoji = value;
                RaiseEmojiPropertyChanged();
                RaisePropertyChanged(nameof(AutoPlayEmojiAll));
            }
        }

        public bool AutoPlayEmojiInChats
        {
            get => PowerSavingPolicy.AutoPlayEmojiInChats;
            set
            {
                if (PowerSavingPolicy.Status == PowerSavingStatus.On)
                {
                    return;
                }

                PowerSavingPolicy.AutoPlayEmojiInChats = value;
                RaiseEmojiPropertyChanged();
                RaisePropertyChanged(nameof(AutoPlayEmojiAll));
            }
        }

        public string AutoPlayEmojiCount
        {
            get
            {
                var count = 0;
                if (AutoPlayEmoji)
                    count++;
                if (AutoPlayEmojiInChats)
                    count++;

                return $"{count}/2";
            }
        }

        private void RaiseEmojiPropertyChanged([CallerMemberName] string propertyName = null)
        {
            RaisePropertyChanged(propertyName);
            RaisePropertyChanged(nameof(AutoPlayEmojiCount));
        }

        #endregion

        public bool AutoPlayAnimations
        {
            get => PowerSavingPolicy.AutoPlayAnimations;
            set
            {
                if (PowerSavingPolicy.Status == PowerSavingStatus.On)
                {
                    return;
                }

                PowerSavingPolicy.AutoPlayAnimations = value;
                RaisePropertyChanged();
            }
        }

        public bool AutoPlayVideos
        {
            get => PowerSavingPolicy.AutoPlayVideos;
            set
            {
                if (PowerSavingPolicy.Status == PowerSavingStatus.On)
                {
                    return;
                }

                PowerSavingPolicy.AutoPlayVideos = value;
                RaisePropertyChanged();
            }
        }

        public bool AreCallsAnimated
        {
            get => PowerSavingPolicy.AreCallsAnimated;
            set
            {
                PowerSavingPolicy.AreCallsAnimated = value;
                RaisePropertyChanged();
            }
        }

        public bool AreMaterialsEnabled
        {
            get => PowerSavingPolicy.AreMaterialsEnabled;
            set
            {
                PowerSavingPolicy.AreMaterialsEnabled = value;
                RaisePropertyChanged();
            }
        }

        public bool AreAnimationsEnabled
        {
            get => BootStrapper.Current.UISettings.AnimationsEnabled;
        }

        public bool AreSmoothTransitionsEnabled
        {
            get => PowerSavingPolicy.AreSmoothTransitionsEnabled;
            set
            {
                PowerSavingPolicy.AreSmoothTransitionsEnabled = value;
                RaisePropertyChanged();
            }
        }
    }
}
