//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

namespace Telegram.Host
{
    /// <summary>
    /// What an <see cref="IslandWindow"/> tells the <c>WindowContext</c> that owns it. Every member
    /// here exists because the UWP half raises the matching event from a <c>Window</c> or an
    /// <c>ApplicationView</c>, and nothing on this host does - a window that never says it was
    /// activated leaves night mode, the passcode lock and a bot's visibility_changed quietly
    /// inert rather than broken, which is worse.
    ///
    /// One interface rather than a delegate per message: they are only correct together, since
    /// activation, visibility and size all move in the same handful of window messages.
    /// </summary>
    internal interface IIslandOwner
    {
        /// <summary>
        /// The window is about to be destroyed. False takes the close over - the owner may have a
        /// question for the user first - and the owner destroys the window itself once it is done.
        /// </summary>
        bool CloseRequested();

        void ActivationChanged(bool active);

        void VisibilityChanged(bool visible);

        /// <summary>
        /// The client area changed size, or the scale it is measured in did. No argument: the
        /// owner reads the size back, because the caller would have to convert to logical pixels
        /// to pass it and the owner has to do that anyway.
        /// </summary>
        void SizeChanged();
    }
}
