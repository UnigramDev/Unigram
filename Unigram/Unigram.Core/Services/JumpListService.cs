using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.Foundation.Metadata;
using Windows.UI.StartScreen;

namespace Unigram.Core.Services
{
    public interface IJumpListService
    {
        Task UpdateAsync(TLUser user);
    }

    public class JumpListService : IJumpListService
    {
        public Task UpdateAsync(TLUser user)
        {
            var photo = new Uri("ms-appx:///Assets/Logos/Square44x44Logo/Square44x44Logo.scale-100.png");
            if (user.HasPhoto)
            {
                var userProfilePhoto = user.Photo as TLUserProfilePhoto;
                if (userProfilePhoto != null)
                {
                    var fileLocation = userProfilePhoto.PhotoSmall as TLFileLocation;
                    if (fileLocation != null)
                    {
                        photo = new Uri(string.Format("ms-appdata:///local/{0}_{1}_{2}.jpg", fileLocation.VolumeId, fileLocation.LocalId, fileLocation.Secret));
                    }
                }
            }

            return UpdateAsync(photo, user.FullName, user.Id.ToString());
        }

        private async Task UpdateAsync(Uri photo, string displayName, string id)
        {
            if (ApiInformation.IsTypePresent("Windows.UI.StartScreen.JumpList") && JumpList.IsSupported())
            {
                var current = await JumpList.LoadCurrentAsync();
                var already = current.Items.FirstOrDefault(x => x.Arguments.Contains(id));
                if (already == null)
                {
                    var item = JumpListItem.CreateWithArguments("tid=" + id, displayName);
                    item.Description = "Chat with " + displayName;
                    item.Logo = photo;
                    item.GroupName = "Recents";

                    if (current.Items.Count == 10)
                    {
                        current.Items.RemoveAt(9);
                    }

                    current.Items.Add(item);

                    await current.SaveAsync();
                }
                else
                {
                    var index = current.Items.IndexOf(already);
                    if (index > 0)
                    {
                        current.Items.RemoveAt(index);
                        current.Items.Insert(index - 1, already);

                        await current.SaveAsync();
                    }
                }
            }
        }
    }
}
