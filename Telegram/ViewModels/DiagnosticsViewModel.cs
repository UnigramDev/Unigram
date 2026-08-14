//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.ApplicationModel.Core;
using Windows.Devices.Enumeration;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels
{
    public partial class DiagnosticsViewModel : ViewModelBase
    {
        public DiagnosticsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Options = new MvxObservableCollection<DiagnosticsOption>();
            Tags = new MvxObservableCollection<DiagnosticsTag>();
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            UpdateDeserialization();

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
                }));
            }
        }

        public MvxObservableCollection<DiagnosticsOption> Options { get; private set; }
        public MvxObservableCollection<DiagnosticsTag> Tags { get; private set; }

        public bool LegacyScrollBars
        {
            get => Settings.Diagnostics.LegacyScrollBars;
            set
            {
                Settings.Diagnostics.LegacyScrollBars = value;
                RaisePropertyChanged();
                Theme.Current.UpdateScrolls();
            }
        }

        public bool PreferIpv6
        {
            get => ClientService.Options.PreferIpv6;
            set
            {
                ClientService.Options.PreferIpv6 = value;
                RaisePropertyChanged();
            }
        }

        public bool CanUseTestDC => ClientService.AuthorizationState is not AuthorizationStateReady;

        public bool IsDatabaseDisabled => Settings.Diagnostics.DisableDatabase;

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
            get => Array.IndexOf(_verbosityIndexer, SettingsService.Current.VerbosityLevel);
            set
            {
                if (value >= 0 && value < _verbosityIndexer.Length && SettingsService.Current.VerbosityLevel != _verbosityIndexer[value])
                {
                    Client.Execute(new SetLogVerbosityLevel(SettingsService.Current.VerbosityLevel = _verbosityIndexer[value]));
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



        #region Send logs

        public void SendCalls()
        {
            SendFile("tgcalls.txt", false);
        }

        public void SendGroupCalls(object sender, RoutedEventArgs e)
        {
            SendFile("tgcalls_group.txt", false);
        }

        public void SendLog()
        {
            SendFile("tdlib_log.txt", true);
        }

        // Read when the page is opened rather than bound live: the counters are written on the
        // TDLib thread, and a ticking readout would be a second observer of the thing it measures.
        private string _deserialized;
        public string Deserialized
        {
            get => _deserialized;
            private set => Set(ref _deserialized, value);
        }

        private string _deserializationRate;
        public string DeserializationRate
        {
            get => _deserializationRate;
            private set => Set(ref _deserializationRate, value);
        }

        private string _deserializationShare;
        public string DeserializationShare
        {
            get => _deserializationShare;
            private set => Set(ref _deserializationShare, value);
        }

        private string _deserializationHandler;
        public string DeserializationHandler
        {
            get => _deserializationHandler;
            private set => Set(ref _deserializationHandler, value);
        }

        private string _deserializationFileChecks;
        public string DeserializationFileChecks
        {
            get => _deserializationFileChecks;
            private set => Set(ref _deserializationFileChecks, value);
        }

        private void UpdateDeserialization()
        {
            var payloads = TdThroughput.Payloads;
            if (payloads == 0)
            {
                var idle = TdThroughput.Enabled ? "nothing yet" : "off";

                Deserialized = idle;
                DeserializationRate = idle;
                DeserializationShare = idle;
                DeserializationHandler = idle;
                DeserializationFileChecks = idle;
                return;
            }

            var megabytes = TdThroughput.Bytes / 1048576d;
            var seconds = TdThroughput.Seconds;

            // Rates are over the parse with file handling taken out, because that is the part the
            // benchmark is comparable to. The total is still what an update costs.
            var handler = TdThroughput.HandlerSeconds;
            var parsing = seconds - handler;

            Deserialized = string.Format("{0:N0} updates, {1:N1} MB", payloads, megabytes);
            DeserializationRate = string.Format("{0:N1} MB/s, {1:N1} µs each of {2:N1}",
                megabytes / parsing, parsing * 1000000d / payloads, seconds * 1000000d / payloads);
            DeserializationHandler = string.Format("{0:N2}s, {1:N0}% of the parse",
                handler, handler * 100 / seconds);

            var checks = TdThroughput.FileChecks;
            DeserializationFileChecks = checks == 0
                ? "none"
                : string.Format("{0:N0} checks, {1:N2}s, {2:N0} µs each",
                    checks, TdThroughput.FileCheckSeconds, TdThroughput.FileCheckSeconds * 1000000d / checks);
            DeserializationShare = string.Format("{0:N2}% of {1:N0}s",
                seconds * 100 / TdThroughput.WallSeconds, TdThroughput.WallSeconds);
        }

        public void ResetDeserialization(object sender, RoutedEventArgs e)
        {
            TdThroughput.Reset();
            UpdateDeserialization();
        }

        public void SendLogOld(object sender, RoutedEventArgs e)
        {
            SendFile("tdlib_log.txt.old", true);
        }

        private async void SendFile(string fileName, bool logs)
        {
            var file = await ApplicationData.Current.LocalFolder.TryGetItemAsync(fileName) as StorageFile;
            if (file != null)
            {
                ChooseChatsConfiguration configuration = logs
                    ? new ChooseChatsConfigurationPostLogs(file.Path)
                    : new ChooseChatsConfigurationPostMessage(new InputMessageDocument(new InputDocument(new InputFileLocal(file.Path), null, true), null));

                await ShowPopupAsync(new ChooseChatsPopup(), configuration);
            }
        }

        #endregion

        public async void VideoInfo()
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

            MessageHelper.CopyText(XamlRoot, builder.ToString());

            try
            {
                var file = await ApplicationData.Current.LocalFolder.CreateFileAsync("video_info.txt", CreationCollisionOption.ReplaceExisting);

                await FileIO.WriteTextAsync(file, builder.ToString());
                await ShowPopupAsync(new ChooseChatsPopup(), new ChooseChatsConfigurationPostMessage(new InputMessageDocument(new InputDocument(new InputFileLocal(file.Path), null, true), null)));
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

        public async void DisableDatabase()
        {
            if (Settings.Diagnostics.DisableDatabase)
            {
                Settings.Diagnostics.DisableDatabase = false;
            }
            else
            {
                var confirm = await ShowPopupAsync("If you disable the messages database some **features** might **stop to work** as expected, **secret chats** will become **inaccessible** and app won't recognize downloaded files after download.\r\n\r\nAre you sure you want to proceed? You can re-enable messages database anytime from here.", Strings.Warning, Strings.OK, Strings.Cancel);
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

    public partial class DiagnosticsOption
    {
        public string Name { get; set; }
        public object Value { get; set; }
    }

    public partial class DiagnosticsTag : BindableBase
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

        public int Verbosity
        {
            get => Array.IndexOf(_verbosityIndexer, _settings.Diagnostics.GetValueOrDefault(Name, -1));
            set
            {
                if (value >= 0 && value < _verbosityIndexer.Length && SettingsService.Current.VerbosityLevel != _verbosityIndexer[value])
                {
                    var level = _verbosityIndexer[value];
                    if (level == -1)
                    {
                        level = Default;
                    }

                    _settings.Diagnostics.AddOrUpdateValue(Name, _verbosityIndexer[value]);
                    Client.Execute(new SetLogTagVerbosityLevel(Name, _verbosityIndexer[value]));
                    RaisePropertyChanged();
                }
            }
        }

        private readonly int[] _verbosityIndexer = new[]
        {
            -1,
            0,
            1,
            2,
            3,
            4,
            5,
        };

        public List<SettingsOptionItem<int>> VerbosityOptions { get; } = new()
        {
            new SettingsOptionItem<int>(-1, "Default"),
            new SettingsOptionItem<int>(0, nameof(VerbosityLevel.Assert)),
            new SettingsOptionItem<int>(1, nameof(VerbosityLevel.Error)),
            new SettingsOptionItem<int>(2, nameof(VerbosityLevel.Warning)),
            new SettingsOptionItem<int>(3, nameof(VerbosityLevel.Info)),
            new SettingsOptionItem<int>(4, nameof(VerbosityLevel.Debug)),
            new SettingsOptionItem<int>(5, nameof(VerbosityLevel.Verbose)),
        };
    }
}
