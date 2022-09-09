using Telegram.Td.Api;
using Unigram.Services;
using Unigram.ViewModels.Gallery;

namespace Unigram.ViewModels.Delegates
{
    public interface IGalleryDelegate
    {
        IClientService ClientService { get; }

        void OpenItem(GalleryContent item);
        void OpenFile(GalleryContent item, File file);
    }
}
