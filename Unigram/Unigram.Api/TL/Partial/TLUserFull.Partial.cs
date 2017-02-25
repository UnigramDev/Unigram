using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public partial class TLUserFull
    {
        public TLUserBase ToUser()
        {
            User.Link = Link;
            User.ProfilePhoto = ProfilePhoto;
            User.NotifySettings = NotifySettings;
            User.IsBlocked = IsBlocked;
            User.BotInfo = BotInfo;

            return User;
        }
    }
}
