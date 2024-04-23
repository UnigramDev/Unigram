/****************************************************************************
*                                                                           *
*                 This file is part of InternalsRT project                  *
*                                                                           *
****************************************************************************/

#pragma once

#include <Windows.h>
#include <CoreWindow.h>
#include <winrt/Windows.UI.Core.h>

#define WINAPI_PARTITION_DESKTOP TRUE
#include <shellscalingapi.h>
#define WINAPI_PARTITION_DESKTOP FALSE

#include <wrl.h>

using namespace winrt::Windows::UI;
using namespace winrt::Windows::UI::Core;

namespace InternalsRT::Core::Windowing
{
    static class CoreWindowHelpersClassics
    {
    public:
        static thread_local bool OverrideDpiForCurrentThread;
        static thread_local bool ThreadHasNoXamlMsgWindow;
        static thread_local HWND XamlMessageWindow;
        static thread_local float DpiForCurrentThread;

        static bool DpiFunctionsHooked;

        static const UINT WM_COREWINDOW_SCALECHANGED = 0x785u;
        static const UINT WM_XAML_SCALECHANGED = 0x8001u;
        //static constexpr wchar_t* XAML_MESSAGE_WINDOW_CLASS_NAME = L"XAMLMessageWindowClass";
    };

    class CoreWindowHelpers
    {
    private:
        CoreWindowHelpers() { };

        static DEVICE_SCALE_FACTOR GetScaleFactorForDeviceHook(DISPLAY_DEVICE_TYPE deviceType);
        static HRESULT GetScaleFactorForMonitorHook(HMONITOR hMon, DEVICE_SCALE_FACTOR* pScale);
        static HRESULT GetScaleFactorForWindowHook(HWND hWnd, DEVICE_SCALE_FACTOR* pScale);

        static UINT GetDpiForSystemHook();
        static UINT GetDpiForWindowHook(HWND hwnd);

        static decltype(&GetScaleFactorForDeviceHook) GetScaleFactorForDeviceOriginal;
        static decltype(&GetScaleFactorForMonitorHook) GetScaleFactorForMonitorOriginal;
        static decltype(&GetScaleFactorForWindowHook) GetScaleFactorForWindowOriginal;

        static decltype(&GetDpiForSystemHook) GetDpiForSystemOriginal;
        static decltype(&GetDpiForWindowHook) GetDpiForWindowOriginal;

        typedef BOOL(WINAPI* PostMessage_t)(HWND hWnd, UINT Msg, WPARAM wParam, LPARAM lParam);
        typedef HWND(WINAPI* FindWindowEx_t)(HWND hWndParent, HWND hWndChildAfter, LPCWSTR lpszClass, LPCWSTR lpszWindow);
        typedef DWORD(WINAPI* GetWindowThreadProcessId_t)(HWND hWnd, LPDWORD lpdwProcessId);

        static PostMessage_t PostMessage;
        static FindWindowEx_t FindWindowEx;
        static GetWindowThreadProcessId_t GetWindowThreadProcessId;

        static inline DEVICE_SCALE_FACTOR DpiToScaleFactor(float dpi) { return (DEVICE_SCALE_FACTOR)(int)(dpi * 100 / 96.0f); }
    public:
        static uint64_t GetHwnd(CoreWindow window);
        static uint64_t GetHwndForCurrentThread();
        static WindowId GetWindowId(CoreWindow window);
        static WindowId GetWindowIdForCurrentThread();
        static void OverrideDpiForCurrentThread(float dpi);
        static float GetDpiForCurrentThread();
    };
}
