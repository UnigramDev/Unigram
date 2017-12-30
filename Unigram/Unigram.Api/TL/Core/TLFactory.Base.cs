using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Telegram.Api.Native.TL;

namespace Telegram.Api.TL
{
    public static partial class TLFactory
    {
        public static T Read<T>(TLBinaryReader from)
        {
            var type = typeof(T);
            if (type == typeof(UInt32)) return (T)(Object)from.ReadUInt32();
            else if (type == typeof(Int32)) return (T)(Object)from.ReadInt32();
            else if (type == typeof(UInt64)) return (T)(Object)from.ReadUInt64();
            else if (type == typeof(Int64)) return (T)(Object)from.ReadInt64();
            else if (type == typeof(Double)) return (T)(Object)from.ReadDouble();
            else if (type == typeof(Boolean)) return (T)(Object)from.ReadBoolean();
            else if (type == typeof(String)) return (T)(Object)from.ReadString();
            else if (type == typeof(Byte[])) return (T)(Object)from.ReadByteArray();

            var magic = from.ReadUInt32();
            /*if (type == 0xFFFFFF0D || typeof(T) == typeof(TLActionInfo))
            {
                return (T)(Object)new TLActionInfo(from);
            }
            else*/ if ((TLType)magic == TLType.Vector)
            {
                if (typeof(T) != typeof(object) && typeof(T) != typeof(TLObject))
                {
                    return (T)(Object)Activator.CreateInstance(typeof(T), from);
                }
                else
                {
                    var length = from.ReadUInt32();
                    if (length > 0)
                    {
                        var inner = from.ReadUInt32();
                        from.Position -= 8;

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
            else if (magic == 0x997275b5 || magic == 0x3fedd339)
            {
                return (T)(Object)true;
            }
            else if (magic == 0xbc799737)
            {
                return (T)(Object)false;
            }
            else
            {
                return Read<T>(from, (TLType)magic);
            }
        }

        public static T Read<T>(TLBinaryReader from, uint constructor)
        {
            // TODO
            //if (typeof(T) == typeof(UInt32)) return (T)(Object)from.ReadUInt32();
            //else if (typeof(T) == typeof(Int32)) return (T)(Object)from.ReadInt32();
            //else if (typeof(T) == typeof(UInt64)) return (T)(Object)from.ReadUInt64();
            //else if (typeof(T) == typeof(Int64)) return (T)(Object)from.ReadInt64();
            //else if (typeof(T) == typeof(Double)) return (T)(Object)from.ReadDouble();
            //else if (typeof(T) == typeof(Boolean)) return (T)(Object)from.ReadBoolean();
            //else if (typeof(T) == typeof(String)) return (T)(Object)from.ReadString();
            //else if (typeof(T) == typeof(Byte[])) return (T)(Object)from.ReadByteArray();

            if (constructor == 0x997275B5)
            {
                return (T)(Object)true;
            }
            else if (constructor == 0xBC799737)
            {
                return (T)(Object)false;
            }

            var magic = (TLType)constructor;
            if (magic == TLType.Vector)
            {
                if (typeof(T) != typeof(object) && typeof(T) != typeof(TLObject))
                {
                    return (T)(Object)Activator.CreateInstance(typeof(T), from);
                }
                else
                {
                    var length = from.ReadUInt32();
                    if (length > 0)
                    {
                        var inner = from.ReadUInt32();
                        from.Position -= 8;

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
            else
            {
                return Read<T>(from, magic);
            }
        }

        public static void Write<T>(TLBinaryWriter to, object value)
        {
            if (value == null)
            {
                to.WriteUInt32(0x56730BCC);
                return;
            }

            var type = typeof(T);
            if (type == typeof(UInt32)) to.WriteUInt32((uint)value);
            else if (type == typeof(Int32)) to.WriteInt32((int)value);
            else if (type == typeof(UInt64)) to.WriteUInt64((ulong)value);
            else if (type == typeof(Int64)) to.WriteInt64((long)value);
            else if (type == typeof(Double)) to.WriteDouble((double)value);
            else if (type == typeof(Boolean)) to.WriteBoolean((bool)value);
            else if (type == typeof(String)) to.WriteString((string)value);
            else if (type == typeof(Byte[])) to.WriteByteArray((byte[])value);
            else to.WriteObject((TLObject)value);
        }
    }
}
