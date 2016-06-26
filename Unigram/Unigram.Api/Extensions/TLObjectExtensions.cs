using System.IO;
using Telegram.Api.TL;

namespace Telegram.Api.Extensions
{
    //public static class TLObjectExtensions
    //{
    //    public static void NullableToStream(this TLObject obj, Stream output)
    //    {
    //        if (obj == null)
    //        {
    //            output.Write(new TLNull().ToBytes());
    //        }
    //        else
    //        {
    //            obj.ToStream(output);
    //        }
    //    }

    //    public static T NullableFromStream<T>(Stream input)
    //    {
    //        var obj = TLObjectGenerator.GetNullableObject<T>(input);
            
    //        if (obj == null) return default(T);

    //        return (T)(object)obj.FromStream<T>(input);
    //    }





    //    public static void NullableToStream(this long? obj, Stream output)
    //    {
    //        if (obj == null)
    //        {
    //            output.Write(new TLNull().ToBytes());
    //        }
    //        else
    //        {
    //            obj.ToStream(output);
    //        }
    //    }

    //    public static void NullableToStream(this int? obj, Stream output)
    //    {
    //        if (obj == null)
    //        {
    //            output.Write(new TLNull().ToBytes());
    //        }
    //        else
    //        {
    //            obj.ToStream(output);
    //        }
    //    }
    //}
}
