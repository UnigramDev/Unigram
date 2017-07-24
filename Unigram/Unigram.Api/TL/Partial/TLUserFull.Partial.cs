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
            User = userFull.User;
            About = userFull.About;
            Link = userFull.Link;
            ProfilePhoto = userFull.ProfilePhoto;
            NotifySettings = userFull.NotifySettings;
            BotInfo = userFull.BotInfo;
            CommonChatsCount = userFull.CommonChatsCount;

            RaisePropertyChanged(() => User);
            RaisePropertyChanged(() => About);
            RaisePropertyChanged(() => Link);
            RaisePropertyChanged(() => ProfilePhoto);
            RaisePropertyChanged(() => NotifySettings);
            RaisePropertyChanged(() => BotInfo);
            RaisePropertyChanged(() => CommonChatsCount);
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
