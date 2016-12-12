using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Enumeration;
using Windows.Devices.Sensors;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Audio;
using Windows.Media.Render;
using Windows.Phone.Media.Devices;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PlaygroundPage : Page
    {
        private ProximitySensor sensor;
        private ProximitySensorDisplayOnOffController displayController;
        private DeviceWatcher watcher;

        public PlaygroundPage()
        {
            this.InitializeComponent();

            watcher = DeviceInformation.CreateWatcher(ProximitySensor.GetDeviceSelector());
            watcher.Added += OnProximitySensorAdded;
            watcher.Start();

            Loaded += PlaygroundPage_Loaded;
        }

        private async void PlaygroundPage_Loaded(object sender, RoutedEventArgs e)
        {
            var song = String.Format("/Views/Vandamme.mp3");
            var soundFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri(BaseUri, song));

            var devices = await DeviceInformation.FindAllAsync(DeviceClass.AudioRender);
            var yolo = devices.ToList();

            var settings = new AudioGraphSettings(AudioRenderCategory.Communications);
            settings.QuantumSizeSelectionMode = QuantumSizeSelectionMode.LowestLatency;
            settings.PrimaryRenderDevice = devices[1];

            var resultg = await AudioGraph.CreateAsync(settings);
            if (resultg.Status == AudioGraphCreationStatus.Success)
            {
                var audioflow = resultg.Graph;

                var deviceOutputNodeResult = await audioflow.CreateDeviceOutputNodeAsync();
                if (deviceOutputNodeResult.Status == AudioDeviceNodeCreationStatus.Success)
                {
                    var deviceOuput = deviceOutputNodeResult.DeviceOutputNode;

                    var fileInputResult = await audioflow.CreateFileInputNodeAsync(soundFile);
                    var fileInput = fileInputResult.FileInputNode;
                    fileInput.AddOutgoingConnection(deviceOuput);

                    audioflow.Start();

                    AudioRoutingManager.GetDefault().SetAudioEndpoint(AudioRoutingEndpoint.Earpiece);
                }

            }
        }

        /// <summary>
        /// Invoked when the device watcher finds a proximity sensor
        /// </summary>
        /// <param name="sender">The device watcher</param>
        /// <param name="device">Device information for the proximity sensor that was found</param>
        private void OnProximitySensorAdded(DeviceWatcher sender, DeviceInformation device)
        {
            if (null == sensor)
            {
                ProximitySensor foundSensor = ProximitySensor.FromId(device.Id);
                if (null != foundSensor)
                {
                    sensor = foundSensor;
                    sensor.ReadingChanged += Sensor_ReadingChanged;
                    displayController = sensor.CreateDisplayOnOffController();
                }
                else
                {
                    Debug.WriteLine("Could not get a proximity sensor from the device id");
                }
            }
        }

        private void Sensor_ReadingChanged(ProximitySensor sender, ProximitySensorReadingChangedEventArgs args)
        {
            //if (args.Reading.IsDetected)
            //{
            //    AudioRoutingManager.GetDefault().SetAudioEndpoint(AudioRoutingEndpoint.Earpiece);
            //}
            //else
            //{
            //    Debug.WriteLine("Nope");
            //    AudioRoutingManager.GetDefault().SetAudioEndpoint(AudioRoutingEndpoint.Default);
            //}
        }

        /// <summary>
        /// Invoked immediately before the Page is unloaded and is no longer the current source of a parent Frame.
        /// </summary>
        /// <param name="e">
        /// Event data that can be examined by overriding code. The event data is representative
        /// of the navigation that will unload the current Page unless canceled. The
        /// navigation can potentially be canceled by setting Cancel.
        /// </param>
        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            if (null != displayController)
            {
                displayController.Dispose(); // closes the controller
                displayController = null;
            }

            base.OnNavigatingFrom(e);
        }
    }
}
