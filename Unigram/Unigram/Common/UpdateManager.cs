using Telegram.Td.Api;
using Unigram.Services;
using Unigram.ViewModels;

namespace Unigram.Common
{
    public static class UpdateManager
    {
        #region Subscribe by ref

        public static void Subscribe(object sender, MessageViewModel message, File file, ref string token, UpdateHandler<File> handler, bool completionOnly = false, bool keepTargetAlive = false)
        {
            Subscribe(sender, message.ProtoService.SessionId, file, ref token, handler, completionOnly, keepTargetAlive);
        }

        public static void Subscribe(object sender, IProtoService protoService, File file, ref string token, UpdateHandler<File> handler, bool completionOnly = false, bool keepTargetAlive = false)
        {
            Subscribe(sender, protoService.SessionId, file, ref token, handler, completionOnly, keepTargetAlive);
        }

        public static void Subscribe(object sender, int sessionId, File file, ref string token, UpdateHandler<File> handler, bool completionOnly = false, bool keepTargetAlive = false)
        {
            var value = $"{sessionId}_{file.Id}";
            if (token != value && token != null)
            {
                EventAggregator.Default.Unregister<File>(sender, token);
            }

            EventAggregator.Default.Register(sender, token = value, handler, keepTargetAlive, completionOnly);
        }

        #endregion

        #region Subscribe

        public static void Subscribe(object sender, MessageViewModel message, File file, UpdateHandler<File> handler, bool completionOnly = false, bool keepTargetAlive = false, bool unsubscribe = true)
        {
            Subscribe(sender, message.ProtoService.SessionId, file, handler, completionOnly, keepTargetAlive, unsubscribe);
        }

        public static void Subscribe(object sender, IProtoService protoService, File file, UpdateHandler<File> handler, bool completionOnly = false, bool keepTargetAlive = false, bool unsubscribe = true)
        {
            Subscribe(sender, protoService.SessionId, file, handler, completionOnly, keepTargetAlive, unsubscribe);
        }

        public static void Subscribe(object sender, int sessionId, File file, UpdateHandler<File> handler, bool completionOnly = false, bool keepTargetAlive = false, bool unsubscribe = true)
        {
            if (unsubscribe)
            {
                EventAggregator.Default.Unregister<File>(sender);
            }

            EventAggregator.Default.Register(sender, $"{sessionId}_{file.Id}", handler, keepTargetAlive, completionOnly);
        }

        #endregion

        public static void Unsubscribe(object sender)
        {
            EventAggregator.Default.Unregister<File>(sender);
        }

        public static void Unsubscribe(object sender, ref string token)
        {
            EventAggregator.Default.Unregister<File>(sender, token);
            token = null;
        }
    }
}
