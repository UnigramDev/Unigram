// Copyright (C) 2016 by David Jeske, Barend Erasmus and donated to the public domain

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Telegram.Common
{
    public delegate HttpResponse ProcessRequestCallback(HttpRequest request);

    public class HttpRequest
    {
        public string Method { get; set; }
        public string Path { get; set; }
        public Dictionary<string, string> Headers { get; set; }

        public HttpRequest()
        {
            this.Headers = new Dictionary<string, string>();
        }

        public override string ToString()
        {
            return string.Format("{0} {1} HTTP/1.0\n{2}", this.Method, this.Path, string.Join("\r\n", this.Headers.Select(x => string.Format("{0}: {1}", x.Key, x.Value))));
        }
    }

    public class HttpResponse
    {
        public string StatusCode { get; set; } = "200";
        public string ReasonPhrase { get; set; }
        public byte[] Content { get; set; }

        public Dictionary<string, string> Headers { get; set; }

        public HttpResponse()
        {
            Headers = new Dictionary<string, string>();
        }

        // informational only tostring...
        public override string ToString()
        {
            return string.Format("HTTP status {0} {1}", this.StatusCode, this.ReasonPhrase);
        }
    }

    public class HttpServer
    {
        private readonly ProcessRequestCallback _callback;
        private readonly int _port;

        private TcpListener _listener;
        private bool _active = true;

        public HttpServer(int port, ProcessRequestCallback callback)
        {
            _callback = callback;
            _port = port;
        }

        public void Start()
        {
            Thread thread = new Thread(new ThreadStart(Listen));
            thread.Start();
        }

        private void Listen()
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();

            while (_active)
            {
                var connection = TryAcceptTcpClient();
                if (connection != null)
                {
                    ThreadPool.QueueUserWorkItem(state => HandleClient(connection));
                }

                Thread.Sleep(1);
            }
        }

        private TcpClient TryAcceptTcpClient()
        {
            try
            {
                return _listener.AcceptTcpClient();
            }
            catch
            {
                return null;
            }
        }

        public void Stop()
        {
            _active = false;
            _listener.Stop();
        }

        #region Processor

        public void HandleClient(TcpClient tcpClient)
        {
            try
            {
                Stream stream = tcpClient.GetStream();
                HttpRequest request = GetRequest(stream);

                // route and handle the request...
                HttpResponse response = _callback(request);

                Console.WriteLine("{0} {1}", response.StatusCode, request.Path);
                // build a default response for errors

                WriteResponse(stream, response);

                stream.Flush();
                stream.Close();
                stream = null;
            }
            catch (IOException)
            {
                // Connection was aborted
            }
        }

        // this formats the HTTP response...
        private static void WriteResponse(Stream stream, HttpResponse response)
        {
            if (response.Content == null)
            {
                response.Content = new byte[0];
            }

            response.Headers["Content-Length"] = response.Content.Length.ToString();

            Write(stream, string.Format("HTTP/1.0 {0} {1}\n", response.StatusCode, response.ReasonPhrase));

            foreach (var header in response.Headers)
            {
                Write(stream, string.Format("{0}: {1}\n", header.Key, header.Value));
            }

            Write(stream, "\n");

            stream.Write(response.Content, 0, response.Content.Length);
        }

        private static string ReadLine(Stream stream)
        {
            int next_char;
            string data = "";
            while (true)
            {
                next_char = stream.ReadByte();
                if (next_char == '\n') { break; }
                if (next_char == '\r') { continue; }
                if (next_char == -1) { Thread.Sleep(1); continue; };
                data += Convert.ToChar(next_char);
            }
            return data;
        }

        private static void Write(Stream stream, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
        }

        private HttpRequest GetRequest(Stream stream)
        {
            //Read Request Line
            string request = ReadLine(stream);

            string[] tokens = request.Split(' ');
            if (tokens.Length != 3)
            {
                throw new Exception("invalid http request line");
            }
            string method = tokens[0].ToUpper();
            string resource = tokens[1];
            string protocolVersion = tokens[2];

            //Read Headers
            Dictionary<string, string> headers = new Dictionary<string, string>();
            string line;
            while ((line = ReadLine(stream)) != null)
            {
                if (line.Equals(""))
                {
                    break;
                }

                int separator = line.IndexOf(':');
                if (separator == -1)
                {
                    throw new Exception("invalid http header line: " + line);
                }
                string name = line.Substring(0, separator);
                int pos = separator + 1;
                while ((pos < line.Length) && (line[pos] == ' '))
                {
                    pos++;
                }

                headers[name] = line.Substring(pos, line.Length - pos);
            }

            return new HttpRequest()
            {
                Method = method,
                Path = resource.Trim('/'),
                Headers = headers,
            };
        }

        #endregion
    }
}
