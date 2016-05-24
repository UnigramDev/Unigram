using System.Collections.Generic;
using Telegram.Api.TL;

namespace Telegram.Api.Services.FileManager
{
    public class DownloadableItem
    {
        public TLInt DCId { get; set; }

        public TLString FileName { get; set; }

        public TLObject Owner { get; set; }

        public TLFileLocation Location { get; set; }

        public TLInputAudioFileLocation InputAudioLocation { get; set; }

        public TLInputVideoFileLocation InputVideoLocation { get; set; }

        public TLInputDocumentFileLocation InputDocumentLocation { get; set; }

        public TLInputFileLocationBase InputEncryptedFileLocation { get; set; }

        public List<DownloadablePart> Parts { get; set; }

        public string IsoFileName { get; set; }

        public bool Canceled { get; set; }
    }
}