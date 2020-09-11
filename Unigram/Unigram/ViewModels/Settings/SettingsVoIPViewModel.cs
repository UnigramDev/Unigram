using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Services;
using Windows.Devices.Enumeration;
using Windows.Media.Devices;
using Windows.System;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsVoIPViewModel : TLViewModelBase
    {
        private readonly IVoipService _voipService;

        public SettingsVoIPViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, IVoipService voipService)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _voipService = voipService;

            Input = new MvxObservableCollection<DeviceInformation>();
            Output = new MvxObservableCollection<DeviceInformation>();
            Video = new MvxObservableCollection<DeviceInformation>();

            SystemCommand = new RelayCommand(SystemExecute);

            _ = OnNavigatedToAsync(null, NavigationMode.New, null);
        }

        public MvxObservableCollection<DeviceInformation> Input { get; private set; }
        public MvxObservableCollection<DeviceInformation> Output { get; private set; }
        public MvxObservableCollection<DeviceInformation> Video { get; private set; }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var input = await DeviceInformation.FindAllAsync(MediaDevice.GetAudioCaptureSelector());
            if (input != null)
            {
                Input.ReplaceWith(input);

                _selectedInput = input.FirstOrDefault(x => x.Id == _voipService.CurrentAudioInput) ?? input.FirstOrDefault(x => x.Id == MediaDevice.GetDefaultAudioCaptureId(AudioDeviceRole.Communications));
                RaisePropertyChanged(() => SelectedInput);
            }

            var output = await DeviceInformation.FindAllAsync(MediaDevice.GetAudioRenderSelector());
            if (output != null)
            {
                Output.ReplaceWith(output);

                _selectedOutput = output.FirstOrDefault(x => x.Id == _voipService.CurrentAudioOutput) ?? output.FirstOrDefault(x => x.Id == MediaDevice.GetDefaultAudioRenderId(AudioDeviceRole.Communications));
                RaisePropertyChanged(() => SelectedOutput);
            }

            var video = await DeviceInformation.FindAllAsync(MediaDevice.GetVideoCaptureSelector());
            if (video != null)
            {
                Video.ReplaceWith(video);

                _selectedVideo = video.FirstOrDefault(x => x.Id == _voipService.CurrentVideoInput) ?? video.FirstOrDefault();
                RaisePropertyChanged(() => SelectedVideo);
            }
        }

        private DeviceInformation _selectedInput;
        public DeviceInformation SelectedInput
        {
            get { return _selectedInput; }
            set
            {
                if (_selectedInput?.Id != value?.Id)
                {
                    Set(ref _selectedInput, value);
                    _voipService.CurrentAudioInput = value?.Id ?? "default";
                }
            }
        }

        private DeviceInformation _selectedOutput;
        public DeviceInformation SelectedOutput
        {
            get { return _selectedOutput; }
            set
            {
                if (_selectedOutput?.Id != value?.Id)
                {
                    Set(ref _selectedOutput, value);
                    _voipService.CurrentAudioOutput = value?.Id ?? "default";
                }
            }
        }

        private DeviceInformation _selectedVideo;
        public DeviceInformation SelectedVideo
        {
            get { return _selectedVideo; }
            set
            {
                if (_selectedVideo?.Id != value?.Id)
                {
                    Set(ref _selectedVideo, value);
                    _voipService.CurrentVideoInput = value?.Id ?? "default";
                }
            }
        }

        public RelayCommand SystemCommand { get; }
        private async void SystemExecute()
        {
            await Launcher.LaunchUriAsync(new Uri("ms-settings:sound"));
        }
    }
}
