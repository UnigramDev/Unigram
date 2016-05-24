using System.IO;
using Telegram.Api.TL;

namespace Telegram.Api.Extensions
{
    public static class TLObjectExtensions
    {
        public static void NullableToStream(this TLObject obj, Stream output)
        {
            if (obj == null)
            {
                output.Write(new TLNull().ToBytes());
            }
            else
            {
                obj.ToStream(output);
            }
        }

        public static T NullableFromStream<T>(Stream input) where T : TLObject
        {
            var obj = TLObjectGenerator.GetNullableObject<T>(input);
            
            if (obj == null) return null;

            return (T)obj.FromStream(input);
        }
    }
}
