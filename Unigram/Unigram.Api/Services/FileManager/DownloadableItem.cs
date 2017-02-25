using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Api.TL;

namespace Telegram.Api.Services.FileManager
{
    public class DownloadableItem
    {
        public int DCId { get; set; }

        public string FileName { get; set; }

        public TLObject Owner { get; set; }

        internal TaskCompletionSource<DownloadableItem> Callback { get; set; }

        internal IProgress<double> Progress { get; set; }

        //public System.Action<DownloadableItem> Callback { get; set; }

        //public IList<System.Action<DownloadableItem>> Callbacks { get; set; }

        public TLFileLocation Location { get; set; }

        public TLInputDocumentFileLocation InputAudioLocation { get; set; }

        public TLInputDocumentFileLocation InputVideoLocation { get; set; }

        public TLInputDocumentFileLocation InputDocumentLocation { get; set; }

        public TLInputFileLocationBase InputEncryptedFileLocation { get; set; }

        public List<DownloadablePart> Parts { get; set; }

        public string IsoFileName { get; set; }

        public bool IsCancelled { get; set; }

        public bool SuppressMerge { get; set; }

        #region Http

        public string SourceUri { get; set; }

        public string DestFileName { get; set; }

        public Action<DownloadableItem> Action { get; set; }

        public System.Action<DownloadableItem> FaultCallback { get; set; }

        public IList<System.Action<DownloadableItem>> FaultCallbacks { get; set; }

        public double Timeout { get; set; }

        public void IncreaseTimeout()
        {
            Timeout = Timeout * 2.0;
            if (Timeout == 0.0)
            {
                Timeout = 4.0;
            }
            if (Timeout >= 32.0)
            {
                Timeout = 4.0;
            }
        }
        #endregion
    }
}