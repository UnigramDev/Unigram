//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Td.Api;
using Unigram.Services;
using Unigram.ViewModels;

namespace Unigram.Common
{
    public static class UpdateManager
    {
        #region Subscribe by ref

        public static void Subscribe(object sender, MessageWithOwner message, File file, ref string token, UpdateHandler<File> handler, bool completionOnly = false, bool keepTargetAlive = false)
        {
            Subscribe(sender, message.ClientService.SessionId, file, ref token, handler, completionOnly, keepTargetAlive);
        }

        public static void Subscribe(object sender, IClientService clientService, File file, ref string token, UpdateHandler<File> handler, bool completionOnly = false, bool keepTargetAlive = false)
        {
            Subscribe(sender, clientService.SessionId, file, ref token, handler, completionOnly, keepTargetAlive);
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
            Subscribe(sender, message.ClientService.SessionId, file, handler, completionOnly, keepTargetAlive, unsubscribe);
        }

        public static void Subscribe(object sender, IClientService clientService, File file, UpdateHandler<File> handler, bool completionOnly = false, bool keepTargetAlive = false, bool unsubscribe = true)
        {
            Subscribe(sender, clientService.SessionId, file, handler, completionOnly, keepTargetAlive, unsubscribe);
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
