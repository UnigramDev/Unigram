using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.Services.FileManager.EventArgs
{
    public class UploadingCanceledEventArgs
    {
        public UploadableItem Item { get; protected set; }

        public UploadingCanceledEventArgs(UploadableItem item)
        {
            Item = item;
        }
    }
}
