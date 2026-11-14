//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

#if !NET10_0_OR_GREATER
namespace System
{
    /// <summary>
    /// The two-argument EventHandler arrived in .NET 10, and CsWinRT 3 projects TypedEventHandler
    /// as it, so the app's own events are declared with it on every flavour. The .NET Native
    /// target lacks it. Never hand one of these to a WinRT event there: the projection still
    /// wants TypedEventHandler, which the aliases in CsWinRT.cs name per flavour.
    /// </summary>
    public delegate void EventHandler<in TSender, in TEventArgs>(TSender sender, TEventArgs e);
}
#endif
