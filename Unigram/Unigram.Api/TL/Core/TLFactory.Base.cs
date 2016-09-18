using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public static partial class TLFactory
    {
        public static T Read<T>(TLBinaryReader from, bool fromCache)
        {
            if (typeof(T) == typeof(UInt32)) return (T)(Object)from.ReadUInt32();
            else if (typeof(T) == typeof(Int32)) return (T)(Object)from.ReadInt32();
            else if (typeof(T) == typeof(UInt64)) return (T)(Object)from.ReadUInt64();
            else if (typeof(T) == typeof(Int64)) return (T)(Object)from.ReadInt64();
            else if (typeof(T) == typeof(Double)) return (T)(Object)from.ReadDouble();
            else if (typeof(T) == typeof(Boolean)) return (T)(Object)from.ReadBoolean();
            else if (typeof(T) == typeof(String)) return (T)(Object)from.ReadString();
            else if (typeof(T) == typeof(Byte[])) return (T)(Object)from.ReadByteArray();
            else if (typeof(T) == typeof(TLInt128)) return (T)(Object)new TLInt128(from);
            else if (typeof(T) == typeof(TLInt256)) return (T)(Object)new TLInt256(from);
            else if (typeof(T) == typeof(TLNonEncryptedTransportMessage)) return (T)(Object)new TLNonEncryptedTransportMessage(from, false);

            var type = from.ReadUInt32();
            if (type == 0xFFFFFF0D || typeof(T) == typeof(TLActionInfo))
            {
                return (T)(Object)new TLActionInfo(from, true);
            }
            else if ((TLType)type == TLType.Vector)
            {
                return (T)(Object)Activator.CreateInstance(typeof(T), from, fromCache);
            }
            else if ((TLType)type == TLType.BoolTrue)
            {
                return (T)(Object)true;
            }
            else if ((TLType)type == TLType.BoolFalse)
            {
                return (T)(Object)false;
            }
            else
            {
                return Read<T>(from, (TLType)type, fromCache);
            }
        }

        public static void Write(TLBinaryWriter to, object value, bool toCache)
        {
            var type = value.GetType();
            if (type == typeof(UInt32)) to.Write((uint)value);
            else if (type == typeof(Int32)) to.Write((int)value);
            else if (type == typeof(UInt64)) to.Write((ulong)value);
            else if (type == typeof(Int64)) to.Write((long)value);
            else if (type == typeof(Double)) to.Write((double)value);
            else if (type == typeof(Boolean)) to.Write((bool)value);
            else if (type == typeof(String)) to.Write((string)value);
            else if (type == typeof(Byte[])) to.WriteByteArray((byte[])value);
            else if (type == typeof(TLInt128)) ((TLInt128)value).Write(to);
            else if (type == typeof(TLInt256)) ((TLInt256)value).Write(to);
            else ((TLObject)value).Write(to, toCache);
        }

        public static T From<T>(byte[] bytes)
        {
            using (var reader = new TLBinaryReader(bytes))
            {
                return Read<T>(reader, false);
            }
        }
    }
}
