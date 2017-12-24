using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Template10.Common;
using Unigram.Converters;
using Unigram.Core.Models;
using Unigram.Views;
using Windows.ApplicationModel.UserActivities;
using Windows.Foundation.Metadata;

namespace Unigram.Helpers
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

        private static UserActivitySession _session;

        public static async Task<UserActivitySession> GenerateActivityAsync(TLPeerBase peer)
        {
            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 5))
            {
                var activity = await Channel.GetOrCreateUserActivityAsync("DialogActivity");

                var uri = "tg://toast?";
                var title = string.Empty;
                var id = 0;

                if (peer is TLPeerUser peerUser)
                {
                    var user = CacheService.GetUser(peerUser.UserId);
                    if (user == null)
                    {
                        return null;
                    }

                    uri += $"from_id={user.Id}";
                    title = user.FullName;
                    id = user.Id;
                }
                else if (peer is TLPeerChat peerChat)
                {
                    var chat = (TLChat)CacheService.GetChat(peerChat.ChatId);
                    if (chat == null)
                    {
                        return null;
                    }

                    uri += $"chat_id={chat.Id}";
                    title = chat.Title;
                    id = chat.Id;
                }
                else if (peer is TLPeerChannel peerChannel)
                {
                    var channel = (TLChannel)CacheService.GetChat(peerChannel.ChannelId);
                    if (channel == null)
                    {
                        return null;
                    }

                    uri += $"channel_id={channel.Id}";
                    title = channel.Title;
                    id = channel.Id;
                }

                activity.ActivationUri = new Uri(uri);
                activity.VisualElements.DisplayText = title;
                activity.VisualElements.BackgroundColor = BindConvert.Current.Bubble(id).Color;

                await activity.SaveAsync();
                var session = activity.CreateSession();

                _session?.Dispose();
                _session = session;

                return session;
            }

            return null;
        }
    }
}
