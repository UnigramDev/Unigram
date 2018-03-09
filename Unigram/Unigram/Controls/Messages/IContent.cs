using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.ViewModels;

namespace Unigram.Controls.Messages
{
    public interface IContent
    {
        void UpdateMessage(MessageViewModel message);

        bool IsValid(MessageContent content, bool primary);
    }

    public interface IContentWithFile : IContent
    {
        void UpdateMessageContentOpened(MessageViewModel message);
        void UpdateFile(MessageViewModel message, File file);
    }
}
