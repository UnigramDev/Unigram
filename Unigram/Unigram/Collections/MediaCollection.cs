using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Services;
using Telegram.Api.TL;
using Unigram.Common;

namespace Unigram.Collections
{
    public class MediaCollection : IncrementalCollection<TLMessage>
    {
        private readonly IMTProtoService _protoService;
        private readonly TLMessagesFilterBase _filter;
        private readonly TLInputPeerBase _peer;

        public MediaCollection(IMTProtoService protoService, TLInputPeerBase peer, TLMessagesFilterBase filter)
        {
            _protoService = protoService;
            _peer = peer;
            _filter = filter;
        }

        public override async Task<IEnumerable<TLMessage>> LoadDataAsync()
        {
            try
            {
                var maxId = 0;
                var last = this.LastOrDefault();
                if (last != null)
                {
                    maxId = last.Id;
                }

                var result = await _protoService.SearchAsync(_peer, string.Empty, _filter, 0, 0, 0, maxId, 50);
                if (result.IsSucceeded)
                {
                    return result.Value.Messages.OfType<TLMessage>();
                }
            }
            catch { }

            return new TLMessage[0];
        }
    }
}
