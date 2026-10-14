//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.ComponentModel;
using Telegram.Common;
using Telegram.Td.Api;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml;

namespace Telegram.Controls
{
    /// <summary>
    /// A box for a message's text, whichever implements it: <see cref="FormattedTextBox"/> over
    /// RichEdit, or <see cref="RichEditor.MessageTextBox"/> over the native editor. Only what
    /// callers need is here, and nothing shaped like either; a caller written against this does
    /// not care which one the diagnostics switch picked.
    /// </summary>
    public interface IFormattedTextInput
    {
        /// <summary>The plain text, as it would be sent.</summary>
        string Text { get; }

        bool IsEmpty { get; }

        FormattedText GetFormattedText(bool clear = false, bool parseMarkdown = true);

        PreparedText GetPreparedText(bool clear = false, bool parseMarkdown = true);

        void SetText(FormattedText text);

        void ClearText();

        /// <summary>Replaces the selection with <paramref name="text"/>, and leaves the caret after it.</summary>
        void InsertText(string text);

        /// <summary>Replaces the selection with <paramref name="text"/>, formatting and all, and leaves the caret after it.</summary>
        void InsertFormattedText(FormattedText text);

        /// <summary>Replaces the selection with a mention of <paramref name="userId"/> reading <paramref name="text"/>, and a space.</summary>
        void InsertMention(string text, long userId);

        void InsertEmoji(Sticker sticker, bool track = false);

        void PasteFromClipboard();

        // =====================================================================================
        // The caret
        // =====================================================================================

        bool IsSelectionEmpty { get; }

        /// <summary>Where the caret is in <see cref="Text"/>; with a selection, where it ends.</summary>
        int CaretOffset { get; }

        void MoveCaretToEnd();

        /// <summary>No selection, and the caret before everything.</summary>
        bool IsCaretAtStart { get; }

        /// <summary>No selection, and the caret after everything.</summary>
        bool IsCaretAtEnd { get; }

        /// <summary>What comes before the selection and after it, markdown parsed: a paste that becomes a rich message keeps both.</summary>
        void SplitAtSelection(out FormattedText before, out FormattedText after);

        /// <summary>
        /// What is being typed before the caret, as <see cref="AutocompleteEntityFinder"/> finds it.
        /// <paramref name="start"/> is where it begins in the box's own terms: only
        /// <see cref="ReplaceQuery"/> takes it.
        /// </summary>
        bool TryGetQuery(out AutocompleteEntity entity, out string query, out int start);

        /// <summary>From <paramref name="start"/> to the caret, as <paramref name="text"/> instead, the caret after it.</summary>
        void ReplaceQuery(int start, FormattedText text);

        /// <summary>Whether the box is showing suggestions of its own, which take the arrows, Tab and Enter.</summary>
        bool HasOpenSuggestions { get; }

        /// <summary>A hint drawn after <paramref name="prefix"/>, which the text starts with: the inline bot's placeholder. Nulls clear it.</summary>
        void SetInlinePlaceholder(string prefix, string hint);

        // =====================================================================================
        // Settings
        // =====================================================================================

        FormattedTextEntity AllowedEntities { get; set; }

        int MaxLength { get; set; }

        string PlaceholderText { get; set; }

        bool IsReadOnly { get; set; }

        bool IsReplaceEmojiEnabled { get; set; }

        bool IsSpellCheckEnabled { get; set; }

        bool IsTextPredictionEnabled { get; set; }

        /// <summary>
        /// Whether Enter, or Ctrl+Enter, raises <see cref="Submitted"/>. Off, it is a new line:
        /// the composer turns it off while an inline bot is being typed to.
        /// </summary>
        bool IsSubmitEnabled { get; set; }

        bool FocusInput(FocusState state);

        // =====================================================================================
        // Events
        // =====================================================================================

        /// <summary>Raised when the text or its formatting changed, not when only the selection moved.</summary>
        event EventHandler ContentChanged;

        /// <summary>Raised when the caret moved or the selection changed, the text with it or not.</summary>
        event EventHandler SelectionChanged;

        /// <summary>Raised on Enter, or Ctrl+Enter, as the user's send setting has it.</summary>
        event EventHandler Submitted;

        /// <summary>
        /// Raised for a paste the box will not take as text: files, pictures, and HTML pasted
        /// with Ctrl+Shift+V. Left unhandled, the box pastes what text there is.
        /// </summary>
        event EventHandler<FormattedTextPasteEventArgs> PasteRequested;
    }

    public sealed class FormattedTextPasteEventArgs : HandledEventArgs
    {
        public FormattedTextPasteEventArgs(DataPackageView package)
        {
            Package = package;
        }

        public DataPackageView Package { get; }
    }
}
