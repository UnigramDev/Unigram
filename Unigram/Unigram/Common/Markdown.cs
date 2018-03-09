using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Telegram.Td.Api;
using Unigram.Services;

namespace Unigram.Common
{
    // Courtesy: https://github.com/LonamiWebs/Telethon/blob/master/telethon/extensions/markdown.py
    public static class Markdown
    {
        private enum Mode
        {
            None = 0,
            Bold = 1,
            Italic = 2,
            Code = 3,
            Pre = 4
        }

        private static readonly Dictionary<Mode, string> _delimiters = new Dictionary<Mode, string>
        {
            { Mode.Bold, "**" },
            { Mode.Italic, "__" },
            { Mode.Code, "`" },
            { Mode.Pre, "```" },
        };

        private static readonly Regex _defaultUrl = new Regex("\\G\\[(.+?)\\]\\((.+?)\\)", RegexOptions.Compiled);

        public static bool IsValidLink(IProtoService protoService, string url)
        {
            if (protoService == null)
            {
                return Uri.IsWellFormedUriString(url, UriKind.Absolute);
            }

            var response = protoService.Execute(new GetTextEntities(url));
            if (response is TextEntities entities)
            {
                return entities.Entities.Count == 1 && entities.Entities[0].Offset == 0 && entities.Entities[0].Length == url.Length && entities.Entities[0].Type is TextEntityTypeUrl;
            }

            return false;
        }

        public static IList<TextEntity> Parse(IProtoService protoService, ref string message)
        {
            // Cannot use a for loop because we need to skip some indices
            var i = 0;
            var result = new List<TextEntity>();

            var current = Mode.None;
            var offset = 0;

            // Work on byte level with the utf-16le encoding to get the offsets right.
            // The offset will just be half the index we're at.
            while (i < message.Length)
            {
                Match url_match = null;
                if (current == Mode.None)
                {
                    // If we're not inside a previous match since Telegram doesn't allow
                    // nested message entities, try matching the URL from the i'th pos.
                    url_match = _defaultUrl.Match(message, i);
                    if (url_match.Success && IsValidLink(protoService, url_match.Groups[2].Value))
                    {
                        // Replace the whole match with only the inline URL text.
                        message = message.Substring(0, url_match.Index) + url_match.Groups[1].Value + message.Substring(url_match.Index + url_match.Length);
                        result.Add(new TextEntity(i, url_match.Groups[1].Length, new TextEntityTypeTextUrl(url_match.Groups[2].Value)));

                        // We matched the delimiter which is now gone, and we'll add
                        // +2 before next iteration which will make us skip a character.
                        // Go back by one utf-16 encoded character (-2) to avoid it.
                        i += url_match.Groups[1].Length - 1;
                    }
                }

                if (url_match == null || !url_match.Success)
                {
                    foreach (var item in _delimiters)
                    {
                        var d = item.Value;
                        var m = item.Key;

                        if (current != Mode.None && current != m)
                        {
                            // We were inside another delimiter/mode, ignore this.
                            continue;
                        }

                        // Slice the string at the current i'th position to see if
                        // it matches the current delimiter d.
                        if (message.Length >= i + d.Length && message.Substring(i, d.Length).Equals(d))
                        {
                            if (message.Length >= i + d.Length + d.Length && message.Substring(i + d.Length, d.Length).Equals(d))
                            {
                                // The same delimiter can't be right afterwards, if
                                // this were the case we would match empty strings
                                // like `` which we don't want to.
                                continue;
                            }

                            // Get rid of the delimiter by slicing it away
                            message = message.Remove(i, d.Length);

                            if (m == Mode.Pre)
                            {
                                if (message.Length > i && char.IsSeparator(message[i]))
                                {
                                    message = message.Remove(i, 1);
                                }
                                else if (message.Length > i && i > 0 && char.IsSeparator(message[i - 1]))
                                {
                                    message = message.Remove(i - 1, 1);
                                }

                                message = message.Insert(i, "\n");
                            }

                            if (current == Mode.None)
                            {
                                offset = i;
                                current = m;
                                // No need to i -= 2 here because it's been already
                                // checked that next character won't be a delimiter.
                            }
                            else
                            {
                                switch (current)
                                {
                                    case Mode.Bold:
                                        result.Add(new TextEntity(offset, i - offset, new TextEntityTypeBold()));
                                        break;
                                    case Mode.Italic:
                                        result.Add(new TextEntity(offset, i - offset, new TextEntityTypeItalic()));
                                        break;
                                    case Mode.Code:
                                        result.Add(new TextEntity(offset, i - offset, new TextEntityTypeCode()));
                                        break;
                                    case Mode.Pre:
                                        result.Add(new TextEntity(offset, i - offset, new TextEntityTypePre()));
                                        break;
                                }

                                current = Mode.None;
                                i -= 1;  // Delimiter matched and gone, go back 1 char
                            }

                            break;
                        }
                    }
                }

                // Next iteration, utf-16 encoded characters need 2 bytes.
                i++;
            }

            if (current != Mode.None)
            {
                message = message.Insert(offset, _delimiters[current]);
            }

            return result;
        }
    }
}