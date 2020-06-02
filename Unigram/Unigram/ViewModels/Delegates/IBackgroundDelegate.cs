using Telegram.Td.Api;

namespace Unigram.ViewModels.Delegates
{
    public interface IBackgroundDelegate : IViewModelDelegate
    {
        void UpdateBackground(Background wallpaper);
    }
}
