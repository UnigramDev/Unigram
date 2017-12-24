using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using Windows.Devices.Enumeration;
using Windows.Devices.Sensors;
using Windows.Foundation.Metadata;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Phone.Media.Devices;

namespace Unigram.Services
{
    public interface IPlaybackService : INotifyPropertyChanged
    {
        IReadOnlyList<TLMessage> Items { get; }

        MediaPlaybackSession Session { get; }
        MediaPlaybackList List { get; }

        TLMessage CurrentItem { get; }

        void Pause();
        void Play();

        void SetPosition(TimeSpan span);

        void Clear();

        void Enqueue(TLMessage message);
    }

    public class PlaybackService : ServiceBase, IPlaybackService
    {
        private readonly IMTProtoService _protoService;
        private readonly ICacheService _cacheService;
        private readonly IDownloadAudioFileManager _downloadManager;
        private readonly ITelegramEventAggregator _aggregator;

        private readonly MediaPlaybackItem _silence;

        private MediaPlayer _mediaPlayer;
        private MediaPlaybackList _playlist;

        private Dictionary<MediaPlaybackItem, TLMessage> _mapping;
        private Dictionary<TLMessage, MediaPlaybackItem> _inverse;

        private List<TLMessage> _items;
        private Queue<TLMessage> _queue;

        public PlaybackService(IMTProtoService protoService, ICacheService cacheService, IDownloadAudioFileManager downloadManager, ITelegramEventAggregator aggregator)
        {
            _protoService = protoService;
            _cacheService = cacheService;
            _downloadManager = downloadManager;
            _aggregator = aggregator;

            _silence = new MediaPlaybackItem(MediaSource.CreateFromUri(new Uri("ms-appx:///Assets/Audio/silence.mp3")));

            _mediaPlayer = new MediaPlayer();
            _mediaPlayer.CommandManager.IsEnabled = false;

            _mapping = new Dictionary<MediaPlaybackItem, TLMessage>();
            _inverse = new Dictionary<TLMessage, MediaPlaybackItem>();
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
            if (args.NewItem == null)
            {
                //Execute.BeginOnUIThread(() => CurrentItem = null);
                //Dispose();

                //Debug.WriteLine("PlaybackService: Playback completed");
            }
            else if (_mapping.TryGetValue(args.NewItem, out TLMessage message))
            {
                Execute.BeginOnUIThread(() => CurrentItem = message);
                Debug.WriteLine("PlaybackService: Playing message " + message.Id);

                MarkAsRead(message);
            }
            else
            {
                Execute.BeginOnUIThread(() => CurrentItem = null);
                Debug.WriteLine("PlaybackService: Current item changed, can't find related message");
            }

            if (_queue != null && _queue.Count > 0)
            {
                Enqueue(_queue.Dequeue(), false);
            }
        }

        private void OnMediaEnded(MediaPlayer sender, object args)
        {
            Debug.WriteLine("PlaybackService: OnMediaEnded");

            Execute.BeginOnUIThread(() => CurrentItem = null);
            Dispose();
        }

        public IReadOnlyList<TLMessage> Items => _items ?? (IReadOnlyList<TLMessage>)new TLMessage[0];

        public MediaPlaybackSession Session => _mediaPlayer.PlaybackSession;
        public MediaPlaybackList List => _playlist;

        private TLMessage _currentItem;
        public TLMessage CurrentItem
        {
            get
            {
                return _currentItem;
            }
            private set
            {
                _currentItem = value;
                RaisePropertyChanged(() => CurrentItem);
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
            Execute.BeginOnUIThread(() => CurrentItem = null);
            Dispose();
        }

        public void Enqueue(TLMessage message)
        {
            if (message == null)
            {
                return;
            }

            if (_mediaPlayer.Source == _playlist && _mediaPlayer.Source != null && _playlist != null && _inverse.TryGetValue(message, out MediaPlaybackItem item) && _playlist.Items.Contains(item))
            {
                var index = _playlist.Items.IndexOf(item);
                if (index >= 0)
                {
                    _playlist.MoveTo((uint)index);
                    return;
                }
            }

            Dispose();

            var peer = message.Parent?.ToInputPeer();
            var voice = message.IsVoice();

            _mediaPlayer.CommandManager.IsEnabled = !voice;
            //_mediaPlayer.AudioDeviceType = voice ? MediaPlayerAudioDeviceType.Communications : MediaPlayerAudioDeviceType.Multimedia;
            //_mediaPlayer.AudioCategory = voice ? MediaPlayerAudioCategory.Communications : MediaPlayerAudioCategory.Media;

            if (peer != null)
            {
                var filter = voice
                    ? new Func<TLMessageBase, bool>(x => x.Id > message.Id && x is TLMessage xm && xm.IsVoice())
                    : new Func<TLMessageBase, bool>(x => x.Id < message.Id && x is TLMessage xm && xm.IsMusic());

                //var response = await _protoService.SearchAsync(peer, null, null, filter, message.Date + 1, int.MaxValue, 0, 0, 50);
                //if (response.IsSucceeded)
                //{
                //    _queue = new Queue<TLMessage>(response.Result.Messages.OfType<TLMessage>().Reverse());
                //}

                _cacheService.GetHistoryAsync(message.Parent.ToPeer(), result =>
                {
                    var items = result.OfType<TLMessage>();
                    if (voice)
                    {
                        items = items.Reverse();
                    }

                    _queue = new Queue<TLMessage>(result.OfType<TLMessage>().Reverse());
                    _items = new List<TLMessage>(new[] { message }.Union(items));

                    Enqueue(message, true);

                }, predicate: filter);
            }

            //if (voice)
            //{
            //    await AttachAsync();
            //}
        }

        private void Enqueue(TLMessage message, bool play)
        {
            if (message.Media is TLMessageMediaDocument documentMedia && documentMedia.Document is TLDocument document)
            {
                var fileName = document.GetFileName();
                if (File.Exists(FileUtils.GetTempFileName(fileName)))
                {
                    var item = new MediaPlaybackItem(MediaSource.CreateFromUri(FileUtils.GetTempFileUri(fileName)));
                    _mapping[item] = message;
                    _inverse[message] = item;

                    if (play)
                    {
                        _playlist = new MediaPlaybackList();
                        _playlist.CurrentItemChanged += OnCurrentItemChanged;
                        _playlist.Items.Add(item);
                        _playlist.Items.Add(_silence);

                        _mediaPlayer.Source = _playlist;
                        _mediaPlayer.Play();
                    }
                    else
                    {
                        _playlist.Items.Insert(_playlist.Items.Count - 1, item);
                    }
                }
                else
                {
                    document.DownloadAsync(_downloadManager, x => Enqueue(message, play));
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

        private void MarkAsRead(TLMessage message)
        {
            if (message.IsMediaUnread && !message.IsOut)
            {
                message.IsMediaUnread = false;
                message.RaisePropertyChanged(() => message.IsMediaUnread);

                var vector = new TLVector<int> { message.Id };
                if (message.Parent is TLChannel channel)
                {
                    _aggregator.Publish(new TLUpdateChannelReadMessagesContents { ChannelId = channel.Id, Messages = vector });
                    _protoService.ReadMessageContentsAsync(channel.ToInputChannel(), vector, null);
                }
                else
                {
                    _aggregator.Publish(new TLUpdateReadMessagesContents { Messages = vector });
                    _protoService.ReadMessageContentsAsync(vector, null);
                }
            }
        }
    }
}
