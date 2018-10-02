using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Windows.Devices.Geolocation;

namespace Unigram.Services
{
    public interface ILiveLocationService
    {
        Task TrackAsync(Message message);

        //void Update(TLInputGeoPointBase geoPoint);

        //bool IsTracking(TLPeerBase peer);
        //void StopTracking(TLPeerBase peer);
        void StopTracking();

        MvxObservableCollection<Message> Items { get; }
    }

    public class LiveLocationService : ILiveLocationService
    {
        //private readonly IProtoService _protoService;
        //private readonly ILocationService _locationService;

        //private readonly IEventAggregator _aggregator;

        private Geolocator _locator;

        public LiveLocationService(/*IProtoService protoService, ILocationService locationService, IEventAggregator aggregator*/)
        {
            //_protoService = protoService;
            //_locationService = locationService;
            //_aggregator = aggregator;

            //_aggregator.Subscribe(this);

            //Items = new MvxObservableCollection<Message>();
        }

        public void Handle(object MessagesRemovedEventArgs)
        {
            //foreach (var message in args.Messages.OfType<TLMessage>())
            //{
            //    var removed = Items.Remove(message);
            //    if (removed == false)
            //    {
            //        var already = Items.FirstOrDefault(x => x.Id == message.Id && message.Parent == args.Dialog.With);
            //        if (already != null)
            //        {
            //            Items.Remove(already);
            //        }
            //    }
            //}

            //if (_locator != null && Items.IsEmpty())
            //{
            //    _locator.PositionChanged -= OnPositionChanged;
            //}
        }

        public MvxObservableCollection<Message> Items { get; private set; }

        //public bool IsTracking(TLPeerBase peer)
        //{
        //    return Items.Any(x => x.Parent != null && peer.Equals(x.Parent.ToPeer()));
        //}

        //public void StopTracking(TLPeerBase peer)
        //{
        //    var message = Items.FirstOrDefault(x => peer.Equals(x.Parent.ToPeer()));
        //    if (message != null)
        //    {
        //        Items.Remove(message);
        //        Update(message, null, true);
        //    }
        //}

        public void StopTracking()
        {
            //foreach (var message in Items.ToList())
            //{
            //    Items.Remove(message);
            //    //Update(message, null, true);
            //}
        }

        public async Task TrackAsync(Message message)
        {
            //Items.Add(message);

            //if (_locator == null)
            //{
            //    //_locator = await _locationService.StartTrackingAsync();
            //    //_locator.PositionChanged -= OnPositionChanged;
            //    //_locator.PositionChanged += OnPositionChanged;
            //}
        }

        private void OnPositionChanged(Geolocator sender, PositionChangedEventArgs args)
        {
            //Update(args.Position.ToInputGeoPoint());
        }

        //public void Update(TLInputGeoPointBase geoPoint)
        //{
        //    foreach (var message in Items.ToList())
        //    {
        //        Update(message, geoPoint, false);
        //    }
        //}

        //private void Update(TLMessage message, TLInputGeoPointBase geoPoint, bool stop)
        //{
        //    if (message.Media is TLMessageMediaGeoLive geoLiveMedia)
        //    {
        //        var expires = ConvertDate(message.Date + geoLiveMedia.Period);
        //        if (expires > DateTime.Now)
        //        {
        //            var peer = message.Parent?.ToInputPeer();
        //            if (peer == null)
        //            {
        //                return;
        //            }

        //            if (geoPoint != null)
        //            {
        //                //geoLiveMedia.Geo = geoPoint.ToGeoPoint();
        //                //geoLiveMedia.RaisePropertyChanged(() => geoLiveMedia.Geo);
        //            }

        //            if (stop)
        //            {
        //                geoLiveMedia.Period = 0;
        //            }

        //            //_protoService.EditMessageAsync(peer, message.Id, null, null, null, geoPoint, false, stop, null);
        //        }
        //        else
        //        {
        //            Items.Remove(message);
        //        }
        //    }

        //    if (_locator != null && Items.IsEmpty())
        //    {
        //        _locator.PositionChanged -= OnPositionChanged;
        //    }
        //}

        //private DateTime ConvertDate(int value)
        //{
        //    return Utils.UnixTimestampToDateTime(value);
        //}
    }
}
