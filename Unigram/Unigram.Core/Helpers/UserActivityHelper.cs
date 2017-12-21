using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Template10.Common;
using Unigram.Core.Models;
using Unigram.Views;
using Windows.ApplicationModel.UserActivities;
using Windows.Foundation.Metadata;

namespace Unigram.Core.Helpers
{
    public static class UserActivityHelper
    {
        static UserActivityChannel _channel;
        static UserActivityChannel Channel
        {
            get
            {
                if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 5))
                {
                    return _channel ?? (_channel = UserActivityChannel.GetDefault());
                }

                return null;
            }
        }

        private static ICacheService _cacheService;
        private static ICacheService CacheService
        {
            get
            {
                return _cacheService ?? (_cacheService = UnigramContainer.Current.ResolveType<ICacheService>());
            }
        }

        public static async Task<UserActivitySession> GenerateActivityAsync(TLPeerBase peer)
        {
            Debug.WriteLine("GenerateActivityAsync : Checking whether Unigram is running on 16299 or higher...");
            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 5))
            {
                Debug.WriteLine("GenerateActivityAsync : Generating UserActivity object...");
                UserActivity activity = 
                    await Channel.GetOrCreateUserActivityAsync("DialogActivity");

                Debug.WriteLine("GenerateActivityAsync : Configuring activation URI...");
                string uri = "tg://toast?";
                string cardTitle = "";

                if (peer is TLPeerUser peerUser)
                {
                    Debug.WriteLine($"GenerateActivityAsync : TLUser detected. ID { peerUser.UserId }");
                    TLUserBase user = CacheService.GetUser(peerUser.UserId);
                    cardTitle = user.FullName;
                    uri = $"{ uri }user_id={ user.Id }";
                }
                else if (peer is TLPeerChat peerChat)
                {
                    TLChat chat = (TLChat)CacheService.GetChat(peerChat.ChatId);
                    cardTitle = chat.Title;
                    uri = $"{ uri }chat_id={ chat.Id }";
                }
                else if (peer is TLPeerChannel peerChannel)
                {
                    TLChannel channel = (TLChannel)CacheService.GetChat(peerChannel.ChannelId);
                    cardTitle = channel.Title;
                    uri = $"{ uri }channel_id={ channel.Id }";
                }

                activity.ActivationUri = new Uri(uri);
                activity.VisualElements.DisplayText = cardTitle;

                await activity.SaveAsync();

                return BootStrapper.Current.NavigationService.Dispatcher.Dispatch(() => activity.CreateSession());
            }

            return null;
        }
    }
}
