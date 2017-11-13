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
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Unigram.Services
{
    public interface IPlaybackService : INotifyPropertyChanged
    {
        MediaPlaybackSession Session { get; }
        TLMessage CurrentItem { get; }

        void Pause();
        void Play();

        void Clear();

        void Enqueue(TLMessage message);
    }

    public class PlaybackService : ServiceBase, IPlaybackService
    {
        private readonly IMTProtoService _protoService;
        private readonly ICacheService _cacheService;
        private readonly IDownloadAudioFileManager _downloadManager;
        private readonly ITelegramEventAggregator _aggregator;

        private readonly MediaSource _silence;

        private MediaPlayer _mediaPlayer;
        private MediaPlaybackList _playlist;

        private Dictionary<MediaPlaybackItem, TLMessage> _mapping;
        private Dictionary<TLMessage, MediaPlaybackItem> _inverse;
        private Queue<TLMessage> _queue;

        public PlaybackService(IMTProtoService protoService, ICacheService cacheService, IDownloadAudioFileManager downloadManager, ITelegramEventAggregator aggregator)
        {
            _protoService = protoService;
            _cacheService = cacheService;
            _downloadManager = downloadManager;
            _aggregator = aggregator;

            _silence = MediaSource.CreateFromUri(new Uri("ms-appx:///Assets/Audio/silence.mp3"));

            _mediaPlayer = new MediaPlayer();
            _mediaPlayer.CommandManager.IsEnabled = false;
            _mediaPlayer.MediaEnded += OnMediaEnded;

            _mapping = new Dictionary<MediaPlaybackItem, TLMessage>();
            _inverse = new Dictionary<TLMessage, MediaPlaybackItem>();
        }

        private void OnCurrentItemChanged(MediaPlaybackList sender, CurrentMediaPlaybackItemChangedEventArgs args)
        {
            if (args.NewItem == null)
            {
                Execute.BeginOnUIThread(() => CurrentItem = null);
                Debug.WriteLine("PlaybackService: Playback completed");
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

        public MediaPlaybackSession Session => _mediaPlayer.PlaybackSession;

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

        public void Clear()
        {
            Execute.BeginOnUIThread(() => CurrentItem = null);
            Dispose();
        }

        public void Enqueue(TLMessage message)
        {
            if (_mediaPlayer.Source == _playlist && _mediaPlayer.Source != null && _inverse.TryGetValue(message, out MediaPlaybackItem item) && _playlist.Items.Contains(item))
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
            if (peer != null)
            {
                var filter = message.IsVoice()
                    ? new Func<TLMessageBase, bool>(x => x.Id > message.Id && x is TLMessage xm && xm.IsVoice())
                    : new Func<TLMessageBase, bool>(x => x.Id > message.Id && x is TLMessage xm && xm.IsMusic());

                //var response = await _protoService.SearchAsync(peer, null, null, filter, message.Date + 1, int.MaxValue, 0, 0, 50);
                //if (response.IsSucceeded)
                //{
                //    _queue = new Queue<TLMessage>(response.Result.Messages.OfType<TLMessage>().Reverse());
                //}

                _cacheService.GetHistoryAsync(message.Parent.ToPeer(), result =>
                {
                    _queue = new Queue<TLMessage>(result.OfType<TLMessage>().Reverse());

                }, predicate: filter);
            }

            Enqueue(message, true);
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
                        _playlist.Items.Add(new MediaPlaybackItem(_silence));

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
            _mediaPlayer.Source = null;

            if (_playlist != null)
            {
                _playlist.Items.Clear();
                _playlist.CurrentItemChanged -= OnCurrentItemChanged;
                _playlist = null;
            }

            if (_queue != null)
            {
                _queue.Clear();
                _queue = null;
            }
        }

        private void MarkAsRead(TLMessage message)
        {
            if (message.IsMediaUnread && !message.IsOut)
            {
                message.IsMediaUnread = false;
                message.RaisePropertyChanged(() => message.IsMediaUnread);

                return;

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
