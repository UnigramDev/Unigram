using System;
using System.IO;
#if WINDOWS_PHONE
using System.Net;
using System.Windows;
#elif WIN_RT
using Windows.UI.Xaml;
#endif
using Telegram.Api.Extensions;
using Telegram.Api.TL;

namespace Telegram.Api
{
    [Flags]
    public enum WebPageFlags
    {
        Type = 0x1,
        SiteName = 0x2,
        Title = 0x4,
        Description = 0x8,
        Photo = 0x10,
        Embed = 0x20,
        EmbedSize = 0x40,
        Duration = 0x80,
        Author = 0x100,
        Document = 0x200,
    }

    public abstract class TLWebPageBase : TLObject
    {
        public TLLong Id { get; set; }

        #region Additional
        public abstract Visibility SiteNameVisibility { get; }
        public abstract Visibility AuthorVisibility { get; }
        public abstract Visibility TitleVisibility { get; }
        public abstract Visibility DescriptionVisibility { get; }
        #endregion
    }

    public class TLWebPageEmpty : TLWebPageBase
    {
        public const uint Signature = TLConstructors.TLWebPageEmpty;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLLong>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLLong>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Id.ToStream(output);
        }

        public override Visibility SiteNameVisibility
        {
            get { return Visibility.Collapsed; }
        }

        public override Visibility AuthorVisibility
        {
            get { return Visibility.Collapsed; }
        }

        public override Visibility TitleVisibility
        {
            get { return Visibility.Collapsed; }
        }

        public override Visibility DescriptionVisibility
        {
            get { return Visibility.Collapsed; }
        }
    }

    public class TLWebPagePending : TLWebPageBase
    {
        public const uint Signature = TLConstructors.TLWebPagePending;

        public TLInt Date { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLLong>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                Date.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLLong>(input);
            Date = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Id.ToStream(output);
            Date.ToStream(output);
        }

        public override Visibility SiteNameVisibility
        {
            get { return Visibility.Collapsed; }
        }

        public override Visibility AuthorVisibility
        {
            get { return Visibility.Collapsed; }
        }

        public override Visibility TitleVisibility
        {
            get { return Visibility.Collapsed; }
        }

        public override Visibility DescriptionVisibility
        {
            get { return Visibility.Collapsed; }
        }
    }

    public class TLWebPage : TLWebPageBase
    {
        public const uint Signature = TLConstructors.TLWebPage;

        public TLInt Flags { get; set; }

        public TLString Url { get; set; }

        public TLString DisplayUrl { get; set; }

        public TLString Type { get; set; }

        public TLString SiteName { get; set; }

        public TLString Title { get; set; }

        public TLString Description { get; set; }

        public TLPhotoBase Photo { get; set; }

        public TLString EmbedUrl { get; set; }

        public TLString EmbedType { get; set; }

        public TLInt EmbedWidth { get; set; }

        public TLInt EmbedHeight { get; set; }

        public TLInt Duration { get; set; }

        public string DurationString
        {
            get
            {
                if (Duration == null) return null;

                var timeSpan = TimeSpan.FromSeconds(Duration.Value);

                if (timeSpan.Hours > 0)
                {
                    return timeSpan.ToString(@"h\:mm\:ss");
                }

                return timeSpan.ToString(@"m\:ss");
            }
        }

        public TLString Author { get; set; }


        public override Visibility SiteNameVisibility
        {
            get { return TLString.IsNullOrEmpty(SiteName) ? Visibility.Collapsed : Visibility.Visible; }
        }

        public override Visibility AuthorVisibility
        {
            get { return TLString.IsNullOrEmpty(Author) ? Visibility.Collapsed : Visibility.Visible; }
        }

        public override Visibility TitleVisibility
        {
            get { return TLString.IsNullOrEmpty(Title)? Visibility.Collapsed : Visibility.Visible; }
        }

        public override Visibility DescriptionVisibility
        {
            get { return TLString.IsNullOrEmpty(Description) ? Visibility.Collapsed : Visibility.Visible; }
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);
            Flags = GetObject<TLInt>(bytes, ref position);
            Id = GetObject<TLLong>(bytes, ref position);
            Url = GetObject<TLString>(bytes, ref position);
            DisplayUrl = GetObject<TLString>(bytes, ref position);
            if (IsSet(Flags, (int)WebPageFlags.Type))
            {
                Type = GetObject<TLString>(bytes, ref position);
            }
            if (IsSet(Flags, (int)WebPageFlags.SiteName))
            {
                SiteName = GetObject<TLString>(bytes, ref position);
            }
            if (IsSet(Flags, (int)WebPageFlags.Title))
            {
                Title = GetObject<TLString>(bytes, ref position);
            }
            if (IsSet(Flags, (int)WebPageFlags.Description))
            {
                Description = GetObject<TLString>(bytes, ref position);
            }
            if (IsSet(Flags, (int)WebPageFlags.Photo))
            {
                Photo = GetObject<TLPhotoBase>(bytes, ref position);
            }
            if (IsSet(Flags, (int)WebPageFlags.Embed))
            {
                EmbedUrl = GetObject<TLString>(bytes, ref position);
                EmbedType = GetObject<TLString>(bytes, ref position);
            }
            if (IsSet(Flags, (int)WebPageFlags.EmbedSize))
            {
                EmbedWidth = GetObject<TLInt>(bytes, ref position);
                EmbedHeight = GetObject<TLInt>(bytes, ref position);
            }
            if (IsSet(Flags, (int)WebPageFlags.Duration))
            {
                Duration = GetObject<TLInt>(bytes, ref position);
            }
            if (IsSet(Flags, (int)WebPageFlags.Author))
            {
                Author = GetObject<TLString>(bytes, ref position);
            }

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Flags.ToBytes(),
                Id.ToBytes(),
                Url.ToBytes(),
                DisplayUrl.ToBytes(),
                ToBytes(Type, Flags, (int)WebPageFlags.Type),
                ToBytes(SiteName, Flags, (int)WebPageFlags.SiteName),
                ToBytes(Title, Flags, (int)WebPageFlags.Title),
                ToBytes(Description, Flags, (int)WebPageFlags.Description),
                ToBytes(Photo, Flags, (int)WebPageFlags.Photo),
                ToBytes(EmbedUrl, Flags, (int)WebPageFlags.Embed),
                ToBytes(EmbedType, Flags, (int)WebPageFlags.Embed),
                ToBytes(EmbedWidth, Flags, (int)WebPageFlags.EmbedSize),
                ToBytes(EmbedHeight, Flags, (int)WebPageFlags.EmbedSize),
                ToBytes(Duration, Flags, (int)WebPageFlags.Duration),
                ToBytes(Author, Flags, (int)WebPageFlags.Author));
        }

        public override TLObject FromStream(Stream input)
        {
            Flags = GetObject<TLInt>(input);
            Id = GetObject<TLLong>(input);
            Url = GetObject<TLString>(input);
            DisplayUrl = GetObject<TLString>(input);
            if (IsSet(Flags, (int)WebPageFlags.Type))
            {
                Type = GetObject<TLString>(input);
            }
            if (IsSet(Flags, (int)WebPageFlags.SiteName))
            {
                SiteName = GetObject<TLString>(input);
            }
            if (IsSet(Flags, (int)WebPageFlags.Title))
            {
                Title = GetObject<TLString>(input);
            }
            if (IsSet(Flags, (int)WebPageFlags.Description))
            {
                Description = GetObject<TLString>(input);
            }
            if (IsSet(Flags, (int)WebPageFlags.Photo))
            {
                Photo = GetObject<TLPhotoBase>(input);
            }
            if (IsSet(Flags, (int)WebPageFlags.Embed))
            {
                EmbedUrl = GetObject<TLString>(input);
                EmbedType = GetObject<TLString>(input);
            }
            if (IsSet(Flags, (int)WebPageFlags.EmbedSize))
            {
                EmbedWidth = GetObject<TLInt>(input);
                EmbedHeight = GetObject<TLInt>(input);
            }
            if (IsSet(Flags, (int)WebPageFlags.Duration))
            {
                Duration = GetObject<TLInt>(input);
            }
            if (IsSet(Flags, (int)WebPageFlags.Author))
            {
                Author = GetObject<TLString>(input);
            }

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Flags.ToStream(output);
            Id.ToStream(output);
            Url.ToStream(output);
            DisplayUrl.ToStream(output);
            ToStream(output, Type, Flags, (int)WebPageFlags.Type);
            ToStream(output, SiteName, Flags, (int)WebPageFlags.SiteName);
            ToStream(output, Title, Flags, (int)WebPageFlags.Title);
            ToStream(output, Description, Flags, (int)WebPageFlags.Description);
            ToStream(output, Photo, Flags, (int)WebPageFlags.Photo);
            ToStream(output, EmbedUrl, Flags, (int)WebPageFlags.Embed);
            ToStream(output, EmbedType, Flags, (int)WebPageFlags.Embed);
            ToStream(output, EmbedWidth, Flags, (int)WebPageFlags.EmbedSize);
            ToStream(output, EmbedHeight, Flags, (int)WebPageFlags.EmbedSize);
            ToStream(output, Duration, Flags, (int)WebPageFlags.Duration);
            ToStream(output, Author, Flags, (int) WebPageFlags.Author);
        }
    }

    public class TLWebPage35 : TLWebPage
    {
        public new const uint Signature = TLConstructors.TLWebPage35;

        public TLDocumentBase Document { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);
            Flags = GetObject<TLInt>(bytes, ref position);
            Id = GetObject<TLLong>(bytes, ref position);
            Url = GetObject<TLString>(bytes, ref position);
            DisplayUrl = GetObject<TLString>(bytes, ref position);
            if (IsSet(Flags, (int)WebPageFlags.Type))
            {
                Type = GetObject<TLString>(bytes, ref position);
            }
            if (IsSet(Flags, (int)WebPageFlags.SiteName))
            {
                SiteName = GetObject<TLString>(bytes, ref position);
            }
            if (IsSet(Flags, (int)WebPageFlags.Title))
            {
                Title = GetObject<TLString>(bytes, ref position);
            }
            if (IsSet(Flags, (int)WebPageFlags.Description))
            {
                Description = GetObject<TLString>(bytes, ref position);
            }
            if (IsSet(Flags, (int)WebPageFlags.Photo))
            {
                Photo = GetObject<TLPhotoBase>(bytes, ref position);
            }
            if (IsSet(Flags, (int)WebPageFlags.Embed))
            {
                EmbedUrl = GetObject<TLString>(bytes, ref position);
                EmbedType = GetObject<TLString>(bytes, ref position);
            }
            if (IsSet(Flags, (int)WebPageFlags.EmbedSize))
            {
                EmbedWidth = GetObject<TLInt>(bytes, ref position);
                EmbedHeight = GetObject<TLInt>(bytes, ref position);
            }
            if (IsSet(Flags, (int)WebPageFlags.Duration))
            {
                Duration = GetObject<TLInt>(bytes, ref position);
            }
            if (IsSet(Flags, (int)WebPageFlags.Author))
            {
                Author = GetObject<TLString>(bytes, ref position);
            }
            if (IsSet(Flags, (int)WebPageFlags.Document))
            {
                Document = GetObject<TLDocumentBase>(bytes, ref position);
            }

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Flags.ToBytes(),
                Id.ToBytes(),
                Url.ToBytes(),
                DisplayUrl.ToBytes(),
                ToBytes(Type, Flags, (int)WebPageFlags.Type),
                ToBytes(SiteName, Flags, (int)WebPageFlags.SiteName),
                ToBytes(Title, Flags, (int)WebPageFlags.Title),
                ToBytes(Description, Flags, (int)WebPageFlags.Description),
                ToBytes(Photo, Flags, (int)WebPageFlags.Photo),
                ToBytes(EmbedUrl, Flags, (int)WebPageFlags.Embed),
                ToBytes(EmbedType, Flags, (int)WebPageFlags.Embed),
                ToBytes(EmbedWidth, Flags, (int)WebPageFlags.EmbedSize),
                ToBytes(EmbedHeight, Flags, (int)WebPageFlags.EmbedSize),
                ToBytes(Duration, Flags, (int)WebPageFlags.Duration),
                ToBytes(Author, Flags, (int)WebPageFlags.Author),
                ToBytes(Document, Flags, (int)WebPageFlags.Document));
        }

        public override TLObject FromStream(Stream input)
        {
            Flags = GetObject<TLInt>(input);
            Id = GetObject<TLLong>(input);
            Url = GetObject<TLString>(input);
            DisplayUrl = GetObject<TLString>(input);
            if (IsSet(Flags, (int)WebPageFlags.Type))
            {
                Type = GetObject<TLString>(input);
            }
            if (IsSet(Flags, (int)WebPageFlags.SiteName))
            {
                SiteName = GetObject<TLString>(input);
            }
            if (IsSet(Flags, (int)WebPageFlags.Title))
            {
                Title = GetObject<TLString>(input);
            }
            if (IsSet(Flags, (int)WebPageFlags.Description))
            {
                Description = GetObject<TLString>(input);
            }
            if (IsSet(Flags, (int)WebPageFlags.Photo))
            {
                Photo = GetObject<TLPhotoBase>(input);
            }
            if (IsSet(Flags, (int)WebPageFlags.Embed))
            {
                EmbedUrl = GetObject<TLString>(input);
                EmbedType = GetObject<TLString>(input);
            }
            if (IsSet(Flags, (int)WebPageFlags.EmbedSize))
            {
                EmbedWidth = GetObject<TLInt>(input);
                EmbedHeight = GetObject<TLInt>(input);
            }
            if (IsSet(Flags, (int)WebPageFlags.Duration))
            {
                Duration = GetObject<TLInt>(input);
            }
            if (IsSet(Flags, (int)WebPageFlags.Author))
            {
                Author = GetObject<TLString>(input);
            }
            if (IsSet(Flags, (int)WebPageFlags.Document))
            {
                Document = GetObject<TLDocumentBase>(input);
            }

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Flags.ToStream(output);
            Id.ToStream(output);
            Url.ToStream(output);
            DisplayUrl.ToStream(output);
            ToStream(output, Type, Flags, (int)WebPageFlags.Type);
            ToStream(output, SiteName, Flags, (int)WebPageFlags.SiteName);
            ToStream(output, Title, Flags, (int)WebPageFlags.Title);
            ToStream(output, Description, Flags, (int)WebPageFlags.Description);
            ToStream(output, Photo, Flags, (int)WebPageFlags.Photo);
            ToStream(output, EmbedUrl, Flags, (int)WebPageFlags.Embed);
            ToStream(output, EmbedType, Flags, (int)WebPageFlags.Embed);
            ToStream(output, EmbedWidth, Flags, (int)WebPageFlags.EmbedSize);
            ToStream(output, EmbedHeight, Flags, (int)WebPageFlags.EmbedSize);
            ToStream(output, Duration, Flags, (int)WebPageFlags.Duration);
            ToStream(output, Author, Flags, (int)WebPageFlags.Author);
            ToStream(output, Document, Flags, (int)WebPageFlags.Document);
        }
    }
}
