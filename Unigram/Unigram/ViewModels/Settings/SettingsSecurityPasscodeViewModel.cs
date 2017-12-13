using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Template10.Common;
using Unigram.Common;
using Unigram.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.ViewModels.Settings
{
    public class SettingsSecurityPasscodeViewModel : UnigramViewModelBase
    {
        private readonly IPasscodeService _passcodeService;

        public SettingsSecurityPasscodeViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IPasscodeService passcodeService)
            : base(protoService, cacheService, aggregator)
        {
            _passcodeService = passcodeService;

            AutolockCommand = new RelayCommand(AutolockExecute);
        }

        public IPasscodeService Passcode => _passcodeService;

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

        public RelayCommand AutolockCommand { get; }
        private async void AutolockExecute()
        {
            var timeout = AutolockTimeout + 0;

            var dialog = new ContentDialog { Style = BootStrapper.Current.Resources["ModernContentDialogStyle"] as Style };
            var stack = new StackPanel();
            stack.Margin = new Thickness(12, 16, 12, 0);
            stack.Children.Add(new RadioButton { Tag = 0,           Content = LocaleHelper.FormatAutoLock(0),           IsChecked = timeout == 0 });
            stack.Children.Add(new RadioButton { Tag = 1 * 60,      Content = LocaleHelper.FormatAutoLock(1 * 60),      IsChecked = timeout == 1 * 60 });
            stack.Children.Add(new RadioButton { Tag = 5 * 60,      Content = LocaleHelper.FormatAutoLock(5 * 60),      IsChecked = timeout == 5 * 60 });
            stack.Children.Add(new RadioButton { Tag = 1 * 60 * 60, Content = LocaleHelper.FormatAutoLock(1 * 60 * 60), IsChecked = timeout == 1 * 60 * 60 });
            stack.Children.Add(new RadioButton { Tag = 5 * 60 * 60, Content = LocaleHelper.FormatAutoLock(5 * 60 * 60), IsChecked = timeout == 5 * 60 * 60 });

            dialog.Title = Strings.Android.AutoLock;
            dialog.Content = stack;
            dialog.PrimaryButtonText = Strings.Android.OK;
            dialog.SecondaryButtonText = Strings.Android.Cancel;

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
