using System;
using System.IO;
using System.Runtime.CompilerServices;
using Telegram.Api.Helpers;
using Telegram.Api.Services;

namespace Telegram.Api.TL
{
    public static partial class TLUtils
    {
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public static bool ByteArraysEqual(byte[] a, byte[] b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }
            if (((a == null) || (b == null)) || (a.Length != b.Length))
            {
                return false;
            }
            var flag = true;
            for (var i = 0; i < a.Length; i++)
            {
                flag &= a[i] == b[i];
            }
            return flag;
        }

        public static T OpenObjectFromMTProtoFile<T>(object syncRoot, string fileName) where T : TLObject
        {
            try
            {
                if (!File.Exists(FileUtils.GetFileName(fileName)))
                {
                    return default(T);
                }

                lock (syncRoot)
                {
                    //using (var fileStream = FileUtils.GetLocalFileStreamForRead(fileName))
                    //{
                    //    if (fileStream.Length > 0)
                    //    {
                            //using (var from = TLObjectSerializer.CreateReader(FileUtils.GetFileName(fileName)))
                            //{
                            //    from.ReadUInt32();
                            //    return (T)Activator.CreateInstance(typeof(T), from);
                            //    //return TLFactory.Read<T>(from);
                            //}
                    //    }
                    //}
                }
            }
            catch (Exception e)
            {

            }

            return default(T);
        }

        public static void SaveObjectToMTProtoFile<T>(object syncRoot, string fileName, T data) where T: TLObject
        {
            try
            {
                lock (syncRoot)
                {
                    FileUtils.SaveWithTempFile(fileName, data);
                }
            }
            catch (Exception e)
            {

            }
        }


        public static int Now
        {
            get
            {
                return TLUtils.DateToUniversalTimeTLInt(DateTime.Now);
            }
        }

        public static int DateToUniversalTimeTLInt(DateTime date)
        {
            return (int)Utils.DateTimeToUnixTimestamp(date);
        }

        public static int ToTLInt(DateTime date)
        {
            return (int)Utils.DateTimeToUnixTimestamp(date); //int * 2^32 + clientDelta
        }

        public static byte[] Combine(params byte[][] arrays)
        {
            var length = 0;
            for (int i = 0; i < arrays.Length; i++)
            {
                length += arrays[i].Length;
            }

            var result = new byte[length]; ////[arrays.Sum(a => a.Length)];
            var offset = 0;
            foreach (var array in arrays)
            {
                Buffer.BlockCopy(array, 0, result, offset, array.Length);
                offset += array.Length;
            }
            return result;
        }

        public static DateTime ToDateTime(int? date)
        {
            var ticks = date.Value;
            return Utils.UnixTimestampToDateTime(ticks >> 32);
        }
    }
}
