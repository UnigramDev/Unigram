using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace Unigram.Core.Media
{
    public class VimeoVideoPlayerTask : VideoPlayerTask
    {
        private readonly CancellationToken _token;
        private readonly string _videoId;

        public async Task<string> DoWork(string videoId, CancellationToken token)
        {
            var results = new string[2];
            var client = new HttpClient();

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"https://player.vimeo.com/video/{videoId}/config");
                var response = await client.SendAsync(request, token);
                var content = await response.Content.ReadAsStringAsync();

                if (token.IsCancellationRequested)
                {
                    return null;
                }

                var json = JsonObject.Parse(content);
                var files = json.GetNamedObject("request").GetNamedObject("files");
                if (files.ContainsKey("hls"))
                {
                    var hls = files.GetNamedObject("hls");
                    if (hls.ContainsKey("url"))
                    {
                        results[0] = hls.GetNamedString("url");
                    }
                    else
                    {
                        var defaultCdn = hls.GetNamedString("default_cdn");
                        var cdns = hls.GetNamedObject("cdns");
                        hls = cdns.GetNamedObject(defaultCdn);
                        results[0] = hls.GetNamedString("url");
                    }
                    results[1] = "hls";
                }
                else if (files.ContainsKey("progressive"))
                {
                    results[1] = "other";
                    var progressive = files.GetNamedArray("progressive");
                    for (int i = 0; i < progressive.Count; i++)
                    {
                        var format = progressive[i];
                        results[0] = format.GetObject().GetNamedString("url");
                        break;
                    }
                }
            }
            catch { }
            finally
            {
                client.Dispose();
            }

            return token.IsCancellationRequested ? null : results[0];
        }
    }
}
