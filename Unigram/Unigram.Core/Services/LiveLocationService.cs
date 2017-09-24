using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.TL;
using Unigram.Core.Common;
using Windows.Devices.Geolocation;

namespace Unigram.Core.Services
{
    public interface ILiveLocationService
    {
        Task TrackAsync(TLMessage message);

        void Update(TLInputGeoPointBase geoPoint);
    }

    public class LiveLocationService : ILiveLocationService
    {
        private readonly IMTProtoService _protoService;
        private readonly ILocationService _locationService;

        private readonly List<TLMessage> _items;

        private Geolocator _locator;

        public LiveLocationService(IMTProtoService protoService, ILocationService locationService)
        {
            _protoService = protoService;
            _locationService = locationService;

            _items = new List<TLMessage>();
        }

        public async Task TrackAsync(TLMessage message)
        {
            _items.Add(message);

            if (_locator == null)
            {
                _locator = await _locationService.StartTrackingAsync();
                _locator.PositionChanged += OnPositionChanged;
            } 
        }

        private void OnPositionChanged(Geolocator sender, PositionChangedEventArgs args)
        {
            Update(args.Position.ToInputGeoPoint());
        }

        public void Update(TLInputGeoPointBase geoPoint)
        {
            foreach (var message in _items.ToList())
            {
                Update(message, geoPoint, false);
            }
        }

        private void Update(TLMessage message, TLInputGeoPointBase geoPoint, bool stop)
        {
            if (message.Media is TLMessageMediaGeoLive geoLiveMedia)
            {
                var expires = ConvertDate(message.Date + geoLiveMedia.Period);
                if (expires > DateTime.Now)
                {
                    var peer = message.Parent?.ToInputPeer();
                    if (peer == null)
                    {
                        return;
                    }

                    _protoService.EditGeoLiveAsync(peer, message.Id, geoPoint, stop, null);
                }
                else
                {
                    _items.Remove(message);
                }
            }
        }

        private DateTime ConvertDate(int value)
        {
            var clientDelta = MTProtoService.Current.ClientTicksDelta;
            var utc0SecsLong = value * 4294967296 - clientDelta;
            var utc0SecsInt = utc0SecsLong / 4294967296.0;
            var dateTime = Utils.UnixTimestampToDateTime(utc0SecsInt);

            return dateTime;
        }
    }
}
