using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.Services.FileManager.EventArgs
{
    public class UploadProgressChangedEventArgs
    {
        public double Progress { get; protected set; }

        public UploadableItem Item { get; protected set; }

        public UploadProgressChangedEventArgs(UploadableItem item, double progress)
        {
            Item = item;
            Progress = progress;
        }
    }
}
