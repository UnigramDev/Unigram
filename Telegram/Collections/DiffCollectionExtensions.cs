//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Td.Api;
using Telegram.ViewModels;

namespace Telegram.Collections
{
    public static class DiffCollectionExtensions
    {
        // Index-aligned: row i overwrites row i, and a row the diff handler calls equal is left alone.
        // Not the library's ReplaceDiff, which emits moves - a ListView is told about every change one
        // notification at a time.
        //
        // Not named ReplaceDiff either: an extension method loses to an instance method of the same
        // name, so every call here would bind to the library's instead.
        public static void Replace<T>(this DiffObservableCollection<T> destination, IReadOnlyList<T> source)
        {
            if (destination.Empty())
            {
                destination.AddRange(source);
                return;
            }
            else if (source.Count == 0)
            {
                destination.ClearIfNotEmpty();
                return;
            }

            var recycledItems = Math.Min(destination.Count, source.Count);
            var changedItems = Math.Max(destination.Count, source.Count);

            if (destination.Count > source.Count)
            {
                for (int i = recycledItems; i < changedItems; i++)
                {
                    destination.RemoveAt(recycledItems);
                }
            }
            else if (source.Count > destination.Count)
            {
                for (int i = recycledItems; i < changedItems; i++)
                {
                    destination.Insert(i, source[i]);
                }
            }

            for (int i = 0; i < recycledItems; i++)
            {
                var oldItem = destination[i];
                var newItem = source[i];

                if (destination.DefaultDiffHandler == null || !destination.DefaultDiffHandler.CompareItems(oldItem, newItem))
                {
                    destination[i] = newItem;
                }
                else if (!ReferenceEquals(oldItem, newItem))
                {
                    // Kept rather than replaced, so nothing re-renders it: the handler carries over
                    // what the new item knows.
                    destination.DefaultDiffHandler.UpdateItem(oldItem, newItem);
                }
            }

        }

        // The row already at this index, when it stands for the same chat or user. Replace aligns by
        // index, so this is the instance it would have kept anyway, and a keystroke that does not change
        // the results then allocates nothing.
        //
        // Public chats excluded: their subtitle is the @username the query selected, highlighted over the
        // query's length, so the same chat found by a different query has to render again.
        public static SearchResult Reuse(this KeyedCollection<SearchResult> destination, int index, Chat chat, User user, string query, SearchResultType type)
        {
            if (index < destination.Count)
            {
                var result = destination[index];
                if (result.Type == type && result.Chat == chat && result.User == user && !result.IsPublic)
                {
                    result.Query = query;
                    return result;
                }
            }

            return null;
        }
    }
}
