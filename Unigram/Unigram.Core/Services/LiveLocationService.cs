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

        bool IsTracking(TLPeerBase peer);
        void StopTracking(TLPeerBase peer);

        MvxObservableCollection<TLMessage> Items { get; }
        MvxObservableCollection<ITLDialogWith> Peers { get; }
    }

    public class LiveLocationService : ILiveLocationService
    {
        private readonly IMTProtoService _protoService;
        private readonly ILocationService _locationService;

        private Geolocator _locator;

        public LiveLocationService(IMTProtoService protoService, ILocationService locationService)
        {
            _protoService = protoService;
            _locationService = locationService;

            Items = new MvxObservableCollection<TLMessage>();
            Peers = new MvxObservableCollection<ITLDialogWith>();
        }

        public MvxObservableCollection<TLMessage> Items { get; private set; }
        public MvxObservableCollection<ITLDialogWith> Peers { get; private set; }

        public bool IsTracking(TLPeerBase peer)
        {
            return Peers.Any(x => peer.Equals(x.ToPeer()));
        }

        public void StopTracking(TLPeerBase peer)
        {
            var message = Items.FirstOrDefault(x => peer.Equals(x.Parent.ToPeer()));
            if (message != null)
            {
                Items.Remove(message);
                Peers.Remove(message.Parent);

                Update(message, null, true);
            }
        }

        public async Task TrackAsync(TLMessage message)
        {
            Items.Add(message);
            Peers.Add(message.Parent);

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
            foreach (var message in Items.ToList())
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

                    if (geoPoint != null)
                    {
                        geoLiveMedia.Geo = geoPoint.ToGeoPoint();
                        geoLiveMedia.RaisePropertyChanged(() => geoLiveMedia.Geo);
                    }

                    if (stop)
                    {
                        geoLiveMedia.Period = 0;
                        geoLiveMedia.RaisePropertyChanged(() => geoLiveMedia.Period);
                    }

                    _protoService.EditGeoLiveAsync(peer, message.Id, geoPoint, stop, null);
                }
                else
                {
                    Items.Remove(message);
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
