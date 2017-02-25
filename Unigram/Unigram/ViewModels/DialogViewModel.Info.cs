using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api;
using Telegram.Api.Helpers;
using Telegram.Api.TL;

namespace Unigram.ViewModels
{
    public partial class DialogViewModel
    {
        public bool HasBots
        {
            get
            {
                var user = With as TLUser;
                if (user != null && user.IsBot)
                {
                    return true;
                }

                var channel = With as TLChannel;
                if (channel != null && channel.IsBroadcast)
                {
                    return false;
                }

                var chat = With as TLChatBase;
                return chat != null && chat.BotInfo != null && chat.BotInfo.Count > 0;
            }
        }

        public bool HasBotsCommands
        {
            get
            {
                var user = With as TLUser;
                if (user != null && user.IsBot && user.BotInfo?.Commands.Count > 0)
                {
                    return true;
                }

                var channel = With as TLChannel;
                if (channel != null && channel.IsBroadcast)
                {
                    return false;
                }

                var chat = With as TLChatBase;
                return chat != null && chat.BotInfo != null && chat.BotInfo.Count > 0 && chat.BotInfo.Any(x => x.Commands?.Count > 0);
            }
        }

        private async void GetFullInfo()
        {
            var user = With as TLUser;
            if (user == null)
            {
                return;
            }
            var result = await ProtoService.GetFullUserAsync(new TLInputUser { UserId = user.Id, AccessHash = user.AccessHash.Value });
            if (result.IsSucceeded)
            {
                var userFull = result.Result;
                user.Link = userFull.Link;
                user.ProfilePhoto = userFull.ProfilePhoto;
                user.NotifySettings = userFull.NotifySettings;
                user.IsBlocked = userFull.IsBlocked;
                user.BotInfo = userFull.BotInfo;
                //user.About = userFull.About;

                Execute.BeginOnUIThread(() =>
                {
                    RaisePropertyChanged(() => HasBots);
                    RaisePropertyChanged(() => With);
                    //this.Subtitle = this.GetSubtitle();
                });
            }
        }
    }
}
