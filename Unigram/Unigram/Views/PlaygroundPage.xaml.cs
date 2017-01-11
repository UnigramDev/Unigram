using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.FileManager;
using Telegram.Api.Services.Updates;
using Telegram.Api.TL;
using Unigram.Controls;
using Unigram.Core.Dependency;
using Unigram.Native;
using Unigram.ViewModels;
using Windows.ApplicationModel.Calls;
using Windows.Devices.Enumeration;
using Windows.Devices.Sensors;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Media.Audio;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
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
        private OpusRecorder recorder;

        public PlaygroundPage()
        {
            this.InitializeComponent();


        }

        private async void Start_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (recorder?.IsRecording == true)
            {
                await recorder.StopAsync();
            }

            var file = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("recording.ogg", CreationCollisionOption.ReplaceExisting);
            recorder = new OpusRecorder(file);
            await recorder.StartAsync();
        }

        private async void Start_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (recorder.IsRecording)
            {
                await recorder.StopAsync();

                var file = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("recording.ogg", CreationCollisionOption.OpenIfExists);

                var cacheService = UnigramContainer.Instance.ResolveType<ICacheService>();
                var protoService = UnigramContainer.Instance.ResolveType<IMTProtoService>();
                var updatesService = UnigramContainer.Instance.ResolveType<IUpdatesService>();
                var uploadManager = UnigramContainer.Instance.ResolveType<IUploadAudioManager>();

                var contacts = await protoService.GetDialogsAsync(0, 0, new TLInputPeerEmpty(), 200);
                //var user = contacts.Value.Users.OfType<TLUser>().FirstOrDefault(x => x.FullName.Equals("Andrea Cocci"));
                var channel = contacts.Value.Chats.OfType<TLChannel>().FirstOrDefault(x => x.FullName.Equals("Unigram Insiders"));

                var fileLocation = new TLFileLocation
                {
                    VolumeId = TLLong.Random(),
                    LocalId = TLInt.Random(),
                    Secret = TLLong.Random(),
                    DCId = 0
                };

                var fileName = string.Format("{0}_{1}_{2}.ogg", fileLocation.VolumeId, fileLocation.LocalId, fileLocation.Secret);
                var fileCache = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

                await file.CopyAndReplaceAsync(fileCache);

                var basicProps = await fileCache.GetBasicPropertiesAsync();
                var imageProps = await fileCache.Properties.GetMusicPropertiesAsync();

                var date = TLUtils.DateToUniversalTimeTLInt(protoService.ClientTicksDelta, DateTime.Now);

                var media = new TLMessageMediaDocument
                {
                    // TODO: Document = ...
                    //Caption = "Yolo"
                };

                var message = TLUtils.GetMessage(SettingsHelper.UserId, new TLPeerChannel { ChannelId = channel.Id }, TLMessageState.Sending, true, true, date, string.Empty, media, TLLong.Random(), null);

                var fileId = TLLong.Random();
                var upload = await uploadManager.UploadFileAsync(fileId, fileCache.Name, false).AsTask(media.Upload());
                if (upload != null)
                {
                    var inputMedia = new TLInputMediaUploadedDocument
                    {
                        File = new TLInputFile
                        {
                            Id = upload.FileId,
                            Md5Checksum = string.Empty,
                            Name = fileName,
                            Parts = upload.Parts.Count
                        },
                        MimeType = "audio/ogg",
                        Caption = media.Caption,
                        Attributes = new TLVector<TLDocumentAttributeBase>
                        {
                            new TLDocumentAttributeAnimated(),
                            new TLDocumentAttributeAudio
                            {
                                IsVoice = true,
                                Duration = 50
                            }
                        }
                    };

                    var result = await protoService.SendMediaAsync(new TLInputPeerChannel { ChannelId = channel.Id, AccessHash = channel.AccessHash.Value }, inputMedia, message);
                }
            }
        }

        private AudioGraph graph;
        private AudioDeviceOutputNode deviceOutputNode;
        private AudioFileInputNode fileInputNode;
        private CreateAudioFileInputNodeResult fileInputNodeResult;
        private CreateAudioDeviceOutputNodeResult deviceOutputNodeResult;
        private StorageFile file;

        private async void Media_Loaded(object sender, RoutedEventArgs e)
        {





            if (recorder?.IsRecording == true)
            {
                await recorder.StopAsync();
                //Media.Source = new Uri("ms-appdata:///temp/recording.ogg");
                //Media.Play();

                file = await ApplicationData.Current.TemporaryFolder.GetFileAsync("recording.ogg");

                var settings = new AudioGraphSettings(AudioRenderCategory.Communications);
                settings.QuantumSizeSelectionMode = QuantumSizeSelectionMode.LowestLatency;

                var result = await AudioGraph.CreateAsync(settings);
                if (result.Status != AudioGraphCreationStatus.Success)
                    return;

                graph = result.Graph;
                Debug.WriteLine("Graph successfully created!");

                fileInputNodeResult = await graph.CreateFileInputNodeAsync(file);
                if (fileInputNodeResult.Status != AudioFileNodeCreationStatus.Success)
                    return;

                deviceOutputNodeResult = await graph.CreateDeviceOutputNodeAsync();
                if (deviceOutputNodeResult.Status != AudioDeviceNodeCreationStatus.Success)
                    return;

                deviceOutputNode = deviceOutputNodeResult.DeviceOutputNode;
                fileInputNode = fileInputNodeResult.FileInputNode;
                fileInputNode.AddOutgoingConnection(deviceOutputNode);
                 
                graph.Start();

                //var file2 = await ApplicationData.Current.TemporaryFolder.GetFileAsync("recording.ogg");
                //Media.SetSource(await file2.OpenReadAsync(), "audio/ogg");
                //Media.Play();

                return;
            }

            file = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("recording.ogg", CreationCollisionOption.ReplaceExisting);
            recorder = new OpusRecorder(file);
            await recorder.StartAsync();

        }
    }

    public class VoiceButton : GlyphButton
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        private OpusRecorder _recorder;
        private StorageFile _file;
        private bool _cancelOnRelease;
        private bool _isPressed;
        private DateTime _start;

        public VoiceButton()
        {
            DefaultStyleKey = typeof(VoiceButton);
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            base.OnPointerPressed(e);
            Start();

            CapturePointer(e.Pointer);
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            base.OnPointerReleased(e);
            Stop();

            ReleasePointerCapture(e.Pointer);
        }

        protected override void OnPointerEntered(PointerRoutedEventArgs e)
        {
            base.OnPointerEntered(e);

            if (_isPressed)
            {
                _cancelOnRelease = false;
            }
        }

        protected override void OnPointerExited(PointerRoutedEventArgs e)
        {
            base.OnPointerExited(e);

            if (_isPressed)
            {
                _cancelOnRelease = true;
            }
        }

        private async void Start()
        {
            if (_recorder?.IsRecording == true)
            {
                await _recorder.StopAsync();
            }

            _file = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("recording.ogg", CreationCollisionOption.ReplaceExisting);
            _recorder = new OpusRecorder(_file);
            await _recorder.StartAsync();

            _isPressed = true;
            _cancelOnRelease = false;
            _start = DateTime.Now;
        }

        private async void Stop()
        {
            if (_recorder?.IsRecording == true)
            {
                await _recorder.StopAsync();
            }

            if (_cancelOnRelease)
            {
                await _file.DeleteAsync();
            }
            else
            {
                await ViewModel.SendAudioAsync(_file, (int)(DateTime.Now - _start).TotalSeconds, true, null, null, null);
            }
        }
    }

    internal sealed class OpusRecorder
    {
        #region fields

        private StorageFile m_file;
        private IMediaExtension m_opusSink;
        private MediaCapture m_mediaCapture;

        #endregion

        #region properties

        public StorageFile File
        {
            get { return m_file; }
        }

        public bool IsRecording
        {
            get { return m_mediaCapture != null; }
        }

        #endregion

        #region constructors

        public OpusRecorder(StorageFile file)
        {
            m_file = file;
        }

        #endregion

        #region methods

        public async Task StartAsync()
        {
            if (m_mediaCapture != null)
                throw new InvalidOperationException("Cannot start while recording");

            m_mediaCapture = new MediaCapture();
            await m_mediaCapture.InitializeAsync(new MediaCaptureInitializationSettings()
            {
                MediaCategory = MediaCategory.Speech,
                AudioProcessing = AudioProcessing.Default,
                MemoryPreference = MediaCaptureMemoryPreference.Auto,
                SharingMode = MediaCaptureSharingMode.SharedReadOnly,
                StreamingCaptureMode = StreamingCaptureMode.Audio,
            });

            m_opusSink = await OpusCodec.CreateMediaSinkAsync(m_file);

            var wawEncodingProfile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.High);
            wawEncodingProfile.Audio.BitsPerSample = 16;
            wawEncodingProfile.Audio.SampleRate = 48000;
            wawEncodingProfile.Audio.ChannelCount = 1;
            await m_mediaCapture.StartRecordToCustomSinkAsync(wawEncodingProfile, m_opusSink);
        }

        public async Task StopAsync()
        {
            if (m_mediaCapture == null)
                throw new InvalidOperationException("Cannot stop while not recording");

            await m_mediaCapture.StopRecordAsync();

            m_mediaCapture.Dispose();
            m_mediaCapture = null;

            ((IDisposable)m_opusSink).Dispose();
            m_opusSink = null;
        }

        #endregion
    }
}
