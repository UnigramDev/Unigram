using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.ViewModels.Gallery;

namespace Unigram.ViewModels.Delegates
{
    public interface IGalleryDelegate
    {
        void OpenItem(GalleryItem item);
        void OpenFile(GalleryItem item, File file);
    }
}
