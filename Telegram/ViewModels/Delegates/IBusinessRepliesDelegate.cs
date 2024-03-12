using Telegram.Td.Api;

namespace Telegram.ViewModels.Delegates
{
    public interface IBusinessRepliesDelegate : IViewModelDelegate
    {
        void UpdateQuickReplyShortcut(QuickReplyShortcut shortcut);
    }
}
