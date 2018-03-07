using Telegram.Td.Api;

namespace Unigram.ViewModels.Delegates
{
    public interface IFileDelegate : IViewModelDelegate
    {
        void UpdateFile(File file);
    }
}
