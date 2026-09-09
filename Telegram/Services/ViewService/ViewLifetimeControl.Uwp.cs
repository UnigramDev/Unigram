//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Navigation;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace Telegram.Services
{
    // A custom event that fires whenever the secondary view is ready to be closed. You should
    // clean up any state (including deregistering for events) then close the window in this handler
    public delegate void ViewReleasedHandler(object sender, EventArgs e);

    // A ViewLifetimeControl is instantiated for every secondary view. ViewLifetimeControl's reference count
    // keeps track of when the secondary view thinks it's in use and when the main view is interacting with the secondary view (about to show
    // it to the user, etc.) When the reference count drops to zero, the secondary view is closed.
    public sealed partial class ViewLifetimeControl
    {
        private readonly object syncObject = new();

        // This class uses references counts to make sure the secondary views isn't closed prematurely.
        // Whenever the main view is about to interact with the secondary view, it should take a reference
        // by calling "StartViewInUse" on this object. When finished interacting, it should release the reference
        // by calling "StopViewInUse"
        private int refCount;

        // Tracks if this ViewLifetimeControl object is still valid. If this is true, then the view is in the process
        // of closing itself down
        private bool released;

        // Used to store pubicly registered events under the protection of a lock
        private event ViewReleasedHandler InternalReleased;

        public Window Window { get; }

        /// <summary>
        /// One per secondary view, created by that view's <see cref="WindowContext"/> and by
        /// nothing else - the sample this comes from says "only do this once per view", and the
        /// lookup that replaced it let any caller mint a control for a view that had never taken
        /// the baseline reference, whose first consolidation then drove the count to -1.
        /// </summary>
        internal ViewLifetimeControl(Window window)
        {
            Window = window;

            // Taken here rather than by the caller, so a control cannot exist without it. The
            // window drops it again when the view consolidates - this class does not watch for
            // that itself, so that the one Consolidated handler can order the two.
            StartViewInUse();
        }

        // Called when a view has been "consolidated" (no longer accessible to the user)
        // and no other view is trying to interact with it. Raising Released is all this does:
        // the handler releases what the view owns and closes last, as the MSDN sample has it.
        // Closing here put the apartment teardown ahead of every release, and an RCW finalized
        // after that point faults - see WindowContext.OnViewReleased.
        private void FinalizeRelease()
        {
            bool justReleased = false;
            lock (syncObject)
            {
                // Redundant posts are expected - every return to zero queues one - so the guard
                // is what stops a second one closing an already closed window.
                if (!released && refCount == 0)
                {
                    justReleased = true;
                    released = true;
                }
            }

            // This assumes that released will never be made false after it
            // it has been set to true
            if (justReleased)
            {
                InternalReleased?.Invoke(this, new EventArgs());
            }
        }

        // Signals that the view is being interacted with by another view,
        // so it shouldn't be closed even if it becomes "consolidated"
        public int StartViewInUse()
        {
            bool releasedCopy = false;
            int refCountCopy = 0;

            lock (syncObject)
            {
                releasedCopy = released;
                if (!released)
                {
                    refCountCopy = ++refCount;
                }
            }

            if (releasedCopy)
            {
                return -1;
            }

            return refCountCopy;
        }

        // Should come after any call to StartViewInUse
        // Signals that the another view has finished interacting with the view tracked
        // by this object
        public int StopViewInUse()
        {
            int refCountCopy = 0;
            bool releasedCopy = false;

            lock (syncObject)
            {
                releasedCopy = released;
                if (!released)
                {
                    refCountCopy = --refCount;
                    if (refCountCopy == 0)
                    {
                        // If no other view is interacting with this view, and
                        // the view isn't accessible to the user, it's appropriate
                        // to close it
                        //
                        // Before actually closing the view, make sure there are no
                        // other important events waiting in the queue (this low-priority item
                        // will run after other events
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        Window.Dispatcher.RunAsync(CoreDispatcherPriority.Low, FinalizeRelease);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    }
                }
            }

            if (releasedCopy)
            {
                return -1;
            }

            return refCountCopy;
        }

        // Signals to consumers that its time to close the view so that
        // they can clean up (including calling Window.Close() when finished)
        public event ViewReleasedHandler Released
        {
            add
            {
                bool releasedCopy;
                lock (syncObject)
                {
                    releasedCopy = released;
                    if (!released)
                    {
                        InternalReleased += value;
                    }
                }
            }
            remove
            {
                lock (syncObject)
                {
                    InternalReleased -= value;
                }
            }
        }
    }
}
