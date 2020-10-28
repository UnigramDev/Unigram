using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Navigation;
using Unigram.Services;
using Unigram.Views.Popups;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class DiagnosticsViewModel : TLViewModelBase
    {
        public DiagnosticsViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Options = new MvxObservableCollection<DiagnosticsOption>();
            Tags = new MvxObservableCollection<DiagnosticsTag>();

            VerbosityCommand = new RelayCommand(VerbosityExecute);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var calls = await ApplicationData.Current.LocalFolder.TryGetItemAsync("tgcalls.txt") as StorageFile;
            if (calls != null)
            {
                var basic = await calls.GetBasicPropertiesAsync();
                LogCallsSize = basic.Size;
            }

            var log = await ApplicationData.Current.LocalFolder.TryGetItemAsync("tdlib_log.txt") as StorageFile;
            if (log != null)
            {
                var basic = await log.GetBasicPropertiesAsync();
                LogSize = basic.Size;
            }

            var logOld = await ApplicationData.Current.LocalFolder.TryGetItemAsync("tdlib_log.txt.old") as StorageFile;
            if (logOld != null)
            {
                var basic = await logOld.GetBasicPropertiesAsync();
                LogOldSize = basic.Size;
            }

            var properties = typeof(IOptionsService).GetProperties();

            foreach (var prop in properties)
            {
                if (string.Equals(prop.Name, "Values", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = prop.GetValue(CacheService.Options);
                if (value == null)
                {
                    continue;
                }
                else if (value.Equals(true))
                {
                    value = "true";
                }
                else if (value.Equals(false))
                {
                    value = "false";
                }

                Options.Add(new DiagnosticsOption { Name = prop.Name, Value = value });
            }

            foreach (var item in CacheService.Options.Values)
            {
                var value = default(object);
                if (item.Value is OptionValueBoolean boolean)
                {
                    value = boolean.Value ? "true" : "false";
                }
                else if (item.Value is OptionValueInteger integer)
                {
                    value = integer.Value;
                }
                else if (item.Value is OptionValueString strong)
                {
                    value = strong.Value;
                }

                Options.Add(new DiagnosticsOption { Name = item.Key, Value = value });
            }

            var tags = Client.Execute(new GetLogTags()) as LogTags;
            if (tags != null)
            {
                Tags.ReplaceWith(tags.Tags.Select(x => new DiagnosticsTag
                {
                    Name = x,
                    Default = ((LogVerbosityLevel)Client.Execute(new GetLogTagVerbosityLevel(x))).VerbosityLevel,
                    Value = (VerbosityLevel)Settings.Diagnostics.GetValueOrDefault(x, -1)
                }));
            }
        }

        public MvxObservableCollection<DiagnosticsOption> Options { get; private set; }
        public MvxObservableCollection<DiagnosticsTag> Tags { get; private set; }

        public bool Minithumbnails
        {
            get => Settings.Diagnostics.Minithumbnails;
            set
            {
                Settings.Diagnostics.Minithumbnails = value;
                RaisePropertyChanged();
            }
        }

        public bool BubbleAnimations
        {
            get => Settings.Diagnostics.BubbleAnimations;
            set
            {
                Settings.Diagnostics.BubbleAnimations = value;
                RaisePropertyChanged();
            }
        }

        public VerbosityLevel Verbosity
        {
            get => (VerbosityLevel)Settings.VerbosityLevel;
            set
            {
                Settings.VerbosityLevel = (int)value;
                RaisePropertyChanged();
            }
        }

        public bool UseTestDc
        {
            get => Settings.UseTestDC;
            set
            {
                Settings.UseTestDC = value;
                RaisePropertyChanged();
            }
        }


        private ulong _logCallsSize;
        public ulong LogCallsSize
        {
            get => _logCallsSize;
            set => Set(ref _logCallsSize, value);
        }

        private ulong _logSize;
        public ulong LogSize
        {
            get => _logSize;
            set => Set(ref _logSize, value);
        }

        private ulong _logOldSize;
        public ulong LogOldSize
        {
            get => _logOldSize;
            set => Set(ref _logOldSize, value);
        }

        public RelayCommand VerbosityCommand { get; }
        private async void VerbosityExecute()
        {
            var items = Enum.GetValues(typeof(VerbosityLevel)).Cast<VerbosityLevel>().Select(x =>
            {
                return new SelectRadioItem(x, Enum.GetName(typeof(VerbosityLevel), x), x == Verbosity);
            }).ToArray();

            var dialog = new SelectRadioPopup(items);
            dialog.Title = "Verbosity Level";
            dialog.PrimaryButtonText = Strings.Resources.OK;
            dialog.SecondaryButtonText = Strings.Resources.Cancel;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary && dialog.SelectedIndex is VerbosityLevel index)
            {
                Verbosity = index;
                Client.Execute(new SetLogVerbosityLevel((int)index));
            }
        }
    }

    public enum VerbosityLevel
    {
        Assert = 0,
        Error = 1,
        Warning = 2,
        Info = 3,
        Debug = 4,
        Verbose = 5
    }

    public class DiagnosticsOption
    {
        public string Name { get; set; }
        public object Value { get; set; }
    }

    public class DiagnosticsTag : BindableBase
    {
        public string Name { get; set; }
        public int Default { get; set; }

        private VerbosityLevel _value;
        public VerbosityLevel Value
        {
            get => _value;
            set => Set(ref _value, value);
        }

        public string Text
        {
            get
            {
                if ((int)Value == -1 || (int)Value == Default)
                {
                    return "Default";
                }

                return Enum.GetName(typeof(VerbosityLevel), Value);
            }
        }

        public async void Change()
        {
            var items = Enum.GetValues(typeof(VerbosityLevel)).Cast<VerbosityLevel>().Select(x =>
            {
                return new SelectRadioItem(x, Enum.GetName(typeof(VerbosityLevel), x), x == _value);
            }).ToArray();

            var dialog = new SelectRadioPopup(items);
            dialog.Title = Name;
            dialog.PrimaryButtonText = Strings.Resources.OK;
            dialog.SecondaryButtonText = Strings.Resources.Cancel;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary && dialog.SelectedIndex is VerbosityLevel index)
            {
                Value = index;
                RaisePropertyChanged(() => Text);
                Client.Execute(new SetLogTagVerbosityLevel(Name, (int)index));
            }
        }
    }
}
