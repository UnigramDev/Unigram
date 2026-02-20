//
// Copyright (c) Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Native;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Gallery;
using Telegram.Views;

namespace Telegram.Common
{
    public class MediaHttpServer
    {
        private readonly HttpServer _server;
        private readonly ConcurrentDictionary<long, int> _availableFiles = new();

        private MediaHttpServer()
        {
            _server = new HttpServer(0, Serve);
            _server.Start();
        }

        private static MediaHttpServer _current;

        public static void Start()
        {
            _current ??= new MediaHttpServer();
        }

        public static int Port => _current?._server.Port ?? 0;

        public static Uri Start(PlaybackItem item, ref long token)
        {
            Start(item.ClientService.SessionId, item.Document.Id, ref token);

            return new Uri(string.Format(CultureInfo.InvariantCulture, "http://127.0.0.1:{0}/{1}/{2}?duration={3}", Port, item.ClientService.SessionId, item.Document.Id, item.Duration));
        }

        public static Uri Start(GalleryMedia video, ref long token)
        {
            Start(video.ClientService.SessionId, video.File.Id, ref token);

            return new Uri(string.Format(CultureInfo.InvariantCulture, "http://127.0.0.1:{0}/{1}/{2}?duration={3}", Port, video.ClientService.SessionId, video.File.Id, video.Duration));
        }

        public static Uri Start(VideoPresentation presentation, ref long token)
        {
            Start(presentation.SessionId, presentation.FileId, ref token);

            return new Uri(string.Format(CultureInfo.InvariantCulture, "http://127.0.0.1:{0}/{1}/{2}?duration={3}&priority=24", Port, presentation.SessionId, presentation.FileId, presentation.Duration));
        }

        public static Uri Start(IClientService clientService, StoryVideo video, ref long token)
        {
            Start(clientService.SessionId, video.Video.Id, ref token);

            return new Uri(string.Format(CultureInfo.InvariantCulture, "http://127.0.0.1:{0}/{1}/{2}?duration={3}&priority=24", Port, clientService.SessionId, video.Video.Id, video.Duration));
        }

        public static void Start(int sessionId, int fileId, ref long token)
        {
            if (_current == null)
            {
                return;
            }

            if (token != 0)
            {
                Stop(ref token);
            }

            token = (sessionId << 16) | fileId;

            if (_current._availableFiles.TryGetValue(token, out int count))
            {
                _current._availableFiles[token] = count++;
            }
            else
            {
                _current._availableFiles[token] = 1;
            }
        }

        public static void Stop(ref long token)
        {
            if (_current == null)
            {
                return;
            }

            if (_current._availableFiles.TryGetValue(token, out int count))
            {
                if (count == 1)
                {
                    _current._availableFiles.TryRemove(token, out count);
                }
                else
                {
                    _current._availableFiles[token] = count--;
                }
            }

            token = 0;
        }

        private void Stop()
        {
            _server.Stop();
        }

        private HttpResponse Serve(HttpRequest request)
        {
            return Serve(request, true);
        }

        private HttpResponse Serve(HttpRequest request, bool retry)
        {
            var session = System.IO.Path.GetDirectoryName(request.Path);
            var fileName = System.IO.Path.GetFileNameWithoutExtension(request.Path);

            if (!int.TryParse(session, out int sessionId) || !int.TryParse(fileName, out int fileId))
            {
                return HttpResponse.NotFound;
            }

            if (!_availableFiles.ContainsKey((sessionId << 16) | fileId))
            {
                return HttpResponse.NotFound;
            }

            var clientService = TypeResolver.Current.Resolve<IClientService>(sessionId);
            if (clientService == null)
            {
                return HttpResponse.NotFound;
            }

            var file = clientService.GetFileAsync(fileId).Result;
            if (file == null)
            {
                return HttpResponse.NotFound;
            }

            var priority = 32;
            if (request.Query.TryGetValue("priority", out string priorityValue))
            {
                int.TryParse(priorityValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out priority);
            }

            long offset = 0;
            long limit = 0;
            long buffer = 0;
            double duration = 0;

            if (request.Headers.TryGetValue("Range", out var range) && RangeHeaderValue.TryParse(range, out var ranges))
            {
                if (request.Query.TryGetValue("duration", out string durationValue) && double.TryParse(durationValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out duration) && duration > 0)
                {
                    buffer = Math.Min((long)(((double)file.Size / duration) * 15), 4 * 1024 * 1024);
                }
                else
                {
                    buffer = 1 * 1024 * 1024;
                }

                foreach (var part in ranges.Ranges)
                {
                    offset = part.From ?? 0;

                    if (part.To.HasValue)
                    {
                        limit = part.To.Value - offset + 1;
                        buffer = part.To.Value - offset + 1;
                    }
                    else if ((double)offset / file.Size >= 0.95)
                    {
                        // Likely metadata, let's read the remaning all together
                        limit = 0;
                        buffer = 0;
                    }
                    else
                    {
                        limit = Math.Min(file.Size - offset, 64 * 1024);
                        buffer = Math.Min(file.Size - offset, buffer);
                    }

                    break;
                }
            }

            Logger.Info(request.Path + ", offset: " + offset + ", count: " + limit);

            if (limit == 0)
            {
                limit = file.Size - offset;
                buffer = file.Size - offset;
            }

            var remote = new RemoteFileSource(clientService, file, duration);
            remote.SeekCallback(offset);
            remote.ReadCallback(limit, buffer, out long bytesRead);
            remote.Close();

            if (bytesRead >= 0)
            {
                try
                {
                    var response = new HttpResponse();
                    response.StatusCode = "206";
                    response.Headers["Access-Control-Allow-Origin"] = "*";
                    response.Headers["Content-Type"] = "video/mp4";
                    response.Headers["Content-Range"] = string.Format("bytes {0}-{1}/{2}", offset, offset + limit - 1, file.Size);

                    using (var stream = new FileStreamFromApp(file.Local.Path))
                    {
                        stream.Seek(offset);

                        var data = BufferSurface.Create((uint)limit);
                        stream.Read(data, (uint)data.Length);
                        response.Content = data.ToArray();
                    }

                    return response;
                }
                catch (System.IO.FileNotFoundException)
                {
                    // It can happen that file got copied from temp to videos, in this case we just retry
                    if (retry)
                    {
                        return Serve(request, false);
                    }
                }
                catch (Exception ex)
                {
                    // Generic error (probably OOM)
                    Logger.Error(ex);

                    // We return a valid but empty response, VLC should try again
                    var response = new HttpResponse();
                    response.StatusCode = "206";
                    response.Headers["Access-Control-Allow-Origin"] = "*";
                    response.Headers["Content-Type"] = "video/mp4";
                    response.Headers["Content-Range"] = string.Format("bytes {0}-{1}/{2}", offset, offset, file.Size);

                    return response;
                }
            }

            return HttpResponse.NotFound;
        }
    }
}
