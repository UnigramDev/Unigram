using System;
using System.Net;
using Telegram.Api.TL;

namespace Telegram.Api.Extensions
{
    public static class HttpWebRequestExtensions
    {
        public static void BeginAsync(this HttpWebRequest request, byte[] data, Action<byte[]> callback, Action faultCallback)
        {
            request.BeginGetRequestStream(ar => GetRequestStreamCallback(data, ar, ar2 => EndAsync(ar2, callback, faultCallback)), request);
        }

        public static void BeginAsync(this HttpWebRequest request, byte[] data, Action<IAsyncResult> onCompleted)
        {
            request.BeginGetRequestStream(ar => GetRequestStreamCallback(data, ar, onCompleted), request);
        }

        private static void GetRequestStreamCallback(byte[] data, IAsyncResult asynchronousResult, Action<IAsyncResult> onCompleted)
        {
            var request = (HttpWebRequest)asynchronousResult.AsyncState;

            // End the operation
            var postStream = request.EndGetRequestStream(asynchronousResult);

            // Convert the string into a byte array.
            var byteArray = data;

            // Write to the request stream.
            postStream.Write(byteArray, 0, data.Length);
            postStream.Dispose();

            // Start the asynchronous operation to get the response
            request.BeginGetResponse(x => onCompleted(x), request);
        }

        private static void EndAsync(IAsyncResult asynchronousResult, Action<byte[]> callback, Action faultCallback)
        {
            //try
            {
                try
                {
                    var request = (HttpWebRequest)asynchronousResult.AsyncState;
                    HttpWebResponse response;
                
                    using (response = (HttpWebResponse)request.EndGetResponse(asynchronousResult))
                    {
                        using (var dataStream = response.GetResponseStream())
                        {

                            var buffer = new byte[Int32.Parse(response.Headers["Content-Length"])];
                            var bytesRead = 0;
                            var totalBytesRead = bytesRead;
                            while (totalBytesRead < buffer.Length)
                            {
                                bytesRead = dataStream.Read(buffer, bytesRead, buffer.Length - bytesRead);
                                totalBytesRead += bytesRead;
                            }

                            callback(buffer);
                        }
                    }
                }
                catch (Exception ex)
                {
                    TLUtils.WriteException(ex);
                    faultCallback();            
                    
                    //response = (HttpWebResponse)ex.Response;
                    //if (response == null)
                    //{
                    //    if (faultCallback != null) faultCallback();
                    //    return;
                    //}

                    //if (response.StatusCode == HttpStatusCode.BadGateway
                    //    || response.StatusCode == HttpStatusCode.NotFound)
                    //{
                    //    if (faultCallback != null) faultCallback();
                    //    return;
                    //}
                }
            }
        }
    }
}
