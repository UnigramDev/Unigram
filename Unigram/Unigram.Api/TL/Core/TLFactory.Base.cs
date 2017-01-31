using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace Telegram.Api.TL
{
    public static partial class TLFactory
    {
        public static T Read<T>(TLBinaryReader from)
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
            else if (typeof(T) == typeof(TLNonEncryptedTransportMessage)) return (T)(Object)new TLNonEncryptedTransportMessage(from);

            var type = from.ReadUInt32();
            if (type == 0xFFFFFF0D || typeof(T) == typeof(TLActionInfo))
            {
                return (T)(Object)new TLActionInfo(from);
            }
            else if ((TLType)type == TLType.Vector)
            {
                if (typeof(T) != typeof(object))
                {
                    return (T)(Object)Activator.CreateInstance(typeof(T), from);
                }
                else
                {
                    var length = from.ReadUInt32();
                    if (length > 0)
                    {
                        var inner = from.ReadUInt32();
                        from.BaseStream.Position -= 8;

                        var innerType = Type.GetType($"Telegram.Api.TL.TL{(TLType)inner}");
                        if (innerType != null)
                        {
                            var baseType = innerType.GetTypeInfo().BaseType;
                            if (baseType.Name != "TLObject")
                            {
                                innerType = baseType;
                            }

                            var d1 = typeof(TLVector<>);
                            var typeArgs = new Type[] { innerType };
                            var makeme = d1.MakeGenericType(typeArgs);
                            return (T)(Object)Activator.CreateInstance(makeme, from);
                        }
                        else
                        {
                            // A base type collection (int, long, double, bool)
                            // TODO:
                            return (T)(Object)null;
                        }
                    }
                    else
                    {
                        // An empty collection, so we can't determine the generic type
                        // TODO:
                        return (T)(Object)new TLVectorEmpty();
                    }
                }
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
                return Read<T>(from, (TLType)type);
            }
        }

        public static void Write(TLBinaryWriter to, object value)
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
            else ((TLObject)value).Write(to);
        }

        public static T From<T>(byte[] bytes)
        {
            using (var reader = new TLBinaryReader(bytes))
            {
                return Read<T>(reader);
            }
        }
    }
}
