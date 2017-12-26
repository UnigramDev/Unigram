using System;
using System.IO;
using System.Linq;
using Telegram.Api.TL;
using Windows.Foundation;

namespace Telegram.Api.TL
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

        public static IAsyncOperationWithProgress<TResult, TProgress> Handle<TResult, TProgress>(this IAsyncOperationWithProgress<TResult, TProgress> op, IProgress<TProgress> handler)
        {
            if (handler != null)
            {
                op.Progress = (s, args) =>
                {
                    handler.Report(args);
                };
            }

            return op;
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
                else if (message.Media is TLMessageMediaWebPage webPageMedia && webPageMedia.WebPage is TLWebPage webPage)
                {
                    return webPage.Photo as TLPhoto;
                }
                else if (message.Media is TLMessageMediaGame gameMedia)
                {
                    return gameMedia.Game.Photo as TLPhoto;
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
                else if (message.Media is TLMessageMediaWebPage webPageMedia && webPageMedia.WebPage is TLWebPage webPage)
                {
                    return webPage.Document as TLDocument;
                }
                else if (message.Media is TLMessageMediaGame gameMedia)
                {
                    return gameMedia.Game.Document as TLDocument;
                }
            }

            return null;
        }

        #region Game

        public static bool IsGame(this TLMessage message)
        {
            return message.Media is TLMessageMediaGame;
        }

        #endregion

        #region Photo

        public static bool IsPhoto(this TLMessage message)
        {
            return message.Media is TLMessageMediaPhoto;
        }

        #endregion

        #region Gif
        public static bool IsGif(this TLMessage message)
        {
            var documentMedia = message.Media as TLMessageMediaDocument;
            return documentMedia != null && IsGif(documentMedia.Document as TLDocument);
        }

        public static bool IsGif(this TLDocumentBase documentBase)
        {
            var document = documentBase as TLDocument;
            return document != null && IsGif(document);
        }

        public static bool IsGif(TLDocument document)
        {
            if (document != null && document.MimeType.Equals("video/mp4", StringComparison.OrdinalIgnoreCase))
            {
                return IsGif(document, document.Size);
            }

            return false;

            //TLDocumentExternal tLDocumentExternal = document as TLDocumentExternal;
            //return tLDocumentExternal != null && string.Equals(tLDocumentExternal.Type.ToString(), "gif", 5) && TLMessageBase.IsGif(tLDocumentExternal, null);
        }

        public static bool IsGif(TLDocument document, int size)
        {
            if (size > 0 && size < 10383360)
            {
                var animatedAttribute = document.Attributes.FirstOrDefault(x => x is TLDocumentAttributeAnimated) as TLDocumentAttributeAnimated;
                var videoAttribute = document.Attributes.FirstOrDefault(x => x is TLDocumentAttributeVideo) as TLDocumentAttributeVideo;
                if (animatedAttribute != null && videoAttribute != null)
                {
                    return !videoAttribute.IsRoundMessage;
                }
            }

            return false;
        }
        #endregion

        #region Music
        public static bool IsMusic(this TLMessage message)
        {
            var documentMedia = message.Media as TLMessageMediaDocument;
            return documentMedia != null && IsMusic(documentMedia.Document);
        }

        public static bool IsMusic(this TLDocumentBase documentBase)
        {
            var document = documentBase as TLDocument;
            return document != null && IsMusic(document, document.Size);
        }

        public static bool IsMusic(TLDocument document, int size)
        {
            if (size > 0)
            {
                var audioAttribute = document.Attributes.FirstOrDefault(x => x is TLDocumentAttributeAudio) as TLDocumentAttributeAudio;
                if (audioAttribute != null && !audioAttribute.IsVoice)
                {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region Video

        public static bool IsVideo(this TLMessage message)
        {
            // TODO: 24/07/2017, warning: || documentMedia.HasTTLSeconds
            var documentMedia = message.Media as TLMessageMediaDocument;
            return documentMedia != null && (IsVideo(documentMedia.Document) || documentMedia.HasTTLSeconds);
        }

        public static bool IsVideo(this TLDocumentBase documentBase)
        {
            var document = documentBase as TLDocument;
            return document != null && IsVideo(document, document.Size);
        }

        public static bool IsVideo(TLDocument document, int size)
        {
            if (size > 0)
            {
                var animatedAttribute = document.Attributes.FirstOrDefault(x => x is TLDocumentAttributeAnimated) as TLDocumentAttributeAnimated;
                var videoAttribute = document.Attributes.FirstOrDefault(x => x is TLDocumentAttributeVideo) as TLDocumentAttributeVideo;
                if (videoAttribute != null && animatedAttribute == null)
                {
                    return !videoAttribute.IsRoundMessage;
                }
            }
            return false;
        }

        #endregion

        #region RoundVideo

        public static bool IsRoundVideo(this TLMessage message)
        {
            var documentMedia = message.Media as TLMessageMediaDocument;
            return documentMedia != null && IsRoundVideo(documentMedia.Document);
        }

        public static bool IsRoundVideo(this TLDocumentBase documentBase)
        {
            var document = documentBase as TLDocument;
            return document != null && IsRoundVideo(document, document.Size);
        }

        public static bool IsRoundVideo(TLDocument document, int size)
        {
            if (size > 0)
            {
                var videoAttribute = document.Attributes.FirstOrDefault(x => x is TLDocumentAttributeVideo) as TLDocumentAttributeVideo;
                //var animatedAttribute = document.Attributes.OfType<TLDocumentAttributeAnimated>().FirstOrDefault();
                if (videoAttribute != null /*&& animatedAttribute == null*/)
                {
                    return videoAttribute.IsRoundMessage;
                }
            }
            return false;
        }

        #endregion

        #region Audio

        public static bool IsAudio(this TLMessage message)
        {
            var documentMedia = message.Media as TLMessageMediaDocument;
            return documentMedia != null && IsAudio(documentMedia.Document);
        }

        public static bool IsAudio(TLDocumentBase documentBase)
        {
            var document = documentBase as TLDocument;
            return document != null && IsAudio(document, document.Size);
        }

        public static bool IsAudio(TLDocument document, int size)
        {
            var audioAttribute = document.Attributes.FirstOrDefault(x => x is TLDocumentAttributeAudio) as TLDocumentAttributeAudio;
            return audioAttribute != null && !audioAttribute.IsVoice;
        }

        #endregion

        #region Voice

        public static bool IsVoice(this TLMessage message)
        {
            var documentMedia = message.Media as TLMessageMediaDocument;
            return documentMedia != null && IsVoice(documentMedia.Document);
        }

        public static bool IsVoice(this TLDocumentBase documentBase)
        {
            var document = documentBase as TLDocument;
            return document != null && IsVoice(document, document.Size);
        }

        public static bool IsVoice(TLDocument document, int size)
        {
            var audioAttribute = document.Attributes.FirstOrDefault(x => x is TLDocumentAttributeAudio) as TLDocumentAttributeAudio;
            return audioAttribute != null && audioAttribute.IsVoice;
        }

        #endregion

        #region Sticker

        public static bool IsSticker(this TLMessage message)
        {
            var documentMedia = message.Media as TLMessageMediaDocument;
            if (documentMedia != null)
            {
                return IsSticker(documentMedia.Document);
            }
            return false;
        }

        public static bool IsSticker(this TLDocumentBase documentBase)
        {
            var document = documentBase as TLDocument;
            if (document != null && document.Size > 0 && document.Size < 262144)
            {
                var attribute = document.Attributes.FirstOrDefault(x => x is TLDocumentAttributeSticker) as TLDocumentAttributeSticker;
                if (attribute != null && string.Equals(document.MimeType.ToString(), "image/webp", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        #endregion

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
