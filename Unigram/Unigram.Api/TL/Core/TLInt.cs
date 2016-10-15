using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public static class TLInt
    {
        private static readonly object _randomSyncRoot = new object();
        private static readonly Random _random = new Random();

        public static int Random()
        {
            var randomNumber = new byte[4];

            lock (_randomSyncRoot)
            {
                var random = _random;
                random.NextBytes(randomNumber);
            }

            return BitConverter.ToInt32(randomNumber, 0);
        }
    }
}
