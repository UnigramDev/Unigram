//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Telegram.Views.Tabbed;
using Windows.Foundation;

namespace Telegram.Entities
{
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(VideoGeneration))]
    [JsonSerializable(typeof(ImageGeneration))]
    [JsonSerializable(typeof(NavigateToHistoryEntryParameters))]
    [JsonSerializable(typeof(NavigationHistory))]
    [JsonSerializable(typeof(Rect))]
    internal partial class SourceGenerationContext : JsonSerializerContext
    {
    }
}
