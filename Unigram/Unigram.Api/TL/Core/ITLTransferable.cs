using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public interface ITLTransferable
    {
        double DownloadingProgress { get; set; }
        double UploadingProgress { get; set; }
        double Progress { get; }

        double LastProgress { get; set; }

        Progress<double> Download();
        Progress<double> Upload();
    }
}
