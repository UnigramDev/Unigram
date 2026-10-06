//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Native.Graphics;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;

namespace Telegram.Views.Wallet
{
    /// <summary>
    /// Somewhere the send screen's stone can land: the transfer's row in the wallet's history, or
    /// its message in a chat.
    /// </summary>
    public interface IWalletTransferSite
    {
        XamlRoot XamlRoot { get; }

        /// <summary>
        /// What is laid out once the site is: the flight measures after the first layout pass to
        /// finish once it is bound, and not before.
        /// </summary>
        FrameworkElement Anchor { get; }

        /// <summary>
        /// Where the stone's panel ends up, in window coordinates. The panel keeps its proportions,
        /// so the width says how far it is scaled.
        /// </summary>
        Rect LandingBounds(Size stone);

        /// <summary>
        /// The site's press, read for <c>Press</c>: the stone squashes with it as the site is
        /// pushed down. Null where there is none yet.
        /// </summary>
        CompositionPropertySet Impact { get; }

        /// <summary>
        /// The stone will arrive after <paramref name="delay"/>, which is when the site is pushed
        /// down - on the compositor's clock, not when the flight hears it has landed.
        /// </summary>
        void ScheduleImpact(TimeSpan delay);

        /// <summary>
        /// The stone has arrived.
        /// </summary>
        WalletTransferLanding Land(WalletTransferFlight flight, Scene3DPanel stone);

        /// <summary>
        /// The flight went elsewhere, or out, without landing here.
        /// </summary>
        void ForgetStone();
    }

    public enum WalletTransferLanding
    {
        // Nothing there to take it: the stone goes out where it landed.
        Refused,

        // Kept in the flight's overlay over the site until the site lets it go.
        Held,

        // Moved into the site's own panel, which goes on showing it.
        Taken
    }
}
