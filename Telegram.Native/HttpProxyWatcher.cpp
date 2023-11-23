#include "pch.h"
#include "HttpProxyWatcher.h"
#if __has_include("HttpProxyWatcher.g.cpp")
#include "HttpProxyWatcher.g.cpp"
#endif

namespace winrt::Telegram::Native::implementation
{
    critical_section HttpProxyWatcher::s_criticalSection;
    winrt::com_ptr<HttpProxyWatcher> HttpProxyWatcher::s_current{ nullptr };

    HttpProxyWatcher::HttpProxyWatcher()
    {
        LSTATUS status;
        HKEY internetSettings;
        status = RegOpenKeyEx(HKEY_CURRENT_USER, L"Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings", 0, STANDARD_RIGHTS_READ | KEY_QUERY_VALUE | KEY_NOTIFY, &internetSettings);
        UpdateValues(internetSettings, false);
        status = RegCloseKey(internetSettings);
    }

    void HttpProxyWatcher::ThreadLoop(HttpProxyWatcher* watcher)
    {
        SetThreadDescription(GetCurrentThread(), L"HttpProxyWatcher");

        LSTATUS status;
        HKEY internetSettings;
        status = RegOpenKeyEx(HKEY_CURRENT_USER, L"Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings", 0, STANDARD_RIGHTS_READ | KEY_QUERY_VALUE | KEY_NOTIFY, &internetSettings);

        HANDLE hEvent = CreateEvent(NULL, FALSE, FALSE, NULL);
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
                status = RegNotifyChangeKeyValue(internetSettings, false, REG_NOTIFY_CHANGE_LAST_SET, hEvent, true);
                waitResult = WaitForMultipleObjects(2, waitArray, false, INFINITE);

                switch (waitResult)
                {
                case WAIT_OBJECT_0 + 0:  // m_shutdownEvent
                    keepWatching = false;
                    break;
                case WAIT_OBJECT_0 + 1:  // hEvent
                    watcher->UpdateValues(internetSettings, true);
                    break;
                }
                break;
            }
        }

        RegCloseKey(internetSettings);
        CloseHandle(hEvent);
    }

    void HttpProxyWatcher::UpdateValues(HKEY internetSettings, bool notify)
    {
        LSTATUS status;
        DWORD proxyServerSize = 0;
        status = RegGetValue(internetSettings, NULL, L"ProxyServer", RRF_RT_REG_SZ, NULL, NULL, &proxyServerSize);

        DWORD bufferLength = proxyServerSize / sizeof(WCHAR);
        WCHAR* const proxyServer = new WCHAR[bufferLength];
        status = RegGetValue(internetSettings, NULL, L"ProxyServer", RRF_RT_REG_SZ, NULL, proxyServer, &proxyServerSize);

        DWORD proxyEnableSize = sizeof(DWORD);
        DWORD proxyEnable = 0;
        status = RegGetValue(internetSettings, NULL, L"ProxyEnable", RRF_RT_REG_DWORD, NULL, &proxyEnable, &proxyEnableSize);

        for (int i = 0; i < bufferLength; i++)
        {
            if (proxyServer[i] == '\0')
            {
                bufferLength = i;
                break;
            }
        }

        auto server = hstring(proxyServer, bufferLength);
        auto enable = proxyEnable == 1;

        if (m_server != server || m_isEnabled != enable)
        {
            m_server = server;
            m_isEnabled = enable;
            m_changed(*this, enable);
        }
    }
}
