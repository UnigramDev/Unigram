//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Td.Api;

namespace Telegram.Controls
{
    /// <summary>
    /// Granularity a <see cref="Telegram.Common.TextSelectionManager"/> gesture snaps to:
    /// a single tap selects by <see cref="Character"/> (a caret-to-caret drag), a double
    /// tap by <see cref="Word"/>, a triple tap by <see cref="Paragraph"/>.
    /// </summary>
    public enum TextSelectionGranularity
    {
        Character,
        Word,
        Paragraph
    }

    /// <summary>
    /// A control that can participate in a cross-block text selection driven by
    /// <see cref="Telegram.Common.TextSelectionManager"/>. Implementers are <c>FrameworkElement</c>s
    /// living in the manager's working tree (so the manager can read their geometry
    /// and route pointer points to them); this interface only adds the selection
    /// behaviour on top.
    ///
    /// Positions are 0-based indices into the control's selectable content; the full
    /// range is <c>[0, ContentLength]</c>. Selection is rendered as a HIGHLIGHT (not
    /// the native focus-bound selection), so any number of controls can show a
    /// selection at once and focus never enters the picture.
    /// </summary>
    public interface ISelectableControl
    {
        /// <summary>
        /// Whether this control participates in <see cref="TextSelectionManager"/>
        /// selection. The manager only collects controls where this is true.
        /// </summary>
        bool IsSelectionEnabled { get; }

        /// <summary>Number of selectable positions; <c>[0, ContentLength]</c> is everything.</summary>
        int ContentLength { get; }

        /// <summary>Hit-test a point in THIS control's coordinate space to a position index (clamped).</summary>
        int GetPositionFromPoint(Point point);

        /// <summary>
        /// Expand <paramref name="position"/> to the <c>[start, end)</c> of the word or
        /// paragraph it sits in (same index space as the other members), for double/triple
        /// tap and granular drag. <see cref="TextSelectionGranularity.Character"/> returns
        /// <c>(position, position)</c>.
        /// </summary>
        void GetSelectionBoundary(int position, TextSelectionGranularity granularity, out int start, out int end);

        /// <summary>Show a selection highlight over <c>[start, end)</c>; the manager always passes <c>start &lt;= end</c>.</summary>
        void Select(int start, int end);

        /// <summary>Remove any selection highlight from this control.</summary>
        void ClearSelection();

        /// <summary>
        /// The selected content over <c>[start, end)</c> as a <see cref="Telegram.Td.Api.FormattedText"/>
        /// (text + entities) for copy, or null when the range is empty.
        /// </summary>
        FormattedText GetSelectedText(int start, int end);

        /// <summary>
        /// Maps a position index to its offset in the control's SOURCE text — the original,
        /// unmodified text (e.g. the message's <see cref="Telegram.Td.Api.FormattedText"/>),
        /// without the virtual breaks the visual copy inserts. Used to position a reply quote.
        /// </summary>
        int GetSourceOffset(int position);

        /// <summary>
        /// The SOURCE text over absolute <c>[from, to)</c> (source offsets from
        /// <see cref="GetSourceOffset"/>) as a <see cref="Telegram.Td.Api.FormattedText"/>.
        /// Blocks that share a single source (a message's text split across blocks) all return
        /// the same source, so any of them yields the full range. Null when empty.
        /// </summary>
        FormattedText GetSourceText(int from, int to);
    }
}
