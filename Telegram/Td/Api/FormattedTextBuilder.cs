//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;

namespace Telegram.Td.Api
{
    /// <summary>
    /// Builds a FormattedText by substituting placeholders, keeping the entity offsets in step.
    /// </summary>
    /// <remarks>
    /// The one way to edit a FormattedText in place. It never touches its source - not the text,
    /// not the entity list, not the TextEntity instances - so a value that arrived from TDLib can
    /// be passed in and the list the parser handed out stays shared and unedited.
    ///
    /// That last part is the reason this exists rather than a copy at each call site: substitution
    /// moves an entity by writing Offset and Length, and a shallow copy of the list still points at
    /// the caller's entities. The previous code copied the list when it was read-only and then
    /// shifted the instances inside it, which reached back into whatever it was given.
    ///
    /// FormattedText.Replace is not the same operation and cannot replace this. It splits the text
    /// around each occurrence and concatenates, so an entity spanning a placeholder ends up split
    /// in two; here it grows to cover the substitution, which is what the markdown paths need.
    ///
    /// A struct because MessageServiceText builds one per service message rendered.
    /// </remarks>
    public struct FormattedTextBuilder
    {
        private string _text;

        // Null until an edit needs it, which for a substitution that matched nothing is never.
        private MutableVector<TextEntity> _entities;

        public FormattedTextBuilder(string text)
        {
            _text = text ?? string.Empty;
            _entities = null;
        }

        public FormattedTextBuilder(FormattedText source)
        {
            _text = source?.Text ?? string.Empty;
            _entities = null;

            var entities = source?.Entities;
            if (entities != null && entities.Count > 0)
            {
                _entities = new MutableVector<TextEntity>(entities.Count);

                for (int i = 0; i < entities.Count; i++)
                {
                    var entity = entities[i];

                    // Cloned, not shared: Shift writes Offset and Length. The type is immutable and
                    // can be carried over as is.
                    _entities.Add(new TextEntity(entity.Offset, entity.Length, entity.Type));
                }
            }
        }

        public readonly string Text => _text;

        public readonly int IndexOf(string value)
        {
            return _text.IndexOf(value, StringComparison.Ordinal);
        }

        /// <summary>
        /// Replaces the first occurrence of <paramref name="placeholder"/> with <paramref name="value"/>,
        /// covering it with one entity. Returns false when the placeholder is absent, leaving the
        /// builder untouched.
        /// </summary>
        public bool Substitute(string placeholder, string value, TextEntityType type)
        {
            var index = IndexOf(placeholder);
            if (index < 0)
            {
                return false;
            }

            value ??= string.Empty;

            Shift(index, value.Length - placeholder.Length);
            Splice(index, placeholder.Length, value);
            AddEntity(index, value.Length, type);

            return true;
        }

        /// <summary>
        /// Replaces the first occurrence of <paramref name="placeholder"/> with a formatted value,
        /// carrying the value's own entities across at their new offsets.
        /// </summary>
        public bool Substitute(string placeholder, FormattedText value)
        {
            var index = IndexOf(placeholder);
            if (index < 0)
            {
                return false;
            }

            var text = value?.Text ?? string.Empty;

            Shift(index, text.Length - placeholder.Length);
            Splice(index, placeholder.Length, text);

            var entities = value?.Entities;
            if (entities != null)
            {
                for (int i = 0; i < entities.Count; i++)
                {
                    var entity = entities[i];
                    AddEntity(index + entity.Offset, entity.Length, entity.Type);
                }
            }

            return true;
        }

        /// <summary>
        /// Deletes every occurrence of <paramref name="value"/>, shifting the entities after each.
        /// </summary>
        public void Strip(string value)
        {
            // With no entities to keep in step this is one pass rather than a copy of the whole
            // text per occurrence, which is the ReplaceWithLink(string, ...) case - by far the
            // more common of the two.
            if (_entities == null)
            {
                _text = _text.Replace(value, string.Empty);
                return;
            }

            int index;
            while ((index = IndexOf(value)) >= 0)
            {
                Shift(index, -value.Length);
                Splice(index, value.Length, string.Empty);
            }
        }

        public void AddEntity(int offset, int length, TextEntityType type)
        {
            if (length <= 0)
            {
                return;
            }

            _entities ??= new MutableVector<TextEntity>();
            _entities.Add(new TextEntity(offset, length, type ?? new TextEntityTypeBold()));
        }

        private void Splice(int index, int length, string value)
        {
            _text = _text.Remove(index, length).Insert(index, value);
        }

        private readonly void Shift(int index, int shift)
        {
            if (shift == 0 || _entities == null)
            {
                return;
            }

            for (int i = 0; i < _entities.Count; i++)
            {
                var entity = _entities[i];

                // An entity that CONTAINS the edit point grows instead of moving: the markdown
                // paths parse their entities before substituting, so the bold run wrapping a whole
                // sentence spans the placeholder it is about to receive.
                if (entity.Offset > index)
                {
                    entity.Offset += shift;
                }
                else if (entity.Offset + entity.Length > index)
                {
                    entity.Length += shift;
                }
            }
        }

        /// <summary>
        /// The result. With nothing to carry it shares the empty singleton rather than allocating,
        /// so the caller must not add to it - use AddEntity before this point instead.
        /// </summary>
        public readonly FormattedText ToFormattedText()
        {
            return new FormattedText(_text, _entities ?? Array.Empty<TextEntity>());
        }

        public static implicit operator FormattedText(FormattedTextBuilder builder)
        {
            return builder.ToFormattedText();
        }
    }
}
