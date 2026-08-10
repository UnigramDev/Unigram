//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using Telegram.Td.Api;

namespace Telegram.Controls.Messages
{
    /// <summary>
    /// Holds message content controls that a bubble is done with, so the next bubble
    /// needing that kind can take one rather than inflate a template.
    ///
    /// A bubble reuses the control it already has whenever the new message is of the same
    /// kind, but a recycled container rarely draws the same kind twice: scrolling a mixed
    /// history, or switching chats, hands every container something else, and the control
    /// it was holding is dropped for a freshly built one.
    ///
    /// Bounded per kind, deliberately. A control is only of use to a realized container,
    /// so keeping more than there are containers cannot pay off, while the kinds seen over
    /// a session would otherwise each retain their high water mark for good — and these
    /// are not small: a PhotoContent alone carries an AspectView, two image brushes, two
    /// animated images and a button.
    /// </summary>
    public class MessageContentRecyclePool
    {
        private const int Capacity = 8;

        // The type is a key and nothing more: it is compared and hashed, never reflected
        // on. Building a missing control from it instead of through the switch that owns
        // that decision would need Activator, which .NET Native cannot serve without a
        // runtime directive — and would fail in the AOT build alone, not in Debug.
        private readonly Dictionary<Type, Stack<IContent>> _pool = new();

        /// <summary>
        /// Takes a control able to render <paramref name="content"/>, or null.
        ///
        /// Which kind of control a piece of content needs is decided by the bubble, and
        /// not by the content alone — an invoice draws through four of them depending on
        /// its media. Rather than restate that here, every kind held is asked whether it
        /// can take the content, using the same test the bubble uses to keep the control
        /// it already has. The pool holds one entry per kind, so this is a handful of type
        /// checks at most.
        /// </summary>
        public IContent TryGet(MessageContent content)
        {
            foreach (var stack in _pool.Values)
            {
                if (stack.Count > 0 && stack.Peek().IsValid(content, true))
                {
                    return stack.Pop();
                }
            }

            return null;
        }

        /// <summary>
        /// Offers a recycled control back. The caller must have called
        /// <see cref="IContent.Recycle"/> first: what is kept here is expected to hold no
        /// message, no subscription and nothing of what it last drew.
        /// </summary>
        public void Put(IContent content)
        {
            if (content == null)
            {
                return;
            }

            var type = content.GetType();

            if (!_pool.TryGetValue(type, out var stack))
            {
                _pool[type] = stack = new Stack<IContent>();
            }

            if (stack.Count < Capacity)
            {
                stack.Push(content);
            }
        }

        public void Clear()
        {
            _pool.Clear();
        }
    }
}
