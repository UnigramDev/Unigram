using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Unigram.Core.Common;
using Windows.ApplicationModel;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;

namespace Unigram.Core.Services
{
    public interface IHockeyAppUpdateService
    {
        Task CheckForUpdatesAsync(string appId, CoreDispatcher dispatcher);
    }

    public class HockeyAppUpdateService : IHockeyAppUpdateService
    {
        public async Task CheckForUpdatesAsync(string appId, CoreDispatcher dispatcher)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    var current = new Version(Package.Current.Id.Version.Major, Package.Current.Id.Version.Minor, Package.Current.Id.Version.Build, Package.Current.Id.Version.Revision);

                    var response = await client.GetStringAsync($"https://rink.hockeyapp.net/api/2/apps/{appId}.json");
                    var json = await Task.Run(() => JArray.Parse(response));
                    var versions = json.Select(x => new AppVersion(x)).Where(x => x.Version != null && x.Data != null && current < x.Version).OrderByDescending(x => x.Version);

                    var builder = new StringBuilder();

                    foreach (var item in versions)
                    {
                        var notes = item.Data["notes"].Value<string>();
                        notes = notes.Replace("<ul>\n", string.Empty);
                        notes = notes.Replace("\n</ul>", string.Empty);
                        notes = notes.Replace("<p>", string.Empty);
                        notes = notes.Replace("</p>", string.Empty);
                        notes = notes.Replace("<strong>", string.Empty);
                        notes = notes.Replace("</strong>", string.Empty);
                        notes = notes.RegexReplace("<li>(.*?)</li>", "- $1");
                        notes = WebUtility.HtmlDecode(notes);

                        builder.AppendFormat("Version {0}.{1}.{2}\n", item.Version.Major, item.Version.Minor, item.Version.Build);
                        builder.AppendLine(notes);
                    }

                    if (builder.Length == 0)
                    {
                        return;
                    }

                    await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                    {
                        try
                        {
                            var dialog = new MessageDialog(builder.ToString(), "Update available");
                            dialog.Commands.Add(new UICommand("Update", (_) => { }, 0));
                            dialog.Commands.Add(new UICommand("Cancel", (_) => { }, 1));

                            var confirm = await dialog.ShowAsync();
                            if (confirm != null && (int)confirm.Id == 0)
                            {
                                await Launcher.LaunchUriAsync(new Uri($"https://rink.hockeyapp.net/apps/{appId}"));
                            }
                        }
                        catch { }
                    });
                }
                catch { }
            }
        }

        private class AppVersion
        {
            public AppVersion(JToken token)
            {
                var version = token as JObject;
                if (version == null)
                {
                    return;
                }

                if (version.TryGetValue("version", out JToken versionToken) && Version.TryParse(versionToken.Value<string>(), out Version value))
                {
                    Version = value;
                    Data = version;
                }
            }

            public Version Version { get; private set; }
            public JObject Data { get; private set; }
        }
    }
}
