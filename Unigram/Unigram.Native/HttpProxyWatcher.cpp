#include "pch.h"
#include "HttpProxyWatcher.h"
#if __has_include("HttpProxyWatcher.g.cpp")
#include "HttpProxyWatcher.g.cpp"
#endif

#include "Helpers/LibraryHelper.h"

typedef
LSTATUS
(APIENTRY*
	pRegOpenKeyExW)(
		_In_ HKEY hKey,
		_In_opt_ LPCWSTR lpSubKey,
		_In_opt_ DWORD ulOptions,
		_In_ REGSAM samDesired,
		_Out_ PHKEY phkResult
		);

typedef
LSTATUS
(APIENTRY*
	pRegNotifyChangeKeyValue)(
		_In_ HKEY hKey,
		_In_ BOOL bWatchSubtree,
		_In_ DWORD dwNotifyFilter,
		_In_opt_ HANDLE hEvent,
		_In_ BOOL fAsynchronous
		);

typedef
LSTATUS
(APIENTRY*
	pRegGetValueW)(
		_In_ HKEY hkey,
		_In_opt_ LPCWSTR lpSubKey,
		_In_opt_ LPCWSTR lpValue,
		_In_ DWORD dwFlags,
		_Out_opt_ LPDWORD pdwType,
		_When_((dwFlags & 0x7F) == RRF_RT_REG_SZ ||
			(dwFlags & 0x7F) == RRF_RT_REG_EXPAND_SZ ||
			(dwFlags & 0x7F) == (RRF_RT_REG_SZ | RRF_RT_REG_EXPAND_SZ) ||
			*pdwType == REG_SZ ||
			*pdwType == REG_EXPAND_SZ, _Post_z_)
		_When_((dwFlags & 0x7F) == RRF_RT_REG_MULTI_SZ ||
			*pdwType == REG_MULTI_SZ, _Post_ _NullNull_terminated_)
		_Out_writes_bytes_to_opt_(*pcbData, *pcbData) PVOID pvData,
		_Inout_opt_ LPDWORD pcbData
		);

typedef
LSTATUS
(APIENTRY*
	pRegCloseKey)(
		_In_ HKEY hKey
		);

namespace winrt::Unigram::Native::implementation
{
	critical_section HttpProxyWatcher::s_criticalSection;
	winrt::com_ptr<HttpProxyWatcher> HttpProxyWatcher::s_current{ nullptr };

	HttpProxyWatcher::HttpProxyWatcher() {
		static const LibraryInstance advapi32(L"advapi32.dll");
		static const auto regOpenKeyEx = advapi32.GetMethod<pRegOpenKeyExW>("RegOpenKeyExW");
		static const auto regCloseKey = advapi32.GetMethod<pRegCloseKey>("RegCloseKey");

		LSTATUS status;
		HKEY internetSettings;
		status = regOpenKeyEx(HKEY_CURRENT_USER, L"Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings", 0, STANDARD_RIGHTS_READ | KEY_QUERY_VALUE | KEY_NOTIFY, &internetSettings);
		UpdateValues(internetSettings, false);
		status = regCloseKey(internetSettings);
	}

	void HttpProxyWatcher::ThreadLoop(HttpProxyWatcher* watcher) {
		static const LibraryInstance advapi32(L"advapi32.dll");
		static const auto regOpenKeyEx = advapi32.GetMethod<pRegOpenKeyExW>("RegOpenKeyExW");
		static const auto regNotifyChangeKeyValue = advapi32.GetMethod<pRegNotifyChangeKeyValue>("RegNotifyChangeKeyValue");
		static const auto regCloseKey = advapi32.GetMethod<pRegCloseKey>("RegCloseKey");

		LSTATUS status;
		HKEY internetSettings;
		status = regOpenKeyEx(HKEY_CURRENT_USER, L"Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings", 0, STANDARD_RIGHTS_READ | KEY_QUERY_VALUE | KEY_NOTIFY, &internetSettings);

		HANDLE hEvent = CreateEvent(NULL, FALSE, FALSE, NULL);
		HANDLE waitArray[2] = { watcher->m_shutdownEvent, hEvent };

		bool keepWatching = true;

		while (keepWatching) {
			DWORD waitResult = WaitForSingleObject(watcher->m_shutdownEvent, 0);

			switch (waitResult)
			{
			case WAIT_OBJECT_0 + 0:  // m_shutdownEvent
				keepWatching = false;
				break;
			case WAIT_TIMEOUT:  // hEvent
				status = regNotifyChangeKeyValue(internetSettings, false, REG_NOTIFY_CHANGE_LAST_SET, hEvent, true);
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

		regCloseKey(internetSettings);
		CloseHandle(hEvent);
	}

	void HttpProxyWatcher::UpdateValues(HKEY internetSettings, bool notify)
	{
		static const LibraryInstance advapi32(L"advapi32.dll");
		static const auto regGetValue = advapi32.GetMethod<pRegGetValueW>("RegGetValueW");

		LSTATUS status;
		DWORD proxyServerSize = 0;
		status = regGetValue(internetSettings, NULL, L"ProxyServer", RRF_RT_REG_SZ, NULL, NULL, &proxyServerSize);

		DWORD bufferLength = proxyServerSize / sizeof(WCHAR);
		WCHAR* const proxyServer = new WCHAR[bufferLength];
		status = regGetValue(internetSettings, NULL, L"ProxyServer", RRF_RT_REG_SZ, NULL, proxyServer, &proxyServerSize);

		DWORD proxyEnableSize = sizeof(DWORD);
		DWORD proxyEnable = 0;
		status = regGetValue(internetSettings, NULL, L"ProxyEnable", RRF_RT_REG_DWORD, NULL, &proxyEnable, &proxyEnableSize);

		for (int i = 0; i < bufferLength; i++) {
			if (proxyServer[i] == '\0') {
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
