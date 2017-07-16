using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Api.Helpers;

namespace Telegram.Api.Services
{
    public interface IStatsService
    {
        void IncrementReceivedItemsCount(NetworkType networkType, DataType dataType, int value);
        void IncrementSentItemsCount(NetworkType networkType, DataType dataType, int value);
        void IncrementReceivedBytesCount(NetworkType networkType, DataType dataType, long value);
        void IncrementSentBytesCount(NetworkType networkType, DataType dataType, long value);
        void IncrementTotalCallsTime(NetworkType networkType, int value);
        int GetReceivedItemsCount(NetworkType networkType, DataType dataType);
        int GetSentItemsCount(NetworkType networkType, DataType dataType);
        long GetSentBytesCount(NetworkType networkType, DataType dataType);
        long GetReceivedBytesCount(NetworkType networkType, DataType dataType);
        int GetCallsTotalTime(NetworkType networkType);
        long GetResetStatsDate(NetworkType networkType);
        void ResetStats(NetworkType networkType);
    }

    public enum NetworkType
    {
        Mobile = 0,
        WiFi = 1,
        Roaming = 2
    }

    public enum DataType
    {
        Calls = 0,
        Messages = 1,
        Videos = 2,
        Audios = 3,
        Photos = 4,
        Files = 5,
        Total = 6
    }
}
