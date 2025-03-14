//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Telegram.Controls;
using Telegram.Services;
using Telegram.Td.Api;

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

        public static FormattedText GetMarkdownText(string text)
        {
            return GetMarkdownText(new FormattedText(text, Array.Empty<TextEntity>()));
        }

        public static FormattedText GetMarkdownText(string text, IList<TextEntity> entities)
        {
            return GetMarkdownText(new FormattedText(text, entities));
        }

        public static FormattedText GetMarkdownText(FormattedText text)
        {
            var result = Client.Execute(new GetMarkdownText(text));
            if (result is FormattedText formatted)
            {
                return formatted;
            }

            return text;
        }

        public static IList<TextEntity> GetTextEntities(string text)
        {
            var result = Client.Execute(new GetTextEntities(text));
            if (result is TextEntities entities)
            {
                return entities.Entities;
            }

            return Array.Empty<TextEntity>();
        }

        public static FormattedText MergeEntities(FormattedText text, IList<TextEntity> entities)
        {
            if (entities.Count > 0 && text.Entities.Count > 0)
            {
                var merge = new FormattedText(text.Text, new List<TextEntity>(text.Entities));

                foreach (var entity in entities)
                {
                    merge.Entities.Add(entity);
                }

                return merge;
            }
            else if (entities.Count > 0)
            {
                return new FormattedText(text.Text, entities);
            }

            return text;
        }

        public static int SearchQuote(FormattedText text, TextQuote quote)
        {
            var result = Client.Execute(new SearchQuote(text, quote.Text, quote.Position));
            if (result is FoundPosition position)
            {
                return position.Position;
            }

            return -1;
        }

        public static FormattedText Format(string format, params FormattedText[] args)
        {
            // TODO: doesn't support more than 10 parameters but I'm lazy

            var builder = new StringBuilder(format);
            var entities = new List<TextEntity>();

            var argument = 0;
            var index = -1;

            for (int i = 0; i < builder.Length; i++)
            {
                var c = builder[i];
                if (c == '{')
                {
                    index = i;
                }
                else if (index >= 0)
                {
                    if (c >= '0' && c <= '9')
                    {
                        argument = c - '0';
                        continue;
                    }

                    if (c == '}')
                    {
                        if (argument < args.Length)
                        {
                            var text = args[argument];

                            builder.Remove(index, i - index + 1);
                            builder.Insert(index, text.Text);

                            foreach (var entity in text.Entities)
                            {
                                entities.Add(new TextEntity(entity.Offset + index, entity.Length, entity.Type));
                            }

                            i = index + text.Text.Length - 1;
                        }
                    }

                    argument = 0;
                    index = -1;
                }
            }

            return new FormattedText(builder.ToString(), entities);
        }



        public static bool CheckQuickReplyShortcutName(string name)
        {
            var result = Client.Execute(new CheckQuickReplyShortcutName(name));
            return result is Ok;
        }



        public static string GetThemeParametersJsonString(ThemeParameters themeParameters)
        {
            var result = Client.Execute(new GetThemeParametersJsonString(themeParameters)) as Text;
            return result.TextValue;
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
