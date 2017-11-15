using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Core.Common;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class PlaybackViewModel : UnigramViewModelBase
    {
        private readonly IPlaybackService _playbackService;

        public PlaybackViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IPlaybackService playbackService)
            : base(protoService, cacheService, aggregator)
        {
            _playbackService = playbackService;

            Items = new MvxObservableCollection<TLMessage>();
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            Items.ReplaceWith(_playbackService.Items);
            return Task.CompletedTask;
        }

        public IPlaybackService Playback => _playbackService;

        public MvxObservableCollection<TLMessage> Items { get; private set; }
    }
}
