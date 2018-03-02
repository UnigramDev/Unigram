using TdWindows;

namespace Unigram.ViewModels.Delegates
{
    public interface IFileDelegate : IViewModelDelegate
    {
        void UpdateFile(File file);
    }
}
