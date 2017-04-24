using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public partial class TLMessageMediaPhoto
    {
        public override double DownloadingProgress
        {
            get => Photo.DownloadingProgress;
            set
            {
                Photo.DownloadingProgress = value;
                RaisePropertyChanged(() => DownloadingProgress);
                RaisePropertyChanged(() => Progress);
            }
        }

        public override double UploadingProgress
        {
            get => Photo.UploadingProgress;
            set
            {
                Photo.UploadingProgress = value;
                RaisePropertyChanged(() => UploadingProgress);
                RaisePropertyChanged(() => Progress);
            }
        }

        public override double LastProgress
        {
            get => Photo.LastProgress;
            set
            {
                Photo.LastProgress = value;
                RaisePropertyChanged(() => LastProgress);
            }
        }
    }
}
