using System;
using System.IO;
using System.Linq;
using Windows.Security.Cryptography.Core;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Telegram.Api.TL;
using System.Globalization;
using Telegram.Api.Services;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using System.Diagnostics;
using System.Collections.Generic;

namespace Telegram.Api.Helpers
{
    public static class Utils
    {
        public static string GetShortTimePattern(ref CultureInfo ci)
        {
            if (ci.DateTimeFormat.ShortTimePattern.Contains("H"))
            {
                return "H:mm";
            }
            ci.DateTimeFormat.AMDesignator = "am";
            ci.DateTimeFormat.PMDesignator = "pm";
            return "h:mmt";
        }



        public static double DateTimeToUnixTimestamp(DateTime dateTime)
        {
            // From local DateTime to UTC0 UnixTime

            var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            DateTime.SpecifyKind(dtDateTime, DateTimeKind.Utc);

            return (dateTime.ToUniversalTime() - dtDateTime).TotalSeconds;
        }

        public static long CurrentTimestamp
        {
            get
            {
                var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                DateTime.SpecifyKind(dtDateTime, DateTimeKind.Utc);

                return (long)(DateTime.Now.ToUniversalTime() - dtDateTime).TotalMilliseconds;
            }
        }

        public static DateTime UnixTimestampToDateTime(double unixTimeStamp)
        {
            // From UTC0 UnixTime to local DateTime

            var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            DateTime.SpecifyKind(dtDateTime, DateTimeKind.Utc);

            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dtDateTime;
        }


        public static byte[] ComputeSHA1(byte[] data)
        {
            var algorithm = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Sha1);
            var buffer = CryptographicBuffer.CreateFromByteArray(data);
            var hash = algorithm.HashData(buffer);

            CryptographicBuffer.CopyToByteArray(hash, out byte[] digest);
            return digest;
        }
    }
}
