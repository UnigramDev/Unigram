using System;

namespace Telegram.Api.Transport
{
    public class DataEventArgs : EventArgs
    {
        public byte[] Data { get; set; }
        public DateTime? LastReceiveTime { get; set; }
        public int NextPacketLength { get; set; }

        public DataEventArgs(byte[] data)
        {
            Data = data;
        }

        public DataEventArgs(byte[] data, int packetLength, DateTime? lastReceiveTime)
        {
            Data = data;
            NextPacketLength = packetLength;
            LastReceiveTime = lastReceiveTime;
        }
    }
}