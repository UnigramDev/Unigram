using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public partial class TLMessageMediaDocument
    {
        public override double DownloadingProgress
        {
            get => Document.DownloadingProgress;
            set
            {
                Document.DownloadingProgress = value;
                RaisePropertyChanged(() => DownloadingProgress);
                RaisePropertyChanged(() => Progress);
            }
        }

        public override double UploadingProgress
        {
            get => Document.UploadingProgress;
            set
            {
                Document.UploadingProgress = value;
                RaisePropertyChanged(() => UploadingProgress);
                RaisePropertyChanged(() => Progress);
            }
        }

        public override double LastProgress
        {
            get => Document.LastProgress;
            set
            {
                Document.LastProgress = value;
                RaisePropertyChanged(() => LastProgress);
            }
        }
    }
}
