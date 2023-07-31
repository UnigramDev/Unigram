//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.ViewModels.Gallery
{
    public abstract class GalleryMedia
    {
        protected readonly IClientService _clientService;

        public GalleryMedia(IClientService clientService)
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
