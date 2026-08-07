//
// Copyright (c) Fela Ameghino 2015-2026
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
        // Set on the token of a subscriber that only wants the update saying the file is
        // there. Placed above the session, and clear of the sign bit.
        public const long CompletionOnly = 1L << 62;

        /// <summary>
        /// Routing key for the updates of one file.
        ///
        /// The session is part of it because a file id only identifies a file within one.
        /// Ids are handles TDLib hands out one after another for every photo, thumbnail,
        /// sticker and document a session touches, and nothing bounds them, so they are
        /// given the full lower half of the token. Packed any tighter, the id of a session
        /// left running long enough reaches into the session field, and the updates for a
        /// file on one account start arriving at whoever subscribed to a file on another.
        /// </summary>
        public static long CreateToken(int sessionId, int fileId, bool completionOnly = false)
        {
            var token = ((long)sessionId << 32) | (uint)fileId;

            if (completionOnly)
            {
                token |= CompletionOnly;
            }

            return token;
        }

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
            var value = CreateToken(sessionId, file.Id, completionOnly);

            if (value == token)
            {
                return;
            }
            else if (token != 0)
            {
                EventAggregator.Current.Unsubscribe(sender, token);
            }

            EventAggregator.Current.Subscribe(sender, token = value, handler);
        }

        #endregion

        public static void Unsubscribe(object sender, ref long token)
        {
            if (token != 0)
            {
                EventAggregator.Current.Unsubscribe(sender, token);
                token = 0;
            }
        }
    }
}
