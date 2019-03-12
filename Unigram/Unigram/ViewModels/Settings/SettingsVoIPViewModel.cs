using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        private readonly IVoIPService _voipService;

        public SettingsVoIPViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, IVoIPService voipService)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _voipService = voipService;

            Input = new MvxObservableCollection<DeviceInformation>();
            Output = new MvxObservableCollection<DeviceInformation>();

            SystemCommand = new RelayCommand(SystemExecute);
        }

        public MvxObservableCollection<DeviceInformation> Input { get; private set; }
        public MvxObservableCollection<DeviceInformation> Output { get; private set; }

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

            _inputVolume = _voipService.CurrentVolumeInput;
            _outputVolume = _voipService.CurrentVolumeOutput;

            RaisePropertyChanged(() => InputVolume);
            RaisePropertyChanged(() => OutputVolume);
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

        private float _inputVolume;
        public float InputVolume
        {
            get { return _inputVolume; }
            set
            {
                if (_inputVolume != value)
                {
                    Set(ref _inputVolume, value);
                    _voipService.CurrentVolumeInput = value;
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

        private float _outputVolume;
        public float OutputVolume
        {
            get { return _outputVolume; }
            set
            {
                if (_outputVolume != value)
                {
                    Set(ref _outputVolume, value);
                    _voipService.CurrentVolumeOutput = value;
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
