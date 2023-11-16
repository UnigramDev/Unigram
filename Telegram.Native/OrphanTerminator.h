#pragma once

#include "OrphanTerminator.g.h"

#include <queue>

namespace winrt::Telegram::Native::implementation
{
    /*
     * How does this work?
     *
     * In some conditions, when closing a secondary view, the corresponding Apartment STA
     * thread fails to exit, by hanging undefinitely on CoUninitialize.
     * While this may not sound like a big issue (apart from possible leaked memory and what not),
     * this causes the application to get completely frozen as soon as either
     * CoreApplication.EnteredBackground or LeavingBackground events get fired.
     * The goal of the following code is to terminate any ASTA thread as soon as it becomes stale.
     * To do this, ShutdownCompleted needs to be called when the secondary view's
     * DispatcherQueue.ShutdownCompleted event is fired, this makes it possible to track the thread ID.
     * Whenever a "DManip Delegate Thread" detaches, we call DetachingThread, this executes a routine
     * that, after sleeping for 500ms (heuristic value) check if any of the monitored threads
     * still exist and terminates them.
     *
     * Reference to the Microsoft issue that will never get addressed: https://github.com/microsoft/microsoft-ui-xaml/issues/802
     *
     */

    struct OrphanTerminator : OrphanTerminatorT<OrphanTerminator>
    {
        static OrphanTerminator* Current()
        {
            std::lock_guard const guard(s_currentLock);

            if (s_current == nullptr)
            {
                s_current = winrt::make_self<OrphanTerminator>();
            }

            return s_current.get();
        }

        OrphanTerminator() = default;

        static void Start()
        {
            Current()->StartImpl();
        }
        static void Stop()
        {
            Current()->StopImpl();
        }

        static void ShutdownCompleted()
        {
            Current()->ShutdownCompletedImpl();
        }
        static void DetachingThread()
        {
            Current()->DetachingThreadImpl();
        }

        void StartImpl();
        void StopImpl();

        void ShutdownCompletedImpl();
        void DetachingThreadImpl();

    private:
        static std::mutex s_currentLock;
        static winrt::com_ptr<OrphanTerminator> s_current;

        std::thread m_thread;
        HANDLE m_shutdownEvent = INVALID_HANDLE_VALUE;
        HANDLE m_detachedEvent = INVALID_HANDLE_VALUE;

        std::mutex m_orphansLock;
        std::queue<DWORD> m_orphans;

        static void ThreadLoop(OrphanTerminator* watcher);
        void Terminate();
    };
}

namespace winrt::Telegram::Native::factory_implementation
{
    struct OrphanTerminator : OrphanTerminatorT<OrphanTerminator, implementation::OrphanTerminator>
    {
    };
}
