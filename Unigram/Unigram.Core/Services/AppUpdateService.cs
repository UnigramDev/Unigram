using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Unigram.Core.Common;
using Windows.ApplicationModel;
using Windows.System;
using Windows.UI.Popups;
using Windows.UI.Xaml;

namespace Unigram.Core.Services
{
    public interface IAppUpdateService
    {
        Task CheckForUpdatesAsync();
    }

    public class AppUpdateService : IAppUpdateService
    {
        public async Task CheckForUpdatesAsync()
        {
            using (var client = new HttpClient())
            {
                try
                {
                    var response = await client.GetStringAsync("https://rink.hockeyapp.net/api/2/apps/7d36a4260af54125bbf6db407911ed3b.json");
                    var json = await Task.Run(() => JArray.Parse(response));
                    var latest = json[0] as JObject;
                    if (latest != null)
                    {
                        if (latest.TryGetValue("version", out JToken versionToken) && Version.TryParse(versionToken.Value<string>(), out Version version))
                        {
                            var current = new Version(Package.Current.Id.Version.Major, Package.Current.Id.Version.Minor, Package.Current.Id.Version.Build, Package.Current.Id.Version.Revision);
                            if (current < version)
                            {
                                var notes = latest["notes"].Value<string>();
                                notes = notes.Replace("\n\n<p>How to install Unigram: http://telegra.ph/How-to-install-Unigram-03-10-2</p>", string.Empty);
                                notes = notes.Replace("<ul>", string.Empty);
                                notes = notes.Replace("</ul>", string.Empty);
                                notes = notes.Replace("<p>", string.Empty);
                                notes = notes.Replace("</p>", string.Empty);
                                notes = notes.RegexReplace("<li>(.*?)</li>", "- $1");
                                notes = notes.Trim('\n');

                                var mandatory = latest["mandatory"].Value<bool>();
                                var timestamp = latest["timestamp"].Value<long>();

                                var date = Utils.UnixTimestampToDateTime(timestamp);
                                var now = DateTime.Now;
                                if (now - date >= TimeSpan.FromDays(5) || mandatory)
                                {
                                    notes += "\n\nThis update is mandatory.\nYou won't be able to use this app until you update it.";
                                }

                                var dialog = new MessageDialog(notes, "Version " + versionToken.Value<string>());
                                dialog.Commands.Add(new UICommand("Update", (_) => { }, 0));
                                dialog.Commands.Add(new UICommand("Cancel", (_) => { }, 1));

                                Execute.BeginOnUIThread(async () =>
                                {
                                    var confirm = await dialog.ShowAsync();
                                    if (confirm != null && (int)confirm.Id == 0)
                                    {
                                        await Launcher.LaunchUriAsync(new Uri("https://rink.hockeyapp.net/apps/7d36a4260af54125bbf6db407911ed3b"));
                                    }

                                    if (now - date >= TimeSpan.FromDays(5) || mandatory)
                                    {
                                        Application.Current.Exit();
                                    }
                                });
                            }
                        }
                    }
                }
                catch { }
            }
        }
    }
}
