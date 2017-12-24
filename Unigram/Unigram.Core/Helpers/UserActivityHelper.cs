using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Template10.Common;
using Windows.ApplicationModel.UserActivities;
using Windows.Foundation.Metadata;

namespace Unigram.Core.Helpers
{
    public static class UserActivityHelper
    {
        private static UserActivityChannel _activityChannel;
        private static UserActivityChannel ActivityChannel
        {
            get
            {
                if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 5))
                {
                    return _activityChannel ?? (_activityChannel = UserActivityChannel.GetDefault());
                }

                return null;
            }
        }

        private static ICacheService _cacheService;
        private static ICacheService CacheService
        {
            get
            {
                return _cacheService ?? (_cacheService = Views.UnigramContainer.Current.ResolveType<ICacheService>());
            }
        }

        public static async Task<UserActivitySession> GenerateUserActivityAsync(TLPeerBase peer)
        {
            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 5))
            {
                UserActivity activity = 
                    await ActivityChannel.GetOrCreateUserActivityAsync("DialogActivity");

                string url = $"tg://toast?";
                string title = "";

                if (peer is TLPeerUser peerUser)
                {
                    TLUser user = (TLUser)CacheService.GetUser(peerUser.UserId);

                    if (!user.IsSelf && !user.IsDeleted)
                    {
                        url = $"{url}from_id={user.Id}";
                        title = user.FullName;
                    }
                }
                else if (peer is TLPeerChat peerChat)
                {
                    TLChat chat = (TLChat)CacheService.GetChat(peerChat.ChatId);

                    if (!chat.IsKicked && !chat.IsLeft && !chat.IsDeactivated)
                    {
                        url = $"{url}chat_id={chat.Id}";
                        title = chat.Title;
                    }
                }
                else if (peer is TLPeerChannel peerChannel)
                {
                    TLChannel channel = (TLChannel)CacheService.GetChat(peerChannel.ChannelId);

                    if (!channel.IsLeft)
                    {
                        url = $"{url}channel_id={channel.Id}";
                        title = channel.Title;
                    }
                }

                activity.ActivationUri = new Uri(url);
                activity.VisualElements.DisplayText = title;

                await activity.SaveAsync();

                return (BootStrapper.Current.NavigationService.Dispatcher.Dispatch(() => activity.CreateSession()));
            }

            return null;
        }
    }
}
