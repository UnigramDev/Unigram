//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Navigation;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.Views.Popups;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Enumeration;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;

namespace Unigram.ViewModels
{
    public class DiagnosticsViewModel : TLViewModelBase
    {
        public DiagnosticsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Options = new MvxObservableCollection<DiagnosticsOption>();
            Tags = new MvxObservableCollection<DiagnosticsTag>();

            VideoInfoCommand = new RelayCommand(VideoInfoExecute);

            DisableDatabaseCommand = new RelayCommand(DisableDatabaseExecute);
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var calls = await ApplicationData.Current.LocalFolder.TryGetItemAsync("tgcalls.txt") as StorageFile;
            if (calls != null)
            {
                var basic = await calls.GetBasicPropertiesAsync();
                LogCallsSize = basic.Size;
            }

            var group = await ApplicationData.Current.LocalFolder.TryGetItemAsync("tgcalls_group.txt") as StorageFile;
            if (group != null)
            {
                var basic = await group.GetBasicPropertiesAsync();
                LogGroupCallsSize = basic.Size;
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

                var value = prop.GetValue(ClientService.Options);
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

            foreach (var item in ClientService.Options.Values)
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
                Tags.ReplaceWith(tags.Tags.Select(x => new DiagnosticsTag(NavigationService, Settings)
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

        public bool CopyFormattedCode
        {
            get => Settings.Diagnostics.CopyFormattedCode;
            set
            {
                Settings.Diagnostics.CopyFormattedCode = value;
                RaisePropertyChanged();
            }
        }

        public bool AllowRightToLeft
        {
            get => SettingsService.Current.Diagnostics.AllowRightToLeft;
            set
            {
                SettingsService.Current.Diagnostics.AllowRightToLeft = value;
                RaisePropertyChanged();
            }
        }

        public bool IsPointerOverEnabled
        {
            get => SettingsService.Current.Stickers.IsPointerOverEnabled;
            set
            {
                SettingsService.Current.Stickers.IsPointerOverEnabled = value;
                RaisePropertyChanged();
            }
        }

        public bool StickyPhotos
        {
            get => SettingsService.Current.Diagnostics.StickyPhotos;
            set
            {
                SettingsService.Current.Diagnostics.StickyPhotos = value;
                RaisePropertyChanged();
            }
        }

        public bool CanUseTestDC => ClientService.AuthorizationState is not AuthorizationStateReady;

        public bool DisableDatabase => Settings.Diagnostics.DisableDatabase;

        public bool UseTestDC
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

        private ulong _logGroupCallsSize;
        public ulong LogGroupCallsSize
        {
            get => _logGroupCallsSize;
            set => Set(ref _logGroupCallsSize, value);
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

        public int Verbosity
        {
            get => Array.IndexOf(_verbosityIndexer, Settings.VerbosityLevel);
            set
            {
                if (value >= 0 && value < _verbosityIndexer.Length && Settings.VerbosityLevel != _verbosityIndexer[value])
                {
                    Client.Execute(new SetLogVerbosityLevel(Settings.VerbosityLevel = _verbosityIndexer[value]));
                    RaisePropertyChanged();
                }
            }
        }

        private readonly int[] _verbosityIndexer = new[]
        {
            0,
            1,
            2,
            3,
            4,
            5,
        };

        public List<SettingsOptionItem<int>> VerbosityOptions { get; } = new()
        {
            new SettingsOptionItem<int>(0, nameof(VerbosityLevel.Assert)),
            new SettingsOptionItem<int>(1, nameof(VerbosityLevel.Error)),
            new SettingsOptionItem<int>(2, nameof(VerbosityLevel.Warning)),
            new SettingsOptionItem<int>(3, nameof(VerbosityLevel.Info)),
            new SettingsOptionItem<int>(4, nameof(VerbosityLevel.Debug)),
            new SettingsOptionItem<int>(5, nameof(VerbosityLevel.Verbose)),
        };

        public RelayCommand VideoInfoCommand { get; }
        public async void VideoInfoExecute()
        {
            var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            var builder = new StringBuilder();

            foreach (var device in devices)
            {
                builder.AppendLine(string.Format("- {0}:", device.Id));
                builder.AppendLine(string.Format("    name: {0}", device.Name));

                FillVideoCaptureCapabilityFromDeviceProfiles(builder, device.Id);
                await FillVideoCaptureCapabilityFromDeviceWithoutProfiles(builder, device.Id);
            }

            var dataPackage = new DataPackage();
            dataPackage.SetText(builder.ToString());
            ClipboardEx.TrySetContent(dataPackage);

            try
            {
                var file = await ApplicationData.Current.LocalFolder.CreateFileAsync("video_info.txt", CreationCollisionOption.ReplaceExisting);

                await FileIO.WriteTextAsync(file, builder.ToString());
                await SharePopup.Create().ShowAsync(new InputMessageDocument(new InputFileLocal(file.Path), null, true, null));
            }
            catch { }
        }

        private static void FillVideoCaptureCapabilityFromDeviceProfiles(StringBuilder builder, string deviceId)
        {
            builder.AppendLine("    video_profiles:");

            foreach (var profile in MediaCapture.FindAllVideoProfiles(deviceId))
            {
                var profile_description_list = profile.SupportedRecordMediaDescription;
                var profile_id = profile.Id;

                foreach (var description in profile_description_list)
                {
                    var width = description.Width;
                    var height = description.Height;
                    var framerate = description.FrameRate;
                    var sub_type = description.Subtype;

                    builder.AppendLine(string.Format("    - size: {0}x{1}, fps: {2}, subtype: {3}", width, height, framerate, sub_type));
                }
            }
        }

        private static async Task FillVideoCaptureCapabilityFromDeviceWithoutProfiles(StringBuilder builder, string deviceId)
        {
            var settings = new MediaCaptureInitializationSettings();
            settings.VideoDeviceId = deviceId;
            settings.StreamingCaptureMode = StreamingCaptureMode.AudioAndVideo;
            settings.MemoryPreference = MediaCaptureMemoryPreference.Cpu;

            builder.AppendLine("    video_properties:");

            var mediaCapture = new MediaCapture();
            await mediaCapture.InitializeAsync(settings);

            var availableProperties = mediaCapture.VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.VideoRecord);

            foreach (var profile in availableProperties.OfType<VideoEncodingProperties>())
            {
                var width = profile.Width;
                var height = profile.Height;
                var framerate = (profile.FrameRate.Denominator != 0) ? profile.FrameRate.Numerator / profile.FrameRate.Denominator : 0;
                var sub_type = profile.Subtype;

                builder.AppendLine(string.Format("    - size: {0}x{1}, fps: {2}, subtype: {3}", width, height, framerate, sub_type));
            }
        }

        public RelayCommand DisableDatabaseCommand { get; }
        private async void DisableDatabaseExecute()
        {
            if (Settings.Diagnostics.DisableDatabase)
            {
                Settings.Diagnostics.DisableDatabase = false;
            }
            else
            {
                var confirm = await MessagePopup.ShowAsync(XamlRoot, "If you disable the messages database some **features** might **stop to work** as expected, **secret chats** will become **inaccessible** and app won't recognize downloaded files after download.\r\n\r\nAre you sure you want to proceed? You can re-enable messages database anytime from here.", Strings.Resources.Warning, Strings.Resources.OK, Strings.Resources.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    Settings.Diagnostics.DisableDatabase = true;
                }
                else
                {
                    return;
                }
            }

            await CoreApplication.RequestRestartAsync(string.Empty);
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
        private readonly INavigationService _navigationService;
        private readonly ISettingsService _settings;

        public string Name { get; set; }
        public int Default { get; set; }

        public DiagnosticsTag(INavigationService navigationService, ISettingsService settings)
        {
            _navigationService = navigationService;
            _settings = settings;
        }

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
                return new ChooseOptionItem(x, Enum.GetName(typeof(VerbosityLevel), x), x == _value);
            }).ToArray();

            var dialog = new ChooseOptionPopup(items);
            dialog.Title = Name;
            dialog.PrimaryButtonText = Strings.Resources.OK;
            dialog.SecondaryButtonText = Strings.Resources.Cancel;

            var confirm = await dialog.ShowQueuedAsync(_navigationService.XamlRoot);
            if (confirm == ContentDialogResult.Primary && dialog.SelectedIndex is VerbosityLevel index)
            {
                Value = index;
                RaisePropertyChanged(nameof(Text));

                _settings.Diagnostics.AddOrUpdateValue(Name, (int)index);
                Client.Execute(new SetLogTagVerbosityLevel(Name, (int)index));
            }
        }
    }
}
