using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

namespace Unigram.Core.Media
{
    public class YouTubeVideoPlayerTask : VideoPlayerTask
    {
        private static readonly Regex stsPattern = new Regex("\"sts\"\\s*:\\s*(\\d+)", RegexOptions.Compiled);
        private static readonly Regex jsPattern = new Regex("\"assets\":.+?\"js\":\\s*(\"[^\"]+\")", RegexOptions.Compiled);
        private static readonly Regex sigPattern = new Regex("\\.sig\\|\\|([a-zA-Z0-9$]+)\\(", RegexOptions.Compiled);
        private static readonly Regex sigPattern2 = new Regex("[\"']signature[\"']\\s*,\\s*([a-zA-Z0-9$]+)\\(", RegexOptions.Compiled);
        private static readonly Regex playerIdPattern = new Regex(".*?-([a-zA-Z0-9_-]+)(?:\\/watch_as3|\\/html5player(?:-new)?|\\/base)?\\.([a-z]+)$", RegexOptions.Compiled);
        private static readonly Regex playerIdPattern2 = new Regex(".*?-([a-zA-Z0-9_-]+)(?:\\/watch_as3|\\/html5player(?:-new)?|\\/[a-z]{2}_[A-Z]{2}\\/base)?\\.([a-z]+)$", RegexOptions.Compiled);

        private String sig;

        public async Task<string> DoWork(string videoId, CancellationToken token)
        {
            var result = new string[2];
            var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate });
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (X11; Linux x86_64; rv:10.0) Gecko/20150101 Firefox/47.0 (Chrome)");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-us,en;q=0.5");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Charset", "ISO-8859-1,utf-8;q=0.7,*;q=0.7");

            Match matcher;

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"https://www.youtube.com/embed/{videoId}");
                var response = await client.SendAsync(request, token);
                var embedCode = await response.Content.ReadAsStringAsync();

                if (token.IsCancellationRequested)
                {
                    return null;
                }

                var parameters = $"video_id={videoId}&ps=default&gl=US&hl=en";
                try
                {
                    parameters += "&eurl=" + WebUtility.UrlEncode("https://youtube.googleapis.com/v/" + videoId);
                }
                catch { }

                if (embedCode != null)
                {
                    matcher = stsPattern.Match(embedCode);
                    if (matcher.Success)
                    {
                        parameters += "&sts=" + embedCode.Substring(matcher.Index + 6, matcher.Length - 6);
                    }
                    else
                    {
                        parameters += "&sts=";
                    }
                }

                var encrypted = false;
                var extra = new string[] { "", "&el=info", "&el=embedded", "&el=detailpage", "&el=vevo" };
                for (int i = 0; i < extra.Length; i++)
                {
                    request = new HttpRequestMessage(HttpMethod.Get, "https://www.youtube.com/get_video_info?" + parameters + extra[i]);
                    response = await client.SendAsync(request, token);
                    var videoInfo = await response.Content.ReadAsStringAsync();

                    if (token.IsCancellationRequested)
                    {
                        return null;
                    }

                    var fmts = string.Empty;

                    var exists = false;
                    if (videoInfo != null)
                    {
                        var args = videoInfo.Split('&');
                        for (int a = 0; a < args.Length; a++)
                        {
                            if (args[a].StartsWith("url_encoded_fmt_stream_map") || args[a].StartsWith("adaptive_fmts"))
                            {
                                var args2 = args[a].Split('=');
                                fmts += WebUtility.UrlDecode(args2[1]) + "&";
                            }


                            if (args[a].StartsWith("dashmpd"))
                            {
                                exists = true;
                                var args2 = args[a].Split('=');
                                if (args2.Length == 2)
                                {
                                    try
                                    {
                                        result[0] = WebUtility.UrlDecode(args2[1]);
                                    }
                                    catch { }
                                }
                            }
                            else if (args[a].StartsWith("use_cipher_signature"))
                            {
                                var args2 = args[a].Split('=');
                                if (args2.Length == 2)
                                {
                                    if (args2[1].ToLower().Equals("true"))
                                    {
                                        encrypted = true;
                                    }
                                }
                            }
                        }
                    }
                    if (exists)
                    {
                        break;
                    }

                    var yolo = fmts.Trim('&').Split('&').Select(x => new Tuple<string, string>(x.Split('=')[0], WebUtility.UrlDecode(x.Split('=')[1]))).ToList();
                }

                if (result[0] != null && (encrypted || result[0].Contains("/s/")) && embedCode != null)
                {
                    encrypted = true;
                    int index = result[0].IndexOf("/s/");
                    int index2 = result[0].IndexOf('/', index + 10);
                    if (index != -1)
                    {
                        if (index2 == -1)
                        {
                            index2 = result[0].Length;
                        }
                        sig = result[0].Substring(index, index2 - index);
                        String jsUrl = null;
                        matcher = jsPattern.Match(embedCode);
                        if (matcher.Success)
                        {
                            try
                            {
                                var tokener = JsonValue.Parse(matcher.Groups[1].Value);
                                if (tokener.ValueType == JsonValueType.String)
                                {
                                    jsUrl = tokener.GetString();
                                }
                            }
                            catch { }
                        }

                        if (jsUrl != null)
                        {
                            matcher = playerIdPattern.Match(jsUrl);
                            String playerId;
                            if (matcher.Success)
                            {
                                playerId = matcher.Groups[1].Value + matcher.Groups[2].Value;
                            }
                            else
                            {
                                matcher = playerIdPattern2.Match(jsUrl);
                                if (matcher.Success)
                                {
                                    playerId = matcher.Groups[1].Value + matcher.Groups[2].Value;
                                }
                                else
                                {
                                    playerId = null;
                                }
                            }
                            String functionCode = null;
                            String functionName = null;
                            var preferences = ApplicationData.Current.LocalSettings.CreateContainer("YouTubeCode", ApplicationDataCreateDisposition.Always);
                            if (playerId != null)
                            {
                                functionCode = preferences.Values[playerId] as string;
                                functionName = preferences.Values[playerId + "n"] as string;
                            }
                            if (functionCode == null)
                            {
                                if (jsUrl.StartsWith("//"))
                                {
                                    jsUrl = "https:" + jsUrl;
                                }
                                else if (jsUrl.StartsWith("/"))
                                {
                                    jsUrl = "https://www.youtube.com" + jsUrl;
                                }

                                request = new HttpRequestMessage(HttpMethod.Get, jsUrl);
                                response = await client.SendAsync(request, token);
                                var jsCode = await response.Content.ReadAsStringAsync();

                                if (token.IsCancellationRequested)
                                {
                                    return null;
                                }

                                if (jsCode != null)
                                {
                                    matcher = sigPattern.Match(jsCode);
                                    if (matcher.Success)
                                    {
                                        functionName = matcher.Groups[1].Value;
                                    }
                                    else
                                    {
                                        matcher = sigPattern2.Match(jsCode);
                                        if (matcher.Success)
                                        {
                                            functionName = matcher.Groups[1].Value;
                                        }
                                    }
                                    if (functionName != null)
                                    {
                                        try
                                        {
                                            JSExtractor extractor = new JSExtractor(jsCode);
                                            functionCode = extractor.ExtractFunction(functionName);
                                            if (!string.IsNullOrEmpty(functionCode) && playerId != null)
                                            {
                                                preferences.Values[playerId] = functionCode;
                                                preferences.Values[playerId + "n"] = functionName;
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            //FileLog.e(e);
                                        }
                                    }
                                }
                            }
                            if (!string.IsNullOrEmpty(functionCode))
                            {
                                functionCode += functionName + "('" + sig.Substring(3) + "');";

                                var webView = new WebView(WebViewExecutionMode.SeparateThread);
                                var value = await webView.InvokeScriptAsync("eval", new[] { functionCode });

                                result[0] = result[0].Replace(sig, "/signature/" + value); // value.Substring(1, value.Length - 1));
                                encrypted = false;
                                Debugger.Break();
                            }
                        }
                    }
                }

                return token.IsCancellationRequested || encrypted ? null : result[0];
            }
            catch { }
            finally
            {
                client.Dispose();
            }

            return token.IsCancellationRequested ? null : result[0];
        }
    }
}
