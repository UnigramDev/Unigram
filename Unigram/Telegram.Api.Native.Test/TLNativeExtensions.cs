using System;
using System.Collections.Generic;
using Telegram.Api.Native.TL;

namespace Telegram.Api.Native.Test
{

    public static class TLNativeExtensions
    {
        #region methods
        public static IReadOnlyList<T> ReadVector<T>(this TLBinaryReader reader)
        {
            var constructor = reader.ReadUInt32();
            if (constructor != 0x1cb5c415)
                throw new InvalidOperationException();

            var vector = new T[reader.ReadInt32()];
            for (int i = 0; i < vector.Length; i++)
            {

            }

            return vector;
        }
        #endregion
    }

}
