using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;

namespace Telegram.Api.TL
{
    public partial class TLAuthorization : INotifyPropertyChanged
    {
        public bool IsCurrent
        {
            get
            {
                return (Flags & 1) == 1;
            }
        }

        public bool IsOfficialApp
        {
            get
            {
                return (Flags & 2) == 2;
            }
        }

        public void Update(TLAuthorization authorization)
        {
            Hash = authorization.Hash;
            Flags = authorization.Flags;
            DeviceModel = authorization.DeviceModel;
            Platform = authorization.Platform;
            SystemVersion = authorization.SystemVersion;
            ApiId = authorization.ApiId;
            AppName = authorization.AppName;
            AppVersion = authorization.AppVersion;
            DateCreated = authorization.DateCreated;
            DateActive = authorization.DateActive;
            Ip = authorization.Ip;
            Country = authorization.Country;
            Region = authorization.Region;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public override void RaisePropertyChanged(string propertyName)
        {
            Execute.OnUIThread(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }
    }
}
