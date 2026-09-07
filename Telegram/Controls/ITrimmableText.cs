//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;

namespace Telegram.Controls
{
    /// <summary>
    /// Text that can be cut short, and say that it was. What <see cref="BlockQuote"/> needs of
    /// whatever it holds to offer to expand it - which both text engines answer.
    /// </summary>
    public interface ITrimmableText
    {
        /// <summary>The lines the text may take, 0 for as many as it needs.</summary>
        int MaxLines { get; set; }

        /// <summary>Whether the text did not fit in <see cref="MaxLines"/>.</summary>
        bool IsTextTrimmable { get; }

        /// <summary>
        /// Raised when the answer above changes, which is at measure: whether the text fits is
        /// not known until it has been laid out at a width.
        /// </summary>
        event EventHandler IsTextTrimmableChanged;
    }
}
