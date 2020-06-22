//********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
//
//*********************************************************

// The objects defined here demonstrate how to make sure each of the views created remains alive as long as 
// the app needs them, but only when they're being used by the app or the user. Many of the scenarios contained in this
// sample use these functions to keep track of the views available and ensure that the view is not closed while
// the scenario is attempting to show it.
//
// As you can see in scenario 1, the ApplicationViewSwitcher.TryShowAsStandaloneAsync and 
// ProjectionManager.StartProjectingAsync methods let you show one view next to another. The Consolidated event
// is fired when a view stops being visible separately from other views. Common cases where this will occur
// is when the view falls out of the list of recently used apps, or when the user performs the close gesture on the view.
// This is a good time to close the view, provided the app isn't trying to show the view at the same time. This event
// is fired on the thread of the view that becomes consolidated.
//
// Each view lives on its own thread, so concurrency control is necessary. Also, as you'll see in the sample,
// certain objects may be bound to UI on given threads. Properties of those objects should only be updated
// on that UI thread.

using System;
using System.Collections.Concurrent;
using Unigram.Navigation;
using Unigram.Navigation.Services;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace Unigram.Services.ViewService
{
    // A custom event that fires whenever the secondary view is ready to be closed. You should
    // clean up any state (including deregistering for events) then close the window in this handler
    public delegate void ViewReleasedHandler(object sender, EventArgs e);

    // A ViewLifetimeControl is instantiated for every secondary view. ViewLifetimeControl's reference count
    // keeps track of when the secondary view thinks it's in use and when the main view is interacting with the secondary view (about to show
    // it to the user, etc.) When the reference count drops to zero, the secondary view is closed.
    public sealed class ViewLifetimeControl
    {
        private static readonly ConcurrentDictionary<int, ViewLifetimeControl> WindowControlsMap = new ConcurrentDictionary<int, ViewLifetimeControl>();

        #region CoreDispatcher
        // Dispatcher for this view. Kept here for sending messages between this view and the main view.
        public CoreDispatcher CoreDispatcher { get; }
        #endregion

        #region Internal tracking fields

        private readonly object syncObject = new object();

        // This class uses references counts to make sure the secondary views isn't closed prematurely.
        // Whenever the main view is about to interact with the secondary view, it should take a reference
        // by calling "StartViewInUse" on this object. When finished interacting, it should release the reference
        // by calling "StopViewInUse"
        private int refCount;

        // Each view has a unique Id, found using the ApplicationView.Id property or
        // ApplicationView.GetApplicationViewIdForCoreWindow method. This id is used in all of the ApplicationViewSwitcher
        // and ProjectionManager APIs. 

        // Tracks if this ViewLifetimeControl object is still valid. If this is true, then the view is in the process
        // of closing itself down
        private bool released;

        private bool isDisposing = false;

        // Used to store pubicly registered events under the protection of a lock
        private event ViewReleasedHandler InternalReleased;
        #endregion

        #region Id
        // Each view has a unique Id, found using the ApplicationView.Id property or
        // ApplicationView.GetApplicationViewIdForCoreWindow method. This id is used in all of the ApplicationViewSwitcher
        // and ProjectionManager APIs. 
        public int Id { get; }
        #endregion

        #region WindowWrapper
        public WindowContext WindowWrapper { get; }
        #endregion

        #region NavigationService
        public INavigationService NavigationService { get; set; }
        #endregion

        private ViewLifetimeControl(CoreWindow newWindow)
        {
            CoreDispatcher = newWindow.Dispatcher;
            WindowWrapper = WindowContext.Current(Window.Current);
            Id = ApplicationView.GetApplicationViewIdForWindow(newWindow);

            // This class will automatically tell the view when its time to close
            // or stay alive in a few cases
            RegisterForEvents();
        }

        private void RegisterForEvents()
        {
            ApplicationView.GetForCurrentView().Consolidated += ViewConsolidated;
        }

        private void UnregisterForEvents()
        {
            try
            {
                ApplicationView.GetForCurrentView().Consolidated -= ViewConsolidated;
            }
            catch { }
        }

        // A view is consolidated with other views hen there's no way for the user to get to it (it's not in the list of recently used apps, cannot be
        // launched from Start, etc.) A view stops being consolidated when it's visible--at that point the user can interact with it, move it on or off screen, etc. 
        // It's generally a good idea to close a view after it has been consolidated, but keep it open while it's visible.
        private void ViewConsolidated(ApplicationView sender, ApplicationViewConsolidatedEventArgs e)
        {
            StopViewInUse();
        }

        // Called when a view has been "consolidated" (no longer accessible to the user) 
        // and no other view is trying to interact with it. This should only be closed after the reference
        // count goes to 0 (including being consolidated). At the end of this, the view should be closed manually. 
        private void FinalizeRelease()
        {
            bool justReleased = false;
            lock (syncObject)
            {
                if (refCount == 0)
                {
                    justReleased = true;
                    released = true;
                }
            }

            // This assumes that released will never be made false after it
            // it has been set to true
            if (justReleased)
            {
                UnregisterForEvents();
                InternalReleased?.Invoke(this, new EventArgs());
                ViewLifetimeControl removed;
                WindowControlsMap.TryRemove(Id, out removed);
            }
        }

        /// <summary>
        /// Retrieves existing or creates new instance of <see cref="ViewLifetimeControl"/> for current <see cref="CoreWindow"/>
        /// </summary>
        /// <returns>Instance of <see cref="ViewLifetimeControl"/> that is associated with current window</returns>
        public static ViewLifetimeControl GetForCurrentView()
        {
            var wnd = Window.Current.CoreWindow;
            /*BUG: use this strange way to get Id as for ShareTarget hosted window on desktop version ApplicationView.GetForCurrentView() throws "Catastrofic failure" COMException.
              Link to question on msdn: https://social.msdn.microsoft.com/Forums/security/en-US/efa50111-043a-4007-8af8-2b53f72ba207/uwp-c-xaml-comexception-catastrofic-failure-due-to-applicationviewgetforcurrentview-in?forum=wpdevelop  */
            return WindowControlsMap.GetOrAdd(ApplicationView.GetApplicationViewIdForWindow(wnd), id => new ViewLifetimeControl(wnd));
        }

        /// <summary>
        /// Tries to retrieve existing instance of <see cref="ViewLifetimeControl"/> for current <see cref="CoreWindow"/>
        /// </summary>
        /// <returns>Instance of <see cref="ViewLifetimeControl"/> that is associated with current window or <value>null</value> if no calls to <see cref="GetForCurrentView"/> were made
        /// before.</returns>
        public static ViewLifetimeControl TryGetForCurrentView()
        {
            ViewLifetimeControl res;
            WindowControlsMap.TryGetValue(ApplicationView.GetApplicationViewIdForWindow(Window.Current.CoreWindow), out res);
            return res;
        }

        // Signals that the view is being interacted with by another view,
        // so it shouldn't be closed even if it becomes "consolidated"
        public int StartViewInUse()
        {
            bool releasedCopy = false;
            int refCountCopy = 0;

            lock (syncObject)
            {
                releasedCopy = this.released;
                if (!released)
                {
                    refCountCopy = ++refCount;
                }
            }
            Logging.LoggingService.WriteLine("Start:" + refCountCopy);
            if (releasedCopy)
            {
                throw new ViewLifeTimeException("This view is being disposed");
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
                releasedCopy = this.released;
                if (!released)
                {
                    refCountCopy = --refCount;
                    if (refCountCopy == 0 && !isDisposing)
                    {
                        // If no other view is interacting with this view, and
                        // the view isn't accessible to the user, it's appropriate
                        // to close it
                        //
                        // Before actually closing the view, make sure there are no
                        // other important events waiting in the queue (this low-priority item
                        // will run after other events
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        CoreDispatcher.RunAsync(CoreDispatcherPriority.Low, FinalizeRelease);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        isDisposing = true;
                    }
                }
            }
            Logging.LoggingService.WriteLine("Stop:" + refCountCopy);
            if (releasedCopy)
            {
                throw new ViewLifeTimeException("This view is being disposed");
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

                if (releasedCopy)
                {
                    throw new ViewLifeTimeException("This view is being disposed");
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

        public class ViewLifeTimeException : Exception
        {
            public ViewLifeTimeException()
            {
            }

            public ViewLifeTimeException(string message) : base(message)
            {
            }

            public ViewLifeTimeException(string message, Exception innerException) : base(message, innerException)
            {
            }
        }
    }
}
