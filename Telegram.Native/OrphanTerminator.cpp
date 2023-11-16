#include "pch.h"
#include "OrphanTerminator.h"
#if __has_include("OrphanTerminator.g.cpp")
#include "OrphanTerminator.g.cpp"
#endif

#include "Helpers\LibraryHelper.h"

typedef
BOOL
(WINAPI*
    pTerminateThread)(
        _In_ HANDLE hThread,
        _In_ DWORD dwExitCode
        );

typedef
BOOL
(WINAPI*
    pGetExitCodeThread)(
        _In_ HANDLE hThread,
        _Out_ LPDWORD lpExitCode
        );

namespace winrt::Telegram::Native::implementation
{
    std::mutex OrphanTerminator::s_currentLock;
    winrt::com_ptr<OrphanTerminator> OrphanTerminator::s_current{ nullptr };

    void OrphanTerminator::StartImpl()
    {
        if (m_shutdownEvent == INVALID_HANDLE_VALUE)
        {
            m_shutdownEvent = CreateEvent(NULL, FALSE, FALSE, NULL);
            m_detachedEvent = CreateEvent(NULL, FALSE, FALSE, NULL);
            m_thread = std::thread(ThreadLoop, this);
        }
    }

    void OrphanTerminator::StopImpl()
    {
        if (m_shutdownEvent != INVALID_HANDLE_VALUE)
        {
            SetEvent(m_shutdownEvent);
            m_thread.join();

            CloseHandle(m_shutdownEvent);
            CloseHandle(m_detachedEvent);

            m_shutdownEvent = INVALID_HANDLE_VALUE;
            m_detachedEvent = INVALID_HANDLE_VALUE;
        }
    }

    void OrphanTerminator::DetachingThreadImpl()
    {
        SetEvent(m_detachedEvent);
    }

    void OrphanTerminator::ShutdownCompletedImpl()
    {
        std::lock_guard const guard(m_orphansLock);
        m_orphans.push(GetCurrentThreadId());
    }

    void OrphanTerminator::ThreadLoop(OrphanTerminator* watcher)
    {
        SetThreadDescription(GetCurrentThread(), L"OrphanTerminator");

        HANDLE hEvent = watcher->m_detachedEvent;
        HANDLE waitArray[2] = { watcher->m_shutdownEvent, hEvent };

        bool keepWatching = true;

        while (keepWatching)
        {
            DWORD waitResult = WaitForSingleObject(watcher->m_shutdownEvent, 0);

            switch (waitResult)
            {
            case WAIT_OBJECT_0 + 0:  // m_shutdownEvent
                keepWatching = false;
                break;
            case WAIT_TIMEOUT:  // hEvent
                waitResult = WaitForMultipleObjects(2, waitArray, false, INFINITE);

                switch (waitResult)
                {
                case WAIT_OBJECT_0 + 0:  // m_shutdownEvent
                    keepWatching = false;
                    break;
                case WAIT_OBJECT_0 + 1:  // hEvent
                    watcher->Terminate();
                    break;
                }
                break;
            }
        }
    }

    void OrphanTerminator::Terminate()
    {
        // Give time to the thread to either exit or get stuck on CoUninitialize
        Sleep(500);

        std::lock_guard const guard(m_orphansLock);

        static const LibraryInstance kernel32(L"kernel32.dll");
        static const auto getExitCodeThread = kernel32.GetMethod<pGetExitCodeThread>("GetExitCodeThread");
        static const auto terminateThread = kernel32.GetMethod<pTerminateThread>("TerminateThread");

        for (; !m_orphans.empty(); m_orphans.pop())
        {
            HANDLE hThread = OpenThread(THREAD_TERMINATE | THREAD_QUERY_LIMITED_INFORMATION, FALSE, m_orphans.front());

            if (hThread == INVALID_HANDLE_VALUE)
            {
                continue;
            }

            DWORD exitCode;
            getExitCodeThread(hThread, &exitCode);

            if (exitCode == STILL_ACTIVE)
            {
                terminateThread(hThread, 0);
            }

            CloseHandle(hThread);
        }
    }
}
