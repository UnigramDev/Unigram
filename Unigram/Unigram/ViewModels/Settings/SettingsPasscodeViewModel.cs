using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.Services.Updates;
using Unigram.Views.Popups;
using Unigram.Views.Settings.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsPasscodeViewModel : TLViewModelBase
        //, IHandle<UpdatePasscodeLock>
    {
        private readonly IPasscodeService _passcodeService;

        public SettingsPasscodeViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, IPasscodeService passcodeService)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _passcodeService = passcodeService;

            ToggleCommand = new RelayCommand(ToggleExecute);
            EditCommand = new RelayCommand(EditExecute);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            Subscribe();
            return base.OnNavigatedToAsync(parameter, mode, state);
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

        public List<SettingsOptionItem<int>> AutolockTimeoutOptions => new List<SettingsOptionItem<int>>
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

        public RelayCommand ToggleCommand { get; }
        private async void ToggleExecute()
        {
            await ToggleAsync();
        }

        public async Task<bool> ToggleAsync()
        {
            if (_passcodeService.IsEnabled)
            {
                var confirm = await MessagePopup.ShowAsync(Strings.Resources.DisablePasscodeConfirmMessage, Strings.Resources.DisablePasscode, Strings.Resources.DisablePasscodeTurnOff, Strings.Resources.Cancel, true);
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

                var confirm = await popup.ShowQueuedAsync();
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

        public RelayCommand EditCommand { get; }
        private async void EditExecute()
        {
            var timeout = _passcodeService.AutolockTimeout + 0;
            var dialog = new SettingsPasscodeInputPopup();
            dialog.IsSimple = _passcodeService.IsSimple;

            var confirm = await dialog.ShowQueuedAsync();
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
