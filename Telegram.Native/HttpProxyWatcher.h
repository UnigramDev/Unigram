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
            auto lock = critical_section::scoped_lock(s_criticalSection);

            if (s_current == nullptr)
            {
                s_current = winrt::make_self<HttpProxyWatcher>();
            }

            return s_current.as<winrt::Telegram::Native::HttpProxyWatcher>();
        }

        HttpProxyWatcher();

        void Close()
        {
            SetEvent(m_shutdownEvent);
            m_thread.join();

            CloseHandle(m_shutdownEvent);
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
                SetEvent(m_shutdownEvent);
                m_thread.join();
            }
        }

    private:
        static critical_section s_criticalSection;
        static winrt::com_ptr<HttpProxyWatcher> s_current;

        static void ThreadLoop(HttpProxyWatcher* watcher);
        void UpdateValues(HKEY internetSettings, bool notify);

        std::thread m_thread;
        HANDLE m_shutdownEvent;

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
