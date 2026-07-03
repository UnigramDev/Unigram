#pragma once

#include "HttpProxyWatcher.g.h"

#include <ppl.h>

using namespace concurrency;

namespace winrt::Telegram::Native::implementation
{
    struct HttpProxyWatcher : HttpProxyWatcherT<HttpProxyWatcher>
    {
        static winrt::Telegram::Native::HttpProxyWatcher Current()
        {
            std::lock_guard const guard(s_criticalSection);

            if (s_current == nullptr)
            {
                s_current = winrt::make_self<HttpProxyWatcher>();
            }

            return s_current.as<winrt::Telegram::Native::HttpProxyWatcher>();
        }

        HttpProxyWatcher();

        void Close()
        {
            StopThread();
        }

        hstring Server()
        {
            return m_server;
        }

        bool IsEnabled()
        {
            return m_isEnabled;
        }

        winrt::event_token Changed(Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::HttpProxyWatcher,
            bool> const& value)
        {
            if (!m_changed)
            {
                m_shutdownEvent = CreateEvent(NULL, FALSE, FALSE, NULL);
                m_thread = std::thread(ThreadLoop, this);
            }

            return m_changed.add(value);
        }

        void Changed(winrt::event_token const& token)
        {
            m_changed.remove(token);

            if (!m_changed)
            {
                StopThread();
            }
        }

    private:
        static std::mutex s_criticalSection;
        static winrt::com_ptr<HttpProxyWatcher> s_current;

        static void ThreadLoop(HttpProxyWatcher* watcher);
        void UpdateValues(HKEY internetSettings, bool notify);

        // Signals the watcher thread to exit, joins it, and releases the shutdown event.
        // Safe to call more than once (Close + last-handler-removed) and when never started.
        void StopThread()
        {
            if (m_thread.joinable())
            {
                SetEvent(m_shutdownEvent);
                m_thread.join();
            }

            if (m_shutdownEvent)
            {
                CloseHandle(m_shutdownEvent);
                m_shutdownEvent = nullptr;
            }
        }

        std::thread m_thread;
        HANDLE m_shutdownEvent{ nullptr };

        hstring m_server;
        bool m_isEnabled;
        winrt::event<Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::HttpProxyWatcher,
            bool>> m_changed;
    };
}

namespace winrt::Telegram::Native::factory_implementation
{
    struct HttpProxyWatcher : HttpProxyWatcherT<HttpProxyWatcher, implementation::HttpProxyWatcher>
    {
    };
}
