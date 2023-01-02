using Telegram.Td.Api;
using Unigram.Services;

namespace Unigram.ViewModels.Gallery
{
    public abstract class GalleryContent
    {
        protected readonly IClientService _clientService;

        public GalleryContent(IClientService clientService)
        {
            _clientService = clientService;
        }

        public IClientService ClientService => _clientService;

        public abstract File GetFile();

        public abstract File GetThumbnail();

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
        public virtual bool IsVideoNote { get; private set; }

        public virtual bool HasStickers { get; private set; }

        public virtual bool CanShare { get; private set; }
        public virtual bool CanView { get; private set; }

        public virtual bool CanSave { get; private set; }
        public virtual bool CanCopy { get; private set; }

        public virtual bool IsProtected { get; private set; } = false;

        public virtual bool IsPublic { get; protected set; }
        public virtual bool IsPersonal { get; protected set; }

        public virtual InputMessageContent ToInput()
        {
            return null;
        }
    }
}
