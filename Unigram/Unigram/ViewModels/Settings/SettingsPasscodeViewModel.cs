using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Template10.Common;
using Unigram.Common;
using Unigram.Controls.Views;
using Unigram.Services;
using Unigram.Services.Updates;
using Windows.UI.Xaml;
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
                var dialog = new SettingsPasscodeInputView();
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
            var dialog = new SettingsPasscodeInputView();
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

            var dialog = new ContentDialog { Style = BootStrapper.Current.Resources["ModernContentDialogStyle"] as Style };
            var stack = new StackPanel();
            stack.Margin = new Thickness(12, 16, 12, 0);
            stack.Children.Add(new RadioButton { Tag = 0,           Content = Locale.FormatAutoLock(0),           IsChecked = timeout == 0 });
            stack.Children.Add(new RadioButton { Tag = 1 * 60,      Content = Locale.FormatAutoLock(1 * 60),      IsChecked = timeout == 1 * 60 });
            stack.Children.Add(new RadioButton { Tag = 5 * 60,      Content = Locale.FormatAutoLock(5 * 60),      IsChecked = timeout == 5 * 60 });
            stack.Children.Add(new RadioButton { Tag = 1 * 60 * 60, Content = Locale.FormatAutoLock(1 * 60 * 60), IsChecked = timeout == 1 * 60 * 60 });
            stack.Children.Add(new RadioButton { Tag = 5 * 60 * 60, Content = Locale.FormatAutoLock(5 * 60 * 60), IsChecked = timeout == 5 * 60 * 60 });

            dialog.Title = Strings.Resources.AutoLock;
            dialog.Content = stack;
            dialog.PrimaryButtonText = Strings.Resources.OK;
            dialog.SecondaryButtonText = Strings.Resources.Cancel;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                var mode = 1;
                foreach (RadioButton current in stack.Children)
                {
                    if (current.IsChecked == true)
                    {
                        mode = (int)current.Tag;
                        break;
                    }
                }

                AutolockTimeout = mode;
                InactivityHelper.Initialize(mode);
            }
        }
    }
}
