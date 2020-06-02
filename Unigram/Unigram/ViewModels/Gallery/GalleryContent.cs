using System;
using Telegram.Td.Api;
using Unigram.Services;

namespace Unigram.ViewModels.Gallery
{
    public abstract class GalleryContent
    {
        protected readonly IProtoService _protoService;

        public GalleryContent(IProtoService protoService)
        {
            _protoService = protoService;
        }

        public IProtoService ProtoService => _protoService;

        public abstract File GetFile();
        public abstract File GetThumbnail();

        public abstract (File File, string FileName) GetFileAndName();

        public abstract bool UpdateFile(File file);

        public virtual object Constraint { get; private set; }

        public virtual object From { get; private set; }

        public virtual string Caption { get; private set; }

        public virtual int Date { get; private set; }

        public virtual int Duration { get; private set; }

        public virtual string MimeType { get; private set; }

        public bool IsPhoto => !IsVideo;

        public virtual bool IsVideo { get; private set; }
        public virtual bool IsStreamable { get; private set; } = true;
        public virtual bool IsLoop { get; private set; }

        public virtual bool HasStickers { get; private set; }

        public virtual bool CanShare { get; private set; }
        public virtual bool CanView { get; private set; }

        public virtual bool CanSave { get; private set; }
        public virtual bool CanCopy { get; private set; }

        public virtual void Share()
        {
            throw new NotImplementedException();
        }
    }
}
