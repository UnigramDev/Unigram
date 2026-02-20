//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Telegram.Entities
{
    [JsonSerializable(typeof(NavigationHistory))]
    [JsonSerializable(typeof(NavigateToHistoryEntryParameters))]
    public partial class NavigationJsonContext : JsonSerializerContext
    {

    }

    public record NavigationHistory
    {
        [JsonPropertyName("currentIndex")]
        public int CurrentIndex { get; init; }

        [JsonPropertyName("entries")]
        public IReadOnlyList<HistoryEntry> Entries { get; init; }
    }

    public record HistoryEntry
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }

        [JsonPropertyName("title")]
        public string Title { get; init; }

        [JsonPropertyName("url")]
        public string Url { get; init; }

        [JsonIgnore]
        public string DocumentTitle
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Title))
                {
                    return Title;
                }

                return Url;
            }
        }

        [JsonIgnore]
        public string FaviconUri { get; init; }

        [JsonIgnore]
        public int Index { get; set; }
    }
}
