/****************************************************************************
*                                                                           *
*                 This file is part of InternalsRT project                  *
*                                                                           *
****************************************************************************/

#include "pch.h"

#include "InternalsRT/CoreWindowHelpers.h"
#include "detours.h"

#include "winrt/Windows.Graphics.Display.h"

using namespace Microsoft::WRL;
using namespace InternalsRT::Core::Windowing;

thread_local bool CoreWindowHelpersClassics::OverrideDpiForCurrentThread = false;
thread_local float CoreWindowHelpersClassics::DpiForCurrentThread = 96.0f;
thread_local bool CoreWindowHelpersClassics::ThreadHasNoXamlMsgWindow = false;
thread_local HWND CoreWindowHelpersClassics::XamlMessageWindow = NULL;

bool CoreWindowHelpersClassics::DpiFunctionsHooked = false;

decltype(&CoreWindowHelpers::GetScaleFactorForDeviceHook) CoreWindowHelpers::GetScaleFactorForDeviceOriginal = nullptr;
decltype(&CoreWindowHelpers::GetScaleFactorForMonitorHook) CoreWindowHelpers::GetScaleFactorForMonitorOriginal = nullptr;
decltype(&CoreWindowHelpers::GetScaleFactorForWindowHook) CoreWindowHelpers::GetScaleFactorForWindowOriginal = nullptr;

decltype(&CoreWindowHelpers::GetDpiForSystemHook) CoreWindowHelpers::GetDpiForSystemOriginal = nullptr;
decltype(&CoreWindowHelpers::GetDpiForWindowHook) CoreWindowHelpers::GetDpiForWindowOriginal = nullptr;

CoreWindowHelpers::PostMessage_t CoreWindowHelpers::PostMessage = nullptr;
CoreWindowHelpers::FindWindowEx_t CoreWindowHelpers::FindWindowEx = nullptr;
CoreWindowHelpers::GetWindowThreadProcessId_t CoreWindowHelpers::GetWindowThreadProcessId = nullptr;

MIDL_INTERFACE("45D64A29-A63E-4CB6-B498-5781D298CB4F")
ICoreWindowInteropLocal : public IUnknown
{
public:
	virtual STDMETHODIMP get_WindowHandle(HWND * hwnd) PURE;
	virtual STDMETHODIMP put_MessageHandled(boolean value) PURE;

	inline HWND GetWindowHandle() { HWND hwnd; get_WindowHandle(&hwnd); return hwnd; }
	__declspec(property(get = GetWindowHandle)) HWND WindowHandle;
	__declspec(property(put = put_MessageHandled)) boolean MessageHandled;
};

uint64_t CoreWindowHelpers::GetHwnd(CoreWindow window)
{
	return (uint64_t)window.as<ICoreWindowInteropLocal>()->WindowHandle;
}

uint64_t CoreWindowHelpers::GetHwndForCurrentThread()
{
	return CoreWindowHelpers::GetHwnd(CoreWindow::GetForCurrentThread());
}

WindowId CoreWindowHelpers::GetWindowId(CoreWindow window)
{
	return { CoreWindowHelpers::GetHwnd(window) };
}

WindowId CoreWindowHelpers::GetWindowIdForCurrentThread()
{
	return { CoreWindowHelpers::GetHwnd(CoreWindow::GetForCurrentThread()) };
}

void CoreWindowHelpers::OverrideDpiForCurrentThread(float dpi)
{
	CoreWindowHelpersClassics::DpiForCurrentThread = dpi;
	CoreWindowHelpersClassics::OverrideDpiForCurrentThread = true;

	if (!CoreWindowHelpersClassics::DpiFunctionsHooked)
	{
		// TODO: Error handling

		HMODULE shcore = GetModuleHandle(L"SHCore.dll");
		if (!shcore) shcore = LoadLibrary(L"SHCore.dll");

		HMODULE user32 = GetModuleHandle(L"User32.dll");
		if (!user32) user32 = LoadLibrary(L"User32.dll");

		GetScaleFactorForDeviceOriginal = (decltype(&GetScaleFactorForDeviceHook))GetProcAddress(shcore, "GetScaleFactorForDevice");
		GetScaleFactorForMonitorOriginal = (decltype(&GetScaleFactorForMonitorHook))GetProcAddress(shcore, "GetScaleFactorForMonitor");
		GetScaleFactorForWindowOriginal = (decltype(&GetScaleFactorForWindowHook))GetProcAddress(shcore, MAKEINTRESOURCEA(244));

		GetDpiForSystemOriginal = (decltype(&GetDpiForSystemHook))GetProcAddress(user32, "GetDpiForSystem");
		GetDpiForWindowOriginal = (decltype(&GetDpiForWindowHook))GetProcAddress(user32, "GetDpiForWindow");

		DetourTransactionBegin();
		DetourUpdateThread(GetCurrentThread());

		DetourAttach(&(PVOID&)CoreWindowHelpers::GetScaleFactorForDeviceOriginal, CoreWindowHelpers::GetScaleFactorForDeviceHook);
		DetourAttach(&(PVOID&)CoreWindowHelpers::GetScaleFactorForMonitorOriginal, CoreWindowHelpers::GetScaleFactorForMonitorHook);
		DetourAttach(&(PVOID&)CoreWindowHelpers::GetScaleFactorForWindowOriginal, CoreWindowHelpers::GetScaleFactorForWindowHook);

		DetourAttach(&(PVOID&)CoreWindowHelpers::GetDpiForSystemOriginal, CoreWindowHelpers::GetDpiForSystemHook);
		DetourAttach(&(PVOID&)CoreWindowHelpers::GetDpiForWindowOriginal, CoreWindowHelpers::GetDpiForWindowHook);

		DetourTransactionCommit();

		CoreWindowHelpersClassics::DpiFunctionsHooked = true;

		PostMessage = (PostMessage_t)GetProcAddress(user32, "PostMessageW");
		FindWindowEx = (FindWindowEx_t)GetProcAddress(user32, "FindWindowExW");
		GetWindowThreadProcessId = (GetWindowThreadProcessId_t)GetProcAddress(user32, "GetWindowThreadProcessId");
	}

	PostMessage((HWND)GetHwndForCurrentThread(), CoreWindowHelpersClassics::WM_COREWINDOW_SCALECHANGED, NULL, NULL);

	if (!CoreWindowHelpersClassics::XamlMessageWindow && !CoreWindowHelpersClassics::ThreadHasNoXamlMsgWindow)
	{
		auto pid = GetCurrentProcessId();

		HWND msgWindow = FindWindowEx(HWND_MESSAGE, NULL, L"XAMLMessageWindowClass", NULL);
		while (msgWindow)
		{
			DWORD _pid = NULL;
			GetWindowThreadProcessId(msgWindow, &_pid);

			if (_pid == pid)
			{
				CoreWindowHelpersClassics::XamlMessageWindow = msgWindow;
				break;
			}

			msgWindow = FindWindowEx(HWND_MESSAGE, msgWindow, L"XAMLMessageWindowClass", NULL);
		}

		if (!CoreWindowHelpersClassics::XamlMessageWindow)
			CoreWindowHelpersClassics::ThreadHasNoXamlMsgWindow = true;
	}

	if (CoreWindowHelpersClassics::XamlMessageWindow)
		PostMessage(CoreWindowHelpersClassics::XamlMessageWindow, CoreWindowHelpersClassics::WM_XAML_SCALECHANGED, NULL, NULL);
}

float CoreWindowHelpers::GetDpiForCurrentThread()
{
	if (GetDpiForWindowOriginal)
		return GetDpiForWindowOriginal((HWND)GetHwndForCurrentThread());

	return winrt::Windows::Graphics::Display::DisplayInformation::GetForCurrentView().RawPixelsPerViewPixel() * 100 * 96.0f / 100.0f;
}

DEVICE_SCALE_FACTOR CoreWindowHelpers::GetScaleFactorForDeviceHook(DISPLAY_DEVICE_TYPE deviceType)
{
	if (CoreWindowHelpersClassics::OverrideDpiForCurrentThread)
		return DpiToScaleFactor(CoreWindowHelpersClassics::DpiForCurrentThread);

	if (GetScaleFactorForDeviceOriginal)
		return GetScaleFactorForDeviceOriginal(deviceType);

	return DEVICE_SCALE_FACTOR::SCALE_100_PERCENT;
}

HRESULT CoreWindowHelpers::GetScaleFactorForMonitorHook(HMONITOR hMon, DEVICE_SCALE_FACTOR* pScale)
{
	if (CoreWindowHelpersClassics::OverrideDpiForCurrentThread)
		*pScale = DpiToScaleFactor(CoreWindowHelpersClassics::DpiForCurrentThread);
	else
	{

		if (GetScaleFactorForMonitorOriginal)
			return GetScaleFactorForMonitorOriginal(hMon, pScale);

		*pScale = DEVICE_SCALE_FACTOR::SCALE_100_PERCENT;
	}

	return S_OK;
}

HRESULT CoreWindowHelpers::GetScaleFactorForWindowHook(HWND hWnd, DEVICE_SCALE_FACTOR* pScale)
{
	if (CoreWindowHelpersClassics::OverrideDpiForCurrentThread)
		*pScale = DpiToScaleFactor(CoreWindowHelpersClassics::DpiForCurrentThread);
	else
	{

		if (GetScaleFactorForWindowOriginal)
			return GetScaleFactorForWindowOriginal(hWnd, pScale);

		*pScale = DEVICE_SCALE_FACTOR::SCALE_100_PERCENT;
	}

	return S_OK;
}

UINT CoreWindowHelpers::GetDpiForSystemHook()
{
	if (CoreWindowHelpersClassics::OverrideDpiForCurrentThread)
		return CoreWindowHelpersClassics::DpiForCurrentThread;

	if (GetDpiForSystemOriginal)
		return GetDpiForSystemOriginal();

	return 96;
}

UINT CoreWindowHelpers::GetDpiForWindowHook(HWND hwnd)
{
	if (CoreWindowHelpersClassics::OverrideDpiForCurrentThread)
		return CoreWindowHelpersClassics::DpiForCurrentThread;

	if (GetDpiForWindowOriginal)
		return GetDpiForWindowOriginal(hwnd);

	return 96;
}
