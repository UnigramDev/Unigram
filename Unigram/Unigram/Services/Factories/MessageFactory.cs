using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.ViewModels;
using Unigram.ViewModels.Delegates;

namespace Unigram.Services.Factories
{
    public interface IMessageFactory
    {
        MessageViewModel Create(IMessageDelegate delegato, Message message);
    }

    public class MessageFactory : IMessageFactory
    {
        private readonly IProtoService _protoService;
        private readonly IPlaybackService _playbackService;
        private readonly IEventAggregator _aggregator;

        public MessageFactory(IProtoService protoService, IPlaybackService playbackService, IEventAggregator aggregator)
        {
            _protoService = protoService;
            _playbackService = playbackService;
            _aggregator = aggregator;
        }

        public MessageViewModel Create(IMessageDelegate delegato, Message message)
        {
            return new MessageViewModel(_protoService, _playbackService, delegato, message);
        }
    }
}
