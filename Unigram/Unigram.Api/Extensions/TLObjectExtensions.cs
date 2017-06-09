using System.IO;
using Telegram.Api.TL;

namespace Telegram.Api
{
    public static class TLObjectExtensions
    {
        public static string TrimStart(this string target, string trimString)
        {
            string result = target;
            while (result.StartsWith(trimString))
            {
                result = result.Substring(trimString.Length);
            }

            return result;
        }
    }
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
