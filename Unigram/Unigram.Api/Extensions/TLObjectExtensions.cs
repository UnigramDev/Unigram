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

        public static string Substr(this string source, int startIndex, int endIndex)
        {
            return source.Substring(startIndex, endIndex - startIndex);
        }

        public static TLPhoto GetPhoto(this TLMessageBase messageBase)
        {
            if (messageBase is TLMessage message)
            {
                if (message.Media is TLMessageMediaPhoto photoMedia)
                {
                    return photoMedia.Photo as TLPhoto;
                }
                else if (message?.Media is TLMessageMediaWebPage webPageMedia && webPageMedia.WebPage is TLWebPage webPage)
                {
                    return webPage.Photo as TLPhoto;
                }
            }
            else if (messageBase is TLMessageService serviceMessage)
            {
                if (serviceMessage.Action is TLMessageActionChatEditPhoto editPhotoAction)
                {
                    return editPhotoAction.Photo as TLPhoto;
                }
            }

            return null;
        }

        public static TLDocument GetDocument(this TLMessageBase messageBase)
        {
            if (messageBase is TLMessage message)
            {
                if (message.Media is TLMessageMediaDocument documentMedia)
                {
                    return documentMedia.Document as TLDocument;
                }
                else if (message?.Media is TLMessageMediaWebPage webPageMedia && webPageMedia.WebPage is TLWebPage webPage)
                {
                    return webPage.Document as TLDocument;
                }
            }

            return null;
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
