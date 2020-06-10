using System.Collections.Generic;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Services;
using Unigram.Services.Updates;
using Unigram.Views.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsPasscodeViewModel : TLViewModelBase, IHandle<UpdatePasscodeLock>
    {
        private readonly IPasscodeService _passcodeService;

        public SettingsPasscodeViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, IPasscodeService passcodeService)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _passcodeService = passcodeService;

            ToggleCommand = new RelayCommand(ToggleExecute);
            EditCommand = new RelayCommand(EditExecute);
            AutolockCommand = new RelayCommand(AutolockExecute);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            Aggregator.Subscribe(this);
            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        public override Task OnNavigatedFromAsync(IDictionary<string, object> pageState, bool suspending)
        {
            Aggregator.Unsubscribe(this);
            return base.OnNavigatedFromAsync(pageState, suspending);
        }

        public void Handle(UpdatePasscodeLock update)
        {
            BeginOnUIThread(() =>
            {
                RaisePropertyChanged(() => IsEnabled);
                RaisePropertyChanged(() => AutolockTimeout);
                RaisePropertyChanged(() => IsBiometricsEnabled);
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
            get
            {
                return _passcodeService.AutolockTimeout;
            }
            set
            {
                _passcodeService.AutolockTimeout = value;
                RaisePropertyChanged();
            }
        }

        public bool IsBiometricsEnabled
        {
            get
            {
                return _passcodeService.IsBiometricsEnabled;
            }
            set
            {
                _passcodeService.IsBiometricsEnabled = value;
                RaisePropertyChanged();
            }
        }

        public RelayCommand ToggleCommand { get; }
        private async void ToggleExecute()
        {
            if (_passcodeService.IsEnabled)
            {
                _passcodeService.Reset();
            }
            else
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

        public RelayCommand AutolockCommand { get; }
        private async void AutolockExecute()
        {
            var timeout = AutolockTimeout + 0;

            var items = new[]
            {
                new SelectRadioItem(0,           Locale.FormatAutoLock(0),           timeout == 0),
                new SelectRadioItem(1 * 60,      Locale.FormatAutoLock(1 * 60),      timeout == 1 * 60),
                new SelectRadioItem(5 * 60,      Locale.FormatAutoLock(5 * 60),      timeout == 5 * 60),
                new SelectRadioItem(1 * 60 * 60, Locale.FormatAutoLock(1 * 60 * 60), timeout == 1 * 60 * 60),
                new SelectRadioItem(5 * 60 * 60, Locale.FormatAutoLock(5 * 60 * 60), timeout == 5 * 60 * 60)
            };

            var dialog = new SelectRadioPopup(items);
            dialog.Title = Strings.Resources.AutoLock;
            dialog.PrimaryButtonText = Strings.Resources.OK;
            dialog.SecondaryButtonText = Strings.Resources.Cancel;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary && dialog.SelectedIndex is int mode)
            {
                AutolockTimeout = mode;
                InactivityHelper.Initialize(mode);
            }
        }
    }
}
