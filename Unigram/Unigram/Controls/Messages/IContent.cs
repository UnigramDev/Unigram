using Telegram.Td.Api;
using Unigram.ViewModels;
using Windows.UI.Composition;

namespace Unigram.Controls.Messages
{
    public interface IContent
    {
        MessageViewModel Message { get; }

        void UpdateMessage(MessageViewModel message);

        bool IsValid(MessageContent content, bool primary);
    }

    public interface IContentWithFile : IContent
    {
        void UpdateMessageContentOpened(MessageViewModel message);
        void UpdateFile(MessageViewModel message, File file);
    }

    public interface IContentWithMask
    {
        CompositionBrush GetAlphaMask();
    }
}
