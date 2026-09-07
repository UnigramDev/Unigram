//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    /// <summary>
    /// Text a caller can set and dress without knowing which engine draws it: the inline one,
    /// which builds an element per run, or the direct one, which owns its layout.
    ///
    /// Every member is something both controls already have - the inline one inherits most of
    /// them from Control - so this declares what they share rather than adding to either. A
    /// Style still has to be picked per engine: one targets a type, and these are two.
    /// </summary>
    public interface ITextPresenter
    {
        double FontSize { get; set; }
        FontFamily FontFamily { get; set; }
        FontWeight FontWeight { get; set; }
        FontStyle FontStyle { get; set; }

        Brush Foreground { get; set; }
        TextAlignment TextAlignment { get; set; }

        bool IsTextSelectionEnabled { get; set; }

        void SetText(IClientService clientService, RichText text);
    }
}
