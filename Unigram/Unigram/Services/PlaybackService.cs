using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Template10.Mvvm;
using Unigram.Common;
using Unigram.Services.Updates;
using Windows.Devices.Enumeration;
using Windows.Devices.Sensors;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Phone.Media.Devices;

namespace Unigram.Services
{
    public interface IPlaybackService : INotifyPropertyChanged
    {
        IReadOnlyList<Message> Items { get; }

        MediaPlaybackList List { get; }

        Message CurrentItem { get; }

        double PlaybackRate { get; set; }

        void Pause();
        void Play();

        void SetPosition(TimeSpan span);

        void Clear();

        void Enqueue(Message message);

        TimeSpan Position { get; }
        TimeSpan Duration { get; }

        MediaPlaybackState PlaybackState { get; }



        bool IsSupportedPlaybackRateRange(double min, double max);



        event TypedEventHandler<MediaPlaybackSession, object> PlaybackStateChanged;
        event TypedEventHandler<MediaPlaybackSession, object> PositionChanged;
    }

    public class PlaybackService : BindableBase, IPlaybackService, IHandle<UpdateFile>
    {
        private readonly IProtoService _protoService;
        private readonly ICacheService _cacheService;
        private readonly IEventAggregator _aggregator;

        private MediaPlayer _mediaPlayer;
        private MediaPlaybackList _playlist;

        private SystemMediaTransportControls _transport;

        private Dictionary<string, Message> _mapping;
        private Dictionary<string, Deferral> _inverse;
        private Dictionary<string, MediaBindingEventArgs> _binders;

        private List<Message> _items;
        private Queue<Message> _queue;

        public event TypedEventHandler<MediaPlaybackSession, object> PlaybackStateChanged;
        public event TypedEventHandler<MediaPlaybackSession, object> PositionChanged;

        public PlaybackService(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
        {
            _protoService = protoService;
            _cacheService = cacheService;
            _aggregator = aggregator;

            _mediaPlayer = new MediaPlayer();
            _mediaPlayer.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;
            _mediaPlayer.PlaybackSession.PositionChanged += OnPositionChanged;
            //_mediaPlayer.CommandManager.IsEnabled = false;

            //_transport = _mediaPlayer.SystemMediaTransportControls;

            _mapping = new Dictionary<string, Message>();
            _inverse = new Dictionary<string, Deferral>();
            _binders = new Dictionary<string, MediaBindingEventArgs>();

            aggregator.Subscribe(this);
        }

        private void OnPlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            if (sender.PlaybackState == MediaPlaybackState.Playing && sender.PlaybackRate != _playbackRate)
            {
                sender.PlaybackRate = _playbackRate;
            }
            else if (sender.PlaybackState == MediaPlaybackState.Paused && sender.Position == sender.NaturalDuration && _playlist.CurrentItem == _playlist.Items.LastOrDefault())
            {
                Clear();
            }

            PlaybackStateChanged?.Invoke(sender, args);
        }

        private void OnPositionChanged(MediaPlaybackSession sender, object args)
        {
            PositionChanged?.Invoke(sender, args);
        }

        #region Proximity

        private ProximitySensor _sensor;
        private ProximitySensorDisplayOnOffController _controller;

        private async Task AttachAsync()
        {
            if (ApiInformation.IsApiContractPresent("Windows.Phone.PhoneContract", 1))
            {
                var devices = await DeviceInformation.FindAllAsync(ProximitySensor.GetDeviceSelector());
                if (devices.Count > 0)
                {
                    _sensor = ProximitySensor.FromId(devices[0].Id);
                    //_sensor.ReadingChanged += OnReadingChanged;

                    _controller = _sensor.CreateDisplayOnOffController();
                }
            }
        }

        private void OnReadingChanged(ProximitySensor sender, ProximitySensorReadingChangedEventArgs args)
        {
            AudioRoutingManager.GetDefault().SetAudioEndpoint(args.Reading.IsDetected ? AudioRoutingEndpoint.Earpiece : AudioRoutingEndpoint.Speakerphone);
        }

        #endregion

        private void OnCurrentItemChanged(MediaPlaybackList sender, CurrentMediaPlaybackItemChangedEventArgs args)
        {
            if (args.NewItem != null && _mapping.TryGetValue((string)args.NewItem.Source.CustomProperties["token"], out Message value))
            {
                CurrentItem = value;
            }
            else
            {
                CurrentItem = null;
            }

            return;

            if (args.NewItem != null && _playlist.CurrentItemIndex == _playlist.Items.Count - 1)
            {
                if (_mapping.TryGetValue((string)args.NewItem.Source.CustomProperties["token"], out Message message))
                {
                    var offset = message.Content is MessageAudio ? 0 : -99;
                    var filter = message.Content is MessageAudio ? new SearchMessagesFilterAudio() : (SearchMessagesFilter)new SearchMessagesFilterVoiceNote();

                    _protoService.Send(new SearchChatMessages(message.ChatId, string.Empty, 0, message.Id, offset, 100, filter), result =>
                    {
                        if (result is Messages messages)
                        {
                            foreach (var add in message.Content is MessageAudio ? messages.MessagesValue.OrderByDescending(x => x.Id) : messages.MessagesValue.OrderBy(x => x.Id))
                            {
                                if (add.Id < message.Id && message.Content is MessageAudio)
                                {
                                    _playlist.Items.Add(GetPlaybackItem(add));
                                }
                                else if (add.Id > message.Id && message.Content is MessageVoiceNote)
                                {
                                    _playlist.Items.Add(GetPlaybackItem(add));
                                }
                            }
                        }
                    });
                }
            }

            if (args.NewItem == null)
            {
                //Execute.BeginOnUIThread(() => CurrentItem = null);
                //Dispose();

                //Debug.WriteLine("PlaybackService: Playback completed");
            }
            //else if (_mapping.TryGetValue(args.NewItem, out TLMessage message))
            //{
            //    Execute.BeginOnUIThread(() => CurrentItem = message);
            //    Debug.WriteLine("PlaybackService: Playing message " + message.Id);

            //    MarkAsRead(message);
            //}
            //else
            //{
            //    Execute.BeginOnUIThread(() => CurrentItem = null);
            //    Debug.WriteLine("PlaybackService: Current item changed, can't find related message");
            //}

            if (_queue != null && _queue.Count > 0)
            {
                //Enqueue(_queue.Dequeue(), false);
            }
        }

        private void OnMediaEnded(MediaPlayer sender, object args)
        {
            Debug.WriteLine("PlaybackService: OnMediaEnded");

            //Execute.BeginOnUIThread(() => CurrentItem = null);
            CurrentItem = null;
            Dispose();
        }

        public IReadOnlyList<Message> Items => _items ?? (IReadOnlyList<Message>)new Message[0];

        public MediaPlaybackSession Session => _mediaPlayer.PlaybackSession;
        public MediaPlaybackList List => _playlist;

        private Message _currentItem;
        public Message CurrentItem
        {
            get
            {
                return _currentItem;
            }
            private set
            {
                _currentItem = value;
                _aggregator.Publish(new UpdatePlaybackItem(value));
                RaisePropertyChanged(() => CurrentItem);
            }
        }

        public TimeSpan Position => _mediaPlayer?.PlaybackSession?.Position ?? TimeSpan.Zero;
        public TimeSpan Duration => _mediaPlayer?.PlaybackSession?.NaturalDuration ?? TimeSpan.Zero;

        public MediaPlaybackState PlaybackState => _mediaPlayer?.PlaybackSession?.PlaybackState ?? MediaPlaybackState.None;

        private double _playbackRate = 1.0;
        public double PlaybackRate
        {
            get
            {
                return _playbackRate;
            }
            set
            {
                _playbackRate = value;
                _mediaPlayer.PlaybackSession.PlaybackRate = value;
            }
        }

        public void Pause()
        {
            if (_mediaPlayer.PlaybackSession.CanPause)
            {
                _mediaPlayer.Pause();
            }
        }

        public void Play()
        {
            _mediaPlayer.Play();
        }

        public void SetPosition(TimeSpan span)
        {
            var index = _playlist.CurrentItemIndex;
            var playing = _mediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing;

            _mediaPlayer.Pause();
            _playlist.MoveTo(index);
            _mediaPlayer.PlaybackSession.Position = span;

            if (playing)
            {
                _mediaPlayer.Play();
            }
        }

        public void Clear()
        {
            //Execute.BeginOnUIThread(() => CurrentItem = null);
            CurrentItem = null;
            Dispose();
        }

        public void Enqueue(Message message)
        {
            if (message == null)
            {
                return;
            }

            //if (_mediaPlayer.Source == _playlist && _mediaPlayer.Source != null && _playlist != null && _inverse.TryGetValue(message, out MediaPlaybackItem item) && _playlist.Items.Contains(item))
            //{
            //    var index = _playlist.Items.IndexOf(item);
            //    if (index >= 0)
            //    {
            //        _playlist.MoveTo((uint)index);
            //        return;
            //    }
            //}

            Dispose();

            Enqueue(message, true);

            //var peer = message.Parent?.ToInputPeer();
            //var voice = message.IsVoice();

            //_mediaPlayer.CommandManager.IsEnabled = !voice;
            ////_mediaPlayer.AudioDeviceType = voice ? MediaPlayerAudioDeviceType.Communications : MediaPlayerAudioDeviceType.Multimedia;
            ////_mediaPlayer.AudioCategory = voice ? MediaPlayerAudioCategory.Communications : MediaPlayerAudioCategory.Media;

            //if (peer != null)
            //{
            //    var filter = voice
            //        ? new Func<TLMessageBase, bool>(x => x.Id > message.Id && x is TLMessage xm && xm.IsVoice())
            //        : new Func<TLMessageBase, bool>(x => x.Id < message.Id && x is TLMessage xm && xm.IsMusic());

            //    //var response = await _protoService.SearchAsync(peer, null, null, filter, message.Date + 1, int.MaxValue, 0, 0, 50);
            //    //if (response.IsSucceeded)
            //    //{
            //    //    _queue = new Queue<TLMessage>(response.Result.Messages.OfType<TLMessage>().Reverse());
            //    //}

            //    _cacheService.GetHistoryAsync(message.Parent.ToPeer(), result =>
            //    {
            //        var items = result.OfType<TLMessage>();
            //        if (voice)
            //        {
            //            items = items.Reverse();
            //        }

            //        _queue = new Queue<TLMessage>(result.OfType<TLMessage>().Reverse());
            //        _items = new List<TLMessage>(new[] { message }.Union(items));

            //        Enqueue(message, true);

            //    }, predicate: filter);
            //}

            ////if (voice)
            ////{
            ////    await AttachAsync();
            ////}
        }

        private void Enqueue(Message message, bool play)
        {
            var item = GetPlaybackItem(message);

            _playlist = new MediaPlaybackList();
            _playlist.MaxPrefetchTime = TimeSpan.FromMinutes(10);
            _playlist.CurrentItemChanged += OnCurrentItemChanged;
            _playlist.Items.Add(item);

            _mediaPlayer.CommandManager.IsEnabled = !(message.Content is MessageVoiceNote);
            //_mediaPlayer.AudioDeviceType = message.Content is MessageVoiceNote ? MediaPlayerAudioDeviceType.Communications : MediaPlayerAudioDeviceType.Multimedia;
            //_mediaPlayer.AudioCategory = message.Content is MessageVoiceNote ? MediaPlayerAudioCategory.Communications : MediaPlayerAudioCategory.Media;

            _mediaPlayer.Source = _playlist;
            _mediaPlayer.Play();
        }

        private MediaPlaybackItem GetPlaybackItem(Message message)
        {
            var token = message.Id.ToString();
            var file = GetFile(message);

            var binder = new MediaBinder();
            binder.Token = token;
            binder.Binding += Binder_Binding;

            var source = MediaSource.CreateFromMediaBinder(binder);
            var item = new MediaPlaybackItem(source);

            source.CustomProperties["file"] = file.Id;
            source.CustomProperties["message"] = message.Id;
            source.CustomProperties["chat"] = message.ChatId;
            source.CustomProperties["token"] = token;

            if (message.Content is MessageAudio audio)
            {
                var props = item.GetDisplayProperties();
                props.Type = MediaPlaybackType.Music;
                props.MusicProperties.Title = string.IsNullOrEmpty(audio.Audio.Title) ? Strings.Resources.AudioUnknownTitle : audio.Audio.Title;
                props.MusicProperties.Artist = string.IsNullOrEmpty(audio.Audio.Performer) ? Strings.Resources.AudioUnknownArtist : audio.Audio.Performer;

                item.ApplyDisplayProperties(props);
            }

            _mapping[token] = message;

            return item;
        }

        private void Binder_Binding(MediaBinder sender, MediaBindingEventArgs args)
        {
            var deferral = args.GetDeferral();
            if (_mapping.TryGetValue(args.MediaBinder.Token, out Message message))
            {
                var file = GetFile(message);
                if (file.Local.IsDownloadingCompleted)
                {
                    args.SetUri(new Uri("file:///" + file.Local.Path));
                    deferral.Complete();
                }
                else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
                {
                    _inverse[args.MediaBinder.Token] = deferral;
                    _binders[args.MediaBinder.Token] = args;
                    _protoService.Send(new DownloadFile(file.Id, 10, 0));
                }
            }
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
            }

            return null;
        }

        public void Handle(UpdateFile update)
        {
            if (update.File.Local.IsDownloadingCompleted)
            {
                foreach (var message in _mapping.Values)
                {
                    if (message.UpdateFile(update.File))
                    {
                        var token = message.Id.ToString();
                        if (_binders.TryGetValue(token, out MediaBindingEventArgs args) && _inverse.TryGetValue(token, out Deferral deferral))
                        {
                            args.SetUri(new Uri("file:///" + update.File.Local.Path));
                            deferral.Complete();
                        }
                    }
                }
            }
        }

        private void Dispose()
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Source = null;
            }

            if (_playlist != null)
            {
                _playlist.CurrentItemChanged -= OnCurrentItemChanged;
                _playlist.Items.Clear();
                _playlist = null;
            }

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

            if (_inverse != null)
            {
                _inverse.Clear();
            }

            //if (_controller != null)
            //{
            //    _controller.Dispose();
            //    _controller = null;
            //}

            //if (_sensor != null)
            //{
            //    _sensor.ReadingChanged -= OnReadingChanged;
            //    _sensor = null;
            //}
        }

        public bool IsSupportedPlaybackRateRange(double min, double max)
        {
            if (_mediaPlayer != null && ApiInformation.IsMethodPresent("Windows.Media.Playback.MediaPlaybackSession", "IsSupportedPlaybackRateRange"))
            {
                return _mediaPlayer.PlaybackSession.IsSupportedPlaybackRateRange(min,  max);
            }

            return false;
        }
    }
}
