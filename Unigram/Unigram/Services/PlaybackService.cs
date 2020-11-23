using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Navigation;
using Unigram.Services.Updates;
using Windows.Foundation;
using Windows.Media;
using Windows.Media.Audio;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.Render;
using Windows.Storage;

namespace Unigram.Services
{
    public interface IPlaybackService : INotifyPropertyChanged
    {
        IReadOnlyList<PlaybackItem> Items { get; }

        Message CurrentItem { get; }

        double PlaybackRate { get; set; }

        double Volume { get; set; }

        void Pause();
        void Play();

        void Seek(TimeSpan span);

        void MoveNext();
        void MovePrevious();

        void Clear();

        void Enqueue(Message message);

        TimeSpan Position { get; }
        TimeSpan Duration { get; }

        MediaPlaybackState PlaybackState { get; }



        bool? IsRepeatEnabled { get; set; }
        bool IsShuffleEnabled { get; set; }
        bool IsReversed { get; set; }


        bool IsSupportedPlaybackRateRange(double min, double max);



        event TypedEventHandler<IPlaybackService, MediaPlayerFailedEventArgs> MediaFailed;
        event TypedEventHandler<IPlaybackService, object> PlaybackStateChanged;
        event TypedEventHandler<IPlaybackService, object> PositionChanged;
        event TypedEventHandler<IPlaybackService, float[]> QuantumChanged;
        event EventHandler PlaylistChanged;
    }

    public class PlaybackService : BindableBase, IPlaybackService, IHandle<UpdateFile>
    {
        private readonly IProtoService _protoService;
        private readonly ICacheService _cacheService;
        private readonly ISettingsService _settingsService;
        private readonly IEventAggregator _aggregator;

        private AudioGraph _audioGraph;
        private MediaSourceAudioInputNode _inputNode;
        private AudioFrameOutputNode _outputNode;

        private readonly SystemMediaTransportControls _transport;

        private readonly Dictionary<string, PlaybackItem> _mapping;

        private readonly FileContext<RemoteFileStream> _streams = new FileContext<RemoteFileStream>();

        private List<PlaybackItem> _items;
        private Queue<Message> _queue;

        public event TypedEventHandler<IPlaybackService, MediaPlayerFailedEventArgs> MediaFailed;
        public event TypedEventHandler<IPlaybackService, object> PlaybackStateChanged;
        public event TypedEventHandler<IPlaybackService, object> PositionChanged;
        public event TypedEventHandler<IPlaybackService, float[]> QuantumChanged;
        public event EventHandler PlaylistChanged;

        public PlaybackService(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
        {
            if (!ApiInfo.IsMediaSupported)
            {
                return;
            }

            _protoService = protoService;
            _cacheService = cacheService;
            _settingsService = settingsService;
            _aggregator = aggregator;

            _transport = SystemMediaTransportControls.GetForCurrentView();
            _transport.ButtonPressed += Transport_ButtonPressed;

            _transport.AutoRepeatMode = _settingsService.Playback.RepeatMode;
            _isRepeatEnabled = _settingsService.Playback.RepeatMode == MediaPlaybackAutoRepeatMode.Track
                ? null
                : _settingsService.Playback.RepeatMode == MediaPlaybackAutoRepeatMode.List
                ? true
                : (bool?)false;

            _mapping = new Dictionary<string, PlaybackItem>();

            aggregator.Subscribe(this);
        }

        #region SystemMediaTransportControls

        private void Transport_AutoRepeatModeChangeRequested(SystemMediaTransportControls sender, AutoRepeatModeChangeRequestedEventArgs args)
        {
            IsRepeatEnabled = args.RequestedAutoRepeatMode == MediaPlaybackAutoRepeatMode.List
                ? true
                : args.RequestedAutoRepeatMode == MediaPlaybackAutoRepeatMode.Track
                ? null
                : (bool?)false;
        }

        private void Transport_ButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            var audioGraph = _audioGraph;
            if (audioGraph == null)
            {
                return;
            }

            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                    audioGraph.Start();
                    break;
                case SystemMediaTransportControlsButton.Pause:
                    audioGraph.Stop();
                    break;
                case SystemMediaTransportControlsButton.Rewind:
                    //_mediaPlayer.StepBackwardOneFrame();
                    break;
                case SystemMediaTransportControlsButton.FastForward:
                    //_mediaPlayer.StepForwardOneFrame();
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    if (Position.TotalSeconds > 5)
                    {
                        Seek(TimeSpan.Zero);
                    }
                    else
                    {
                        MovePrevious();
                    }
                    break;
                case SystemMediaTransportControlsButton.Next:
                    MoveNext();
                    break;
            }
        }

        #endregion

        private void OnSourceChanged(MediaPlayer sender, object args)
        {
            if (sender.Source is MediaSource source && source.CustomProperties.TryGet("token", out string token) && _mapping.TryGetValue(token, out PlaybackItem item))
            {
                CurrentPlayback = item;

                var message = item.Message;
                if ((message.Content is MessageVideoNote videoNote && !videoNote.IsViewed && !message.IsOutgoing) || (message.Content is MessageVoiceNote voiceNote && !voiceNote.IsListened && !message.IsOutgoing))
                {
                    _protoService.Send(new OpenMessageContent(message.ChatId, message.Id));
                }
            }
        }

        private void OnMediaEnded(MediaSourceAudioInputNode sender, object args)
        {
            if (sender.MediaSource is MediaSource source && source.CustomProperties.TryGet("token", out string token) && _mapping.TryGetValue(token, out PlaybackItem item))
            {
                if (item.Message.Content is MessageAudio && _isRepeatEnabled == null)
                {
                    _audioGraph.Start();
                    //_mediaPlayer.Play();
                }
                else
                {
                    var index = _items.IndexOf(item);
                    if (index == -1 || index == (_isReversed ? 0 : _items.Count - 1))
                    {
                        if (item.Message.Content is MessageAudio && _isRepeatEnabled == true)
                        {
                            CurrentPlayback = _items[_isReversed ? _items.Count - 1 : 0];
                            //_mediaPlayer.Source = _items[_isReversed ? _items.Count - 1 : 0].Source;
                            //_mediaPlayer.Play();
                        }
                        else
                        {
                            Clear();
                        }
                    }
                    else
                    {
                        CurrentPlayback = _items[_isReversed ? index - 1 : index + 1];
                        //_mediaPlayer.Source = _items[_isReversed ? index - 1 : index + 1].Source;
                        //_mediaPlayer.Play();
                    }
                }
            }
        }

        private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            Clear();
            MediaFailed?.Invoke(this, args);
        }

        private void OnPlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            if (sender.PlaybackState == MediaPlaybackState.Playing && sender.PlaybackRate != _playbackRate)
            {
                sender.PlaybackRate = _playbackRate;
            }

            switch (sender.PlaybackState)
            {
                case MediaPlaybackState.Playing:
                    _transport.PlaybackStatus = MediaPlaybackStatus.Playing;
                    break;
                case MediaPlaybackState.Paused:
                    _transport.PlaybackStatus = MediaPlaybackStatus.Paused;
                    break;
                case MediaPlaybackState.None:
                    _transport.PlaybackStatus = MediaPlaybackStatus.Stopped;
                    break;
            }

            //else if (sender.PlaybackState == MediaPlaybackState.Paused && sender.Position == sender.NaturalDuration && _playlist.CurrentItem == _playlist.Items.LastOrDefault())
            //{
            //    Clear();
            //}
            //else
            //{
            //    Debugger.Break();
            //}

            PlaybackStateChanged?.Invoke(this, args);
        }

        private void OnPositionChanged(MediaPlaybackSession sender, object args)
        {
            PositionChanged?.Invoke(this, args);
        }

        private async void UpdateTransport()
        {
            var items = _items;
            var item = CurrentPlayback;

            if (items == null || item == null)
            {
                _transport.IsEnabled = false;
                _transport.DisplayUpdater.ClearAll();
                return;
            }

            _transport.IsEnabled = true;
            _transport.IsPlayEnabled = true;
            _transport.IsPauseEnabled = true;
            _transport.IsPreviousEnabled = true;
            _transport.IsNextEnabled = items.Count > 1;

            void SetProperties(string title, string artist)
            {
                _transport.DisplayUpdater.ClearAll();
                _transport.DisplayUpdater.Type = MediaPlaybackType.Music;

                try
                {
                    _transport.DisplayUpdater.MusicProperties.Title = title ?? string.Empty;
                    _transport.DisplayUpdater.MusicProperties.Artist = artist ?? string.Empty;
                }
                catch { }
            }

            if (item.File.Local.IsDownloadingCompleted)
            {
                try
                {
                    var file = await _protoService.GetFileAsync(item.File);
                    await _transport.DisplayUpdater.CopyFromFileAsync(MediaPlaybackType.Music, file);
                }
                catch
                {
                    SetProperties(item.Title, item.Artist);
                }
            }
            else
            {
                SetProperties(item.Title, item.Artist);
            }

            _transport.DisplayUpdater.Update();
        }

        public IReadOnlyList<PlaybackItem> Items
        {
            get
            {
                return _items ?? (IReadOnlyList<PlaybackItem>)new PlaybackItem[0];
            }
        }

        private PlaybackItem _currentPlayback;
        public PlaybackItem CurrentPlayback
        {
            get => _currentPlayback;
            private set => Set(value);
        }

        private async void Set(PlaybackItem value)
        {
            if (_inputNode != null)
            {
                _inputNode.MediaSourceCompleted -= OnMediaEnded;
                _inputNode = null;
            }

            if (_audioGraph != null)
            {
                _audioGraph.QuantumProcessed -= OnQuantumProcessed;
                _audioGraph.Stop();
                _audioGraph = null;
            }

            _currentPlayback = value;
            _aggregator.Publish(new UpdatePlaybackItem(value?.Message));
            RaisePropertyChanged(nameof(CurrentItem));
            UpdateTransport();

            if (value == null)
            {
                PlaybackState = MediaPlaybackState.None;
                return;
            }

            PlaybackState = MediaPlaybackState.Playing;

            var result = await CreateAudioGraphAsync(value);
            if (result == null)
            {
                PlaybackState = MediaPlaybackState.None;
                return;
            }

            PlaybackState = MediaPlaybackState.Playing;

            var message = value.Message;
            if (message != null && ((message.Content is MessageVideoNote videoNote && !videoNote.IsViewed && !message.IsOutgoing) || (message.Content is MessageVoiceNote voiceNote && !voiceNote.IsListened && !message.IsOutgoing)))
            {
                _protoService.Send(new OpenMessageContent(message.ChatId, message.Id));
            }
        }

        private const int BUFFER_SIZE = 1024;
        private readonly FastFourierTransform _fft = new FastFourierTransform(BUFFER_SIZE, 48000);
        private int _lastUpdateTime;

        [ComImport]
        [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        unsafe interface IMemoryBufferByteAccess
        {
            void GetBuffer(out byte* buffer, out uint capacity);
        }

        private unsafe void OnQuantumProcessed(AudioGraph sender, object args)
        {
            var outputNode = _outputNode;
            if (outputNode == null)
            {
                return;
            }

            var position = PositionChanged;
            var quantum = QuantumChanged;

            if (position != null || quantum != null)
            {
                if (Environment.TickCount - _lastUpdateTime < 64)
                {
                    return;
                }

                _lastUpdateTime = Environment.TickCount;
                position?.Invoke(this, null);

                if (quantum == null)
                {
                    return;
                }

                try
                {
                    var frame = outputNode.GetFrame();

                    using var audioBuffer = frame.LockBuffer(AudioBufferAccessMode.Read);
                    using var bufferReference = audioBuffer.CreateReference();

                    // Get the buffer from the AudioFrame
                    ((IMemoryBufferByteAccess)bufferReference).GetBuffer(out byte* buffer, out uint capacity);

                    if (capacity < BUFFER_SIZE /*> MAX_BUFFER_SIZE || len == 0*/)
                    {
                        //audioUpdateHandler.removeCallbacksAndMessages(null);
                        //audioVisualizerDelegate.onVisualizerUpdate(false, true, null);
                        return;
                        //                len = MAX_BUFFER_SIZE;
                        //                byte[] bytes = new byte[BUFFER_SIZE];
                        //                buffer.get(bytes);
                        //                byteBuffer.put(bytes, 0, BUFFER_SIZE);
                    }
                    else
                    {
                        //byteBuffer.put(buffer);
                    }

                    capacity = BUFFER_SIZE;

                    _fft.Forward((short*)buffer, BUFFER_SIZE);

                    float sum = 0;
                    for (int i = 0; i < capacity; i++)
                    {
                        float r = _fft.SpectrumReal[i];
                        float img = _fft.SpectrumImaginary[i];
                        float peak = (float)MathF.Sqrt(r * r + img * img) / 30f;
                        if (peak > 1f)
                        {
                            peak = 1f;
                        }
                        else if (peak < 0)
                        {
                            peak = 0;
                        }
                        sum += peak * peak;
                    }
                    float amplitude = MathF.Sqrt(sum / capacity);

                    float[] partsAmplitude = new float[7];
                    partsAmplitude[6] = amplitude;
                    if (amplitude < 0.4f)
                    {
                        for (int k = 0; k < 7; k++)
                        {
                            partsAmplitude[k] = 0;
                        }
                    }
                    else
                    {
                        int part = (int)capacity / 6;

                        for (int k = 0; k < 6; k++)
                        {
                            int start = part * k;
                            float r = _fft.SpectrumReal[start];
                            float img = _fft.SpectrumImaginary[start];
                            partsAmplitude[k] = MathF.Sqrt(r * r + img * img) / 30f;

                            if (partsAmplitude[k] > 1f)
                            {
                                partsAmplitude[k] = 1f;
                            }
                            else if (partsAmplitude[k] < 0)
                            {
                                partsAmplitude[k] = 0;
                            }
                        }
                    }

                    quantum?.Invoke(this, partsAmplitude);
                }
                catch { }
            }
        }

        public Message CurrentItem => _currentPlayback?.Message;

        public TimeSpan Position
        {
            get
            {
                try
                {
                    return _inputNode?.Position ?? TimeSpan.Zero;
                }
                catch
                {
                    return TimeSpan.Zero;
                }
            }
        }

        public TimeSpan Duration
        {
            get
            {
                try
                {
                    return _inputNode?.Duration ?? TimeSpan.Zero;
                }
                catch
                {
                    return TimeSpan.Zero;
                }
            }
        }

        private MediaPlaybackState _playbackState;
        public MediaPlaybackState PlaybackState
        {
            get => _playbackState;
            private set => Set(value);
        }

        private void Set(MediaPlaybackState value)
        {
            if (_playbackState != value)
            {
                _playbackState = value;
                PlaybackStateChanged?.Invoke(this, null);
            }
        }

        private bool? _isRepeatEnabled = false;
        public bool? IsRepeatEnabled
        {
            get => _isRepeatEnabled;
            set
            {
                _isRepeatEnabled = value;
                _transport.AutoRepeatMode =
                    _settingsService.Playback.RepeatMode = value == true
                        ? MediaPlaybackAutoRepeatMode.List
                        : value == null
                        ? MediaPlaybackAutoRepeatMode.Track
                        : MediaPlaybackAutoRepeatMode.None;
            }
        }

        private bool _isReversed = false;
        public bool IsReversed
        {
            get => _isReversed;
            set => _isReversed = value;
        }

        private bool _isShuffleEnabled;
        public bool IsShuffleEnabled
        {
            get => _isShuffleEnabled;
            set
            {
                _isShuffleEnabled = value;
                _transport.ShuffleEnabled = value;
            }
        }

        private double _playbackRate = 1.0;
        public double PlaybackRate
        {
            get => _playbackRate;
            set
            {
                _playbackRate = value;
                _transport.PlaybackRate = value;

                if (_inputNode != null)
                {
                    _inputNode.PlaybackSpeedFactor = value;
                }
            }
        }

        public double Volume
        {
            get => _inputNode?.OutgoingGain ?? _settingsService.VolumeLevel;
            set
            {
                _settingsService.VolumeLevel = value;

                if (_inputNode != null)
                {
                    _inputNode.OutgoingGain = value;
                }
            }
        }

        public void Pause()
        {
            _audioGraph?.Stop();
            PlaybackState = MediaPlaybackState.Paused;
        }

        public void Play()
        {
            _audioGraph?.Start();
            PlaybackState = MediaPlaybackState.Playing;
        }

        public void Seek(TimeSpan span)
        {
            _inputNode?.Seek(span);
        }

        public void MoveNext()
        {
            var items = _items;
            if (items == null)
            {
                return;
            }

            var index = items.IndexOf(CurrentPlayback);
            if (index == (_isReversed ? 0 : items.Count - 1))
            {
                SetSource(items, _isReversed ? items.Count - 1 : 0);
            }
            else
            {
                SetSource(items, _isReversed ? index - 1 : index + 1);
            }
        }

        public void MovePrevious()
        {
            var items = _items;
            if (items == null)
            {
                return;
            }

            var index = items.IndexOf(CurrentPlayback);
            if (index == (_isReversed ? items.Count - 1 : 0))
            {
                SetSource(items, _isReversed ? 0 : items.Count - 1);
            }
            else
            {
                SetSource(items, _isReversed ? index + 1 : index - 1);
            }
        }

        private void SetSource(List<PlaybackItem> items, int index)
        {
            if (index >= 0 && index <= items.Count - 1)
            {
                CurrentPlayback = items[index];
            }
        }

        public void Clear()
        {
            //Execute.BeginOnUIThread(() => CurrentItem = null);
            CurrentPlayback = null;
            Dispose();
        }

        public void Enqueue(Message message)
        {
            if (message == null)
            {
                return;
            }

            var previous = _items;
            if (previous != null)
            {
                var already = previous.FirstOrDefault(x => x.Message.Id == message.Id && x.Message.ChatId == message.ChatId);
                if (already != null)
                {
                    CurrentPlayback = already;
                    return;
                }
            }

            Dispose();

            var item = GetPlaybackItem(message);

            var items = _items = new List<PlaybackItem>();
            _items.Add(item);

            CurrentPlayback = item;

            if (message.Content is MessageText)
            {
                return;
            }

            var offset = -49;
            var filter = message.Content is MessageAudio ? new SearchMessagesFilterAudio() : (SearchMessagesFilter)new SearchMessagesFilterVoiceAndVideoNote();

            _protoService.Send(new SearchChatMessages(message.ChatId, string.Empty, null, message.Id, offset, 100, filter, 0), result =>
            {
                if (result is Messages messages)
                {
                    foreach (var add in message.Content is MessageAudio ? messages.MessagesValue.OrderBy(x => x.Id) : messages.MessagesValue.OrderByDescending(x => x.Id))
                    {
                        if (add.Id > message.Id && message.Content is MessageAudio)
                        {
                            items.Insert(0, GetPlaybackItem(add));
                        }
                        else if (add.Id < message.Id && (message.Content is MessageVoiceNote || message.Content is MessageVideoNote))
                        {
                            items.Insert(0, GetPlaybackItem(add));
                        }
                    }

                    foreach (var add in message.Content is MessageAudio ? messages.MessagesValue.OrderByDescending(x => x.Id) : messages.MessagesValue.OrderBy(x => x.Id))
                    {
                        if (add.Id < message.Id && message.Content is MessageAudio)
                        {
                            items.Add(GetPlaybackItem(add));
                        }
                        else if (add.Id > message.Id && (message.Content is MessageVoiceNote || message.Content is MessageVideoNote))
                        {
                            items.Add(GetPlaybackItem(add));
                        }
                    }

                    UpdateTransport();
                    PlaylistChanged?.Invoke(this, EventArgs.Empty);
                }
            });
        }

        private async Task<PlaybackItem> CreateAudioGraphAsync(PlaybackItem item)
        {
            try
            {
                AudioGraphSettings settings = new AudioGraphSettings(AudioRenderCategory.Media);
                CreateAudioGraphResult result = await AudioGraph.CreateAsync(settings);

                if (result.Status != AudioGraphCreationStatus.Success)
                {
                    return null;
                }

                AudioGraph graph = result.Graph;

                CreateAudioDeviceOutputNodeResult deviceOutputNodeResult = await graph.CreateDeviceOutputNodeAsync();

                if (deviceOutputNodeResult.Status != AudioDeviceNodeCreationStatus.Success)
                {
                    return null;
                }

                AudioDeviceOutputNode deviceOutput = deviceOutputNodeResult.DeviceOutputNode;

                AudioFrameOutputNode frameOutput = graph.CreateFrameOutputNode();

                CreateMediaSourceAudioInputNodeResult fileInputResult = await graph.CreateMediaSourceAudioInputNodeAsync(item.Source);
                if (MediaSourceAudioInputNodeCreationStatus.Success != fileInputResult.Status)
                {
                    return null;
                }

                MediaSourceAudioInputNode fileInput = fileInputResult.Node;

                fileInput.AddOutgoingConnection(deviceOutput);
                fileInput.AddOutgoingConnection(frameOutput);

                fileInput.OutgoingGain = _settingsService.VolumeLevel;
                fileInput.PlaybackSpeedFactor = _playbackRate;

                fileInput.MediaSourceCompleted += OnMediaEnded;

                graph.QuantumProcessed += OnQuantumProcessed;
                graph.Start();

                _inputNode = fileInput;
                _outputNode = frameOutput;
                _audioGraph = graph;

                return item;
            }
            catch
            {
                return null;
            }
        }

        private PlaybackItem GetPlaybackItem(Message message)
        {
            var token = $"{message.ChatId}_{message.Id}";
            var file = GetFile(message);
            var mime = GetMimeType(message);
            var duration = GetDuration(message);

            var stream = new RemoteFileStream(_protoService, file, TimeSpan.FromSeconds(duration));
            var source = MediaSource.CreateFromStream(stream, mime);
            var item = new PlaybackItem(source);

            _streams[file.Id].Add(stream);

            source.CustomProperties["file"] = file.Id;
            source.CustomProperties["message"] = message.Id;
            source.CustomProperties["chat"] = message.ChatId;
            source.CustomProperties["token"] = token;

            item.File = file;
            item.Message = message;
            item.Token = token;

            if (message.Content is MessageAudio audio)
            {
                var performer = string.IsNullOrEmpty(audio.Audio.Performer) ? null : audio.Audio.Performer;
                var title = string.IsNullOrEmpty(audio.Audio.Title) ? null : audio.Audio.Title;

                if (performer == null && title == null)
                {
                    item.Title = audio.Audio.FileName;
                    item.Artist = string.Empty;
                }
                else
                {
                    item.Title = string.IsNullOrEmpty(audio.Audio.Title) ? Strings.Resources.AudioUnknownTitle : audio.Audio.Title;
                    item.Artist = string.IsNullOrEmpty(audio.Audio.Performer) ? Strings.Resources.AudioUnknownArtist : audio.Audio.Performer;
                }
            }

            _mapping[token] = item;

            return item;
        }

        private File GetFile(Message message)
        {
            if (message.Content is MessageAudio audio)
            {
                return audio.Audio.AudioValue;
            }
            else if (message.Content is MessageVoiceNote voiceNote)
            {
                return voiceNote.VoiceNote.Voice;
            }
            else if (message.Content is MessageVideoNote videoNote)
            {
                return videoNote.VideoNote.Video;
            }
            else if (message.Content is MessageText text && text.WebPage != null)
            {
                if (text.WebPage.Audio != null)
                {
                    return text.WebPage.Audio.AudioValue;
                }
                else if (text.WebPage.VoiceNote != null)
                {
                    return text.WebPage.VoiceNote.Voice;
                }
                else if (text.WebPage.VideoNote != null)
                {
                    return text.WebPage.VideoNote.Video;
                }
            }

            return null;
        }

        private string GetMimeType(Message message)
        {
            if (message.Content is MessageAudio audio)
            {
                return audio.Audio.MimeType;
            }
            else if (message.Content is MessageVoiceNote voiceNote)
            {
                return voiceNote.VoiceNote.MimeType;
            }
            else if (message.Content is MessageVideoNote videoNote)
            {
                return "video/mp4";
            }
            else if (message.Content is MessageText text && text.WebPage != null)
            {
                if (text.WebPage.Audio != null)
                {
                    return text.WebPage.Audio.MimeType;
                }
                else if (text.WebPage.VoiceNote != null)
                {
                    return text.WebPage.VoiceNote.MimeType;
                }
                else if (text.WebPage.VideoNote != null)
                {
                    return "video/mp4";
                }
            }

            return null;
        }

        private int GetDuration(Message message)
        {
            if (message.Content is MessageAudio audio)
            {
                return audio.Audio.Duration;
            }
            else if (message.Content is MessageVoiceNote voiceNote)
            {
                return voiceNote.VoiceNote.Duration;
            }
            else if (message.Content is MessageVideoNote videoNote)
            {
                return videoNote.VideoNote.Duration;
            }
            else if (message.Content is MessageText text && text.WebPage != null)
            {
                if (text.WebPage.Audio != null)
                {
                    return text.WebPage.Audio.Duration;
                }
                else if (text.WebPage.VoiceNote != null)
                {
                    return text.WebPage.VoiceNote.Duration;
                }
                else if (text.WebPage.VideoNote != null)
                {
                    return text.WebPage.VideoNote.Duration;
                }
            }

            return 0;
        }

        public void Handle(UpdateFile update)
        {
            if (_streams.TryGetValue(update.File.Id, out List<RemoteFileStream> streams))
            {
                foreach (var stream in streams)
                {
                    stream.UpdateFile(update.File);
                }
            }
        }

        private void Dispose()
        {
            _inputNode = null;

            _audioGraph?.Stop();
            _audioGraph = null;

            //if (_playlist != null)
            //{
            //    _playlist.CurrentItemChanged -= OnCurrentItemChanged;
            //    _playlist.Items.Clear();
            //    _playlist = null;
            //}

            if (_queue != null)
            {
                _queue.Clear();
                _queue = null;
            }

            if (_items != null)
            {
                // TODO: anything else?
                _items = null;
            }

            if (_mapping != null)
            {
                _mapping.Clear();
            }
        }

        public bool IsSupportedPlaybackRateRange(double min, double max)
        {
            return true;
        }
    }

    public class PlaybackItem
    {
        public MediaSource Source { get; set; }
        public string Token { get; set; }

        public Message Message { get; set; }

        public File File { get; set; }

        public string Title { get; set; }
        public string Artist { get; set; }

        public PlaybackItem(MediaSource source)
        {
            Source = source;
        }

        public bool UpdateFile(File file)
        {
            return Message.UpdateFile(file);
        }
    }
}
