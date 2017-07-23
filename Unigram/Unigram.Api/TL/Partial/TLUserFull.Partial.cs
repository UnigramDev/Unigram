using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;

namespace Telegram.Api.TL
{
    public partial class TLUserFull : INotifyPropertyChanged
    {
        public void Update(TLUserFull userFull)
        {
            // TODO: update
        }

        //public TLUserBase ToUser()
        //{
        //    User.Link = Link;
        //    User.ProfilePhoto = ProfilePhoto;
        //    User.NotifySettings = NotifySettings;
        //    User.IsBlocked = IsBlocked;
        //    User.BotInfo = BotInfo;

        //    return User;
        //}

        public event PropertyChangedEventHandler PropertyChanged;
        public override void RaisePropertyChanged(string propertyName)
        {
            Execute.OnUIThread(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }
    }
}
