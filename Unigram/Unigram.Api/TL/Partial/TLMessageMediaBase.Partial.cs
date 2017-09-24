using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services.Cache;

namespace Telegram.Api.TL
{
    public abstract partial class TLMessageMediaBase : INotifyPropertyChanged
    {
        public virtual TLInputMediaBase ToInputMedia()
        {
            throw new NotImplementedException();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public override void RaisePropertyChanged(string propertyName)
        {
            Execute.OnUIThread(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }
    }

    public partial class TLMessageMediaEmpty
    {
        public override TLInputMediaBase ToInputMedia()
        {
            return new TLInputMediaEmpty();
        }
    }

    public partial class TLMessageMediaGame
    {
        public override TLInputMediaBase ToInputMedia()
        {
            return new TLInputMediaGame { Id = new TLInputGameID { Id = Game.Id, AccessHash = Game.AccessHash } };
        }
    }

    public partial class TLMessageMediaGeo
    {
        public override TLInputMediaBase ToInputMedia()
        {
            return new TLInputMediaGeoPoint { GeoPoint = Geo.ToInputGeoPoint() };
        }
    }

    public partial class TLMessageMediaGeoLive
    {
        public override TLInputMediaBase ToInputMedia()
        {
            return new TLInputMediaGeoLive { GeoPoint = Geo.ToInputGeoPoint(), Period = Period };
        }
    }

    //public partial class TLMessageMediaInvoice
    //{
    //    public override TLInputMediaBase ToInputMedia()
    //    {
    //        return new TLInputMediaInvoice();
    //    }
    //}

    public partial class TLMessageMediaVenue
    {
        public override TLInputMediaBase ToInputMedia()
        {
            return new TLInputMediaVenue { Address = Address, GeoPoint = Geo.ToInputGeoPoint(), Provider = Provider, Title = Title, VenueId = VenueId, VenueType = VenueType };
        }
    }

    public partial class TLMessageMediaContact
    {
        public override TLInputMediaBase ToInputMedia()
        {
            return new TLInputMediaContact { FirstName = FirstName, LastName = LastName, PhoneNumber = PhoneNumber };
        }
    }

    public partial class TLMessageMediaDocument
    {
        public override TLInputMediaBase ToInputMedia()
        {
            return new TLInputMediaDocument { Id = Document.ToInputDocument(), Caption = Caption };
        }
    }

    public partial class TLMessageMediaPhoto
    {
        public override TLInputMediaBase ToInputMedia()
        {
            return new TLInputMediaPhoto { Id = Photo.ToInputPhoto(), Caption = Caption };
        }
    }
}
