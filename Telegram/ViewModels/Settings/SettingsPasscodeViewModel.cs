//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Services;
using Telegram.Services.Updates;
using Telegram.Views.Popups;
using Telegram.Views.Settings.Popups;
using Windows.UI.Xaml.Controls;

namespace Telegram.ViewModels.Settings
{
    public class SettingsPasscodeViewModel : TLViewModelBase
        , IHandle
    //, IHandle<UpdatePasscodeLock>
    {
        private readonly IPasscodeService _passcodeService;

        public SettingsPasscodeViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, IPasscodeService passcodeService)
            : base(clientService, settingsService, aggregator)
        {
            _passcodeService = passcodeService;
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdatePasscodeLock>(this, Handle);
        }

        public void Handle(UpdatePasscodeLock update)
        {
            BeginOnUIThread(() =>
            {
                RaisePropertyChanged(nameof(IsEnabled));
                RaisePropertyChanged(nameof(AutolockTimeout));
                RaisePropertyChanged(nameof(IsBiometricsEnabled));
            });
        }

        public bool IsEnabled
        {
            get
            {
                return _passcodeService.IsEnabled;
            }
        }

        public int AutolockTimeout
        {
            get => Array.IndexOf(_autolockTimeoutIndexer, _passcodeService.AutolockTimeout);
            set
            {
                if (value >= 0 && value < _autolockTimeoutIndexer.Length && _passcodeService.AutolockTimeout != _autolockTimeoutIndexer[value])
                {
                    InactivityHelper.Initialize(_passcodeService.AutolockTimeout = _autolockTimeoutIndexer[value]);
                    RaisePropertyChanged();
                }
            }
        }

        private readonly int[] _autolockTimeoutIndexer = new[]
        {
            0,
            1 * 60,
            5 * 60,
            1 * 60 * 60,
            5 * 60 * 60
        };

        public List<SettingsOptionItem<int>> AutolockTimeoutOptions { get; } = new()
        {
                new SettingsOptionItem<int>(0,           Locale.FormatAutoLock(0)),
                new SettingsOptionItem<int>(1 * 60,      Locale.FormatAutoLock(1 * 60)),
                new SettingsOptionItem<int>(5 * 60,      Locale.FormatAutoLock(5 * 60)),
                new SettingsOptionItem<int>(1 * 60 * 60, Locale.FormatAutoLock(1 * 60 * 60)),
                new SettingsOptionItem<int>(5 * 60 * 60, Locale.FormatAutoLock(5 * 60 * 60))
        };

        public bool IsBiometricsEnabled
        {
            get => _passcodeService.IsBiometricsEnabled;
            set
            {
                _passcodeService.IsBiometricsEnabled = value;
                RaisePropertyChanged();
            }
        }

        public void Toggle()
        {
            _ = ToggleAsync();
        }

        public async Task<bool> ToggleAsync()
        {
            if (_passcodeService.IsEnabled)
            {
                var confirm = await ShowPopupAsync(Strings.Resources.DisablePasscodeConfirmMessage, Strings.Resources.DisablePasscode, Strings.Resources.DisablePasscodeTurnOff, Strings.Resources.Cancel, true);
                if (confirm == ContentDialogResult.Primary)
                {
                    _passcodeService.Reset();
                    NavigationService.GoBack();
                }
            }
            else
            {
                var timeout = _passcodeService.AutolockTimeout + 0;
                var popup = new SettingsPasscodeInputPopup();
                popup.IsSimple = _passcodeService.IsSimple;

                var confirm = await ShowPopupAsync(popup);
                if (confirm == ContentDialogResult.Primary)
                {
                    var passcode = popup.Passcode;
                    var simple = popup.IsSimple;
                    _passcodeService.Set(passcode, simple, timeout);

                    InactivityHelper.Initialize(timeout);
                    return true;
                }
            }

            return false;
        }

        public async void Edit()
        {
            var timeout = _passcodeService.AutolockTimeout + 0;
            var dialog = new SettingsPasscodeInputPopup();
            dialog.IsSimple = _passcodeService.IsSimple;

            var confirm = await ShowPopupAsync(dialog);
            if (confirm == ContentDialogResult.Primary)
            {
                var passcode = dialog.Passcode;
                var simple = dialog.IsSimple;
                _passcodeService.Set(passcode, simple, timeout);

                InactivityHelper.Initialize(timeout);
            }
        }
    }
}
