using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public interface ITLTransferable : INotifyPropertyChanged
    {
        double DownloadingProgress { get; set; }
        double UploadingProgress { get; set; }
        double Progress { get; }

        bool IsTransferring { get; /*set;*/ }

        double LastProgress { get; set; }

        Progress<double> Download();
        Progress<double> Upload();
    }
}
