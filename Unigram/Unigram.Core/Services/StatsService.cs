using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Unigram.Common;

namespace Unigram.Core.Services
{
    public class StatsService : IStatsService
    {
        private const int TYPE_MOBILE = 0;
        private const int TYPE_WIFI = 1;
        private const int TYPE_ROAMING = 2;

        private const int TYPE_CALLS = 0;
        private const int TYPE_MESSAGES = 1;
        private const int TYPE_VIDEOS = 2;
        private const int TYPE_AUDIOS = 3;
        private const int TYPE_PHOTOS = 4;
        private const int TYPE_FILES = 5;
        private const int TYPE_TOTAL = 6;
        private const int TYPES_COUNT = 7;

        private long[][] sentBytes = new long[3][] { new long[TYPES_COUNT], new long[TYPES_COUNT], new long[TYPES_COUNT] };
        private long[][] receivedBytes = new long[3][] { new long[TYPES_COUNT], new long[TYPES_COUNT], new long[TYPES_COUNT] };
        private int[][] sentItems = new int[3][] { new int[TYPES_COUNT], new int[TYPES_COUNT], new int[TYPES_COUNT] };
        private int[][] receivedItems = new int[3][] { new int[TYPES_COUNT], new int[TYPES_COUNT], new int[TYPES_COUNT] };
        private long[] resetStatsDate = new long[3];
        private int[] callsTotalTime = new int[3];

        private ThreadLocal<long> lastStatsSaveTime = new ThreadLocal<long>(() => Utils.CurrentTimestamp - 1000);

        public StatsService()
        {
            sentBytes[0] = new long[TYPES_COUNT];

            bool save = false;
            for (int a = 0; a < 3; a++)
            {
                callsTotalTime[a] = ApplicationSettings.Current.GetValueOrDefault("callsTotalTime" + a, 0);
                resetStatsDate[a] = ApplicationSettings.Current.GetValueOrDefault("resetStatsDate" + a, 0L);
                for (int b = 0; b < TYPES_COUNT; b++)
                {
                    sentBytes[a][b] = ApplicationSettings.Current.GetValueOrDefault("sentBytes" + a + "_" + b, 0L);
                    receivedBytes[a][b] = ApplicationSettings.Current.GetValueOrDefault("receivedBytes" + a + "_" + b, 0L);
                    sentItems[a][b] = ApplicationSettings.Current.GetValueOrDefault("sentItems" + a + "_" + b, 0);
                    receivedItems[a][b] = ApplicationSettings.Current.GetValueOrDefault("receivedItems" + a + "_" + b, 0);
                }
                if (resetStatsDate[a] == 0)
                {
                    save = true;
                    resetStatsDate[a] = Utils.CurrentTimestamp;
                }
            }
            if (save)
            {
                SaveStats();
            }
        }

        public void IncrementReceivedItemsCount(NetworkType networkType, DataType dataType, int value)
        {
            receivedItems[(int)networkType][(int)dataType] += value;
            SaveStats();
        }

        public void IncrementSentItemsCount(NetworkType networkType, DataType dataType, int value)
        {
            sentItems[(int)networkType][(int)dataType] += value;
            SaveStats();
        }

        public void IncrementReceivedBytesCount(NetworkType networkType, DataType dataType, long value)
        {
            receivedBytes[(int)networkType][(int)dataType] += value;
            SaveStats();
        }

        public void IncrementSentBytesCount(NetworkType networkType, DataType dataType, long value)
        {
            sentBytes[(int)networkType][(int)dataType] += value;
            SaveStats();
        }

        public void IncrementTotalCallsTime(NetworkType networkType, int value)
        {
            callsTotalTime[(int)networkType] += value;
            SaveStats();
        }

        public int GetReceivedItemsCount(NetworkType networkType, DataType dataType)
        {
            return receivedItems[(int)networkType][(int)dataType];
        }

        public int GetSentItemsCount(NetworkType networkType, DataType dataType)
        {
            return sentItems[(int)networkType][(int)dataType];
        }

        public long GetSentBytesCount(NetworkType networkType, DataType dataType)
        {
            if (dataType == DataType.Messages)
            {
                return sentBytes[(int)networkType][TYPE_TOTAL] - sentBytes[(int)networkType][TYPE_FILES] - sentBytes[(int)networkType][TYPE_AUDIOS] - sentBytes[(int)networkType][TYPE_VIDEOS] - sentBytes[(int)networkType][TYPE_PHOTOS];
            }
            return sentBytes[(int)networkType][(int)dataType];
        }

        public long GetReceivedBytesCount(NetworkType networkType, DataType dataType)
        {
            if (dataType == DataType.Messages)
            {
                return receivedBytes[(int)networkType][TYPE_TOTAL] - receivedBytes[(int)networkType][TYPE_FILES] - receivedBytes[(int)networkType][TYPE_AUDIOS] - receivedBytes[(int)networkType][TYPE_VIDEOS] - receivedBytes[(int)networkType][TYPE_PHOTOS];
            }
            return receivedBytes[(int)networkType][(int)dataType];
        }

        public int GetCallsTotalTime(NetworkType networkType)
        {
            return callsTotalTime[(int)networkType];
        }

        public long GetResetStatsDate(NetworkType networkType)
        {
            return resetStatsDate[(int)networkType];
        }

        public void ResetStats(NetworkType networkType)
        {
            resetStatsDate[(int)networkType] = Utils.CurrentTimestamp;
            for (int a = 0; a < TYPES_COUNT; a++)
            {
                sentBytes[(int)networkType][a] = 0;
                receivedBytes[(int)networkType][a] = 0;
                sentItems[(int)networkType][a] = 0;
                receivedItems[(int)networkType][a] = 0;
            }
            callsTotalTime[(int)networkType] = 0;
            SaveStats();
        }

        private void SaveStats()
        {
            long newTime = Utils.CurrentTimestamp;
            if (Math.Abs(newTime - lastStatsSaveTime.Value) >= 1000)
            {
                lastStatsSaveTime.Value = newTime;
                Execute.BeginOnThreadPool(() =>
                {
                    for (int networkType = 0; networkType < 3; networkType++)
                    {
                        for (int a = 0; a < TYPES_COUNT; a++)
                        {
                            ApplicationSettings.Current.AddOrUpdateValue("receivedItems" + networkType + "_" + a, receivedItems[networkType][a]);
                            ApplicationSettings.Current.AddOrUpdateValue("sentItems" + networkType + "_" + a, sentItems[networkType][a]);
                            ApplicationSettings.Current.AddOrUpdateValue("receivedBytes" + networkType + "_" + a, receivedBytes[networkType][a]);
                            ApplicationSettings.Current.AddOrUpdateValue("sentBytes" + networkType + "_" + a, sentBytes[networkType][a]);
                        }

                        ApplicationSettings.Current.AddOrUpdateValue("callsTotalTime" + networkType, callsTotalTime[networkType]);
                        ApplicationSettings.Current.AddOrUpdateValue("resetStatsDate" + networkType, resetStatsDate[networkType]);
                    }
                    //try
                    //{
                    //    editor.commit();
                    //}
                    //catch (Exception e)
                    //{
                    //    FileLog.e(e);
                    //}
                });
            }
        }
    }
}
