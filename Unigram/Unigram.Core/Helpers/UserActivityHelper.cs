using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Template10.Common;
using Unigram.Core.Models;
using Windows.ApplicationModel.UserActivities;
using Windows.Foundation.Metadata;

namespace Unigram.Core.Helpers
{
    public interface IUserActivityHelper
    {
        Task<UserActivitySession> GenerateActivityAsync(UserActivityInfo info);
    }

    public class UserActivityHelper : IUserActivityHelper
    {
        private UserActivityChannel _channel;
        protected UserActivityChannel Channel
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

        public async Task<UserActivitySession> GenerateActivityAsync(UserActivityInfo info)
        {
            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 5))
            {
                UserActivity activity = 
                    await Channel.GetOrCreateUserActivityAsync(info.ActivityId);

                activity.VisualElements.DisplayText = info.Title;
                activity.VisualElements.Description = info.Details;
                activity.VisualElements.BackgroundColor = info.ActivityCardBackground;
                activity.ActivationUri = info.ActivationUri;

                await activity.SaveAsync();

                return BootStrapper.Current.NavigationService.Dispatcher.Dispatch(() => activity.CreateSession());
            }

            return null;
        }
    }
}
