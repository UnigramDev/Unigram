using System;

namespace Telegram.Api.Transport
{
    public class DataEventArgs : EventArgs
    {
        public byte[] Data { get; set; }

        public DataEventArgs(byte[] data)
        {
            Data = data;
        }
    }
}