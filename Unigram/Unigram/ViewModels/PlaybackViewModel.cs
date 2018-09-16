using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Core.Common;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class PlaybackViewModel : TLViewModelBase
    {
        private readonly IPlaybackService _playbackService;

        public PlaybackViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, IPlaybackService playbackService)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _playbackService = playbackService;

            Items = new MvxObservableCollection<Message>();
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            Items.ReplaceWith(_playbackService.Items);
            return Task.CompletedTask;
        }

        public IPlaybackService Playback => _playbackService;

        public MvxObservableCollection<Message> Items { get; private set; }
    }
}
