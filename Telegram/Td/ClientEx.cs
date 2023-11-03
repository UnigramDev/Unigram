//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Controls;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Media;

namespace Telegram.Td
{
    static class ClientEx
    {
        public static void Send(this Client client, Function function, Action<BaseObject> handler)
        {
            if (handler == null)
            {
                client.Send(function, null);
            }
            else
            {
                client.Send(function, new TdHandler(handler));
            }
        }

        public static void Send(this Client client, Function function)
        {
            client.Send(function, null);
        }

        public static Task<BaseObject> SendAsync(this Client client, Function function, Action<BaseObject> closure)
        {
            var tsc = new TdCompletionSource(closure);
            client.Send(function, tsc);

            return tsc.Task;
        }

        public static bool SearchByPrefix(string input, string query)
        {
            var result = Client.Execute(new SearchStringsByPrefix(new[] { input }, query, 1, true));
            if (result is FoundPositions positions && positions.Positions.Count == 1)
            {
                return positions.Positions[0] >= 0;
            }

            return false;
        }

        public static FormattedText ParseMarkdown(string text)
        {
            return ParseMarkdown(new FormattedText(text, Array.Empty<TextEntity>()));
        }

        public static FormattedText ParseMarkdown(string text, IList<TextEntity> entities)
        {
            return ParseMarkdown(new FormattedText(text, entities));
        }

        public static FormattedText ParseMarkdown(FormattedText text)
        {
            var result = Client.Execute(new ParseMarkdown(text));
            if (result is FormattedText formatted)
            {
                return formatted;
            }

            return text;
        }



        public static SolidColorBrush GetAccentBrush(this IClientService clientService, int id)
        {
            var accent = clientService.GetAccentColor(id);
            if (accent != null)
            {
                return new SolidColorBrush(accent.LightThemeColors[0]);
            }

            return PlaceholderImage.GetBrush(id);
        }
    }
}
