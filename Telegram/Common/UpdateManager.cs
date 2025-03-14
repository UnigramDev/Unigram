//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;

namespace Telegram.Common
{
    public static class UpdateManager
    {
        #region Subscribe by ref

        public static void Subscribe(object sender, MessageWithOwner message, File file, ref long token, UpdateHandler<File> handler, bool completionOnly = false)
        {
            Subscribe(sender, message.ClientService.SessionId, file, ref token, handler, completionOnly);
        }

        public static void Subscribe(object sender, IClientService clientService, File file, ref long token, UpdateHandler<File> handler, bool completionOnly = false)
        {
            Subscribe(sender, clientService.SessionId, file, ref token, handler, completionOnly);
        }

        public static void Subscribe(object sender, int sessionId, File file, ref long token, UpdateHandler<File> handler, bool completionOnly = false)
        {
            var value = (sessionId << 16) | file.Id;
            if (completionOnly)
            {
                value |= 0x01000000;
            }

            if (value == token)
            {
                return;
            }
            else if (token != 0)
            {
                EventAggregator.Current.Unsubscribe(sender, token, false);
            }

            EventAggregator.Current.Subscribe(sender, token = value, handler, false);
        }

        #endregion

        public static void Unsubscribe(object sender, ref long token, bool completionOnly = false)
        {
            if (token != 0)
            {
                EventAggregator.Current.Unsubscribe(sender, token, false);
                token = 0;
            }
        }
    }
}
