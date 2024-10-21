#include "pch.h"
#include "WindowVisual.h"
#include "Composition/WindowVisual.g.cpp"

#include "winrt/Windows.UI.Core.h"
#include "winrt/Windows.UI.Xaml.h"

using namespace winrt::Windows::UI;
using namespace winrt::Windows::UI::Composition;
using namespace winrt::Windows::UI::Core;
using namespace winrt::Windows::UI::Xaml;

#define GW_OWNER            4
#define WS_EX_TOOLWINDOW    0x00000080L
#define DWMWA_CLOAKED       14

namespace winrt::Telegram::Native::Composition::implementation
{

    inline static HMODULE GetUser32()
    {
        const static auto user32 = LoadLibrary(L"user32.dll");
        return user32;
    }

    inline static HMODULE GetDwmApi()
    {
        const static auto dwmapi = LoadLibrary(L"dwmapi.dll");
        return dwmapi;
    }

    inline static bool IsWindowOwnedByCurrentProcess(HWND hwnd)
    {
        const static auto GetWindowThreadProcessId = (pGetWindowThreadProcessId)GetProcAddress(GetUser32(), "GetWindowThreadProcessId");

        DWORD process_id;
        GetWindowThreadProcessId(hwnd, &process_id);
        return process_id == GetCurrentProcessId();
    }

    inline static bool IsWindowResponding(HWND window)
    {
        const static auto SendMessageTimeout = (pSendMessageTimeoutW)GetProcAddress(GetUser32(), "SendMessageTimeoutW");

        // 50ms is chosen in case the system is under heavy load, but it's also not
        // too long to delay window enumeration considerably.
        const UINT uTimeoutMs = 50;
        return SendMessageTimeout(window, WM_NULL, 0, 0, SMTO_ABORTIFHUNG, uTimeoutMs,
            nullptr);
    }

    // A cloaked window is composited but not visible to the user.
    // Example: Cortana or the Action Center when collapsed.
    inline static bool IsWindowCloaked(HWND hwnd)
    {
        const static auto DwmGetWindowAttribute = (pDwmGetWindowAttribute)GetProcAddress(GetDwmApi(), "DwmGetWindowAttribute");

        int res = 0;
        if (DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, &res, sizeof(res)) != 0)
        {
            // Cannot tell so assume not cloaked for backward compatibility.
            return false;
        }

        return res;
    }

    inline static bool IsWindowValidAndVisible(HWND window)
    {
        const static auto IsWindow = (pIsWindow)GetProcAddress(GetUser32(), "IsWindow");
        const static auto IsWindowVisible = (pIsWindowVisible)GetProcAddress(GetUser32(), "IsWindowVisible");
        const static auto IsIconic = (pIsIconic)GetProcAddress(GetUser32(), "IsIconic");

        return IsWindow(window) && IsWindowVisible(window) && !IsIconic(window);
    }

    inline static bool IsWindowVisibleOnCurrentDesktop(HWND window)
    {
        return IsWindowValidAndVisible(window) && /*IsWindowOnCurrentDesktop(manager, hwnd) &&*/ !IsWindowCloaked(window);
    }

    bool WindowVisual::IsValid(WindowId windowId, hstring& title)
    {
        const static auto IsWindowVisible = (pIsWindowVisible)GetProcAddress(GetUser32(), "IsWindowVisible");
        const static auto IsWindow = (pIsWindow)GetProcAddress(GetUser32(), "IsWindow");
        const static auto IsIconic = (pIsIconic)GetProcAddress(GetUser32(), "IsIconic");
        const static auto GetWindow = (pGetWindow)GetProcAddress(GetUser32(), "GetWindow");
        const static auto GetWindowLong = (pGetWindowLongW)GetProcAddress(GetUser32(), "GetWindowLongW");
        const static auto GetWindowText = (pGetWindowTextW)GetProcAddress(GetUser32(), "GetWindowTextW");
        const static auto GetWindowTextLength = (pGetWindowTextLengthW)GetProcAddress(GetUser32(), "GetWindowTextLengthW");
        const static auto GetClassName = (pGetClassNameW)GetProcAddress(GetUser32(), "GetClassNameW");

        HWND hwnd = (HWND)windowId.Value;

        // Skip invisible and minimized windows
        if (!IsWindowVisible(hwnd) || IsIconic(hwnd))
        {
            return false;
        }

        // Skip windows which are not presented in the taskbar,
        // namely owned window if they don't have the app window style set
        HWND owner = GetWindow(hwnd, GW_OWNER);
        LONG exstyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        if (owner && !(exstyle & WS_EX_APPWINDOW))
        {
            return false;
        }

        // Filter out windows that match the extended styles the caller has specified,
        // e.g. WS_EX_TOOLWINDOW for capturers that don't support overlay windows.
        if (exstyle & WS_EX_TOOLWINDOW)
        {
            return false;
        }

        if (/*params->ignore_unresponsive &&*/ !IsWindowResponding(hwnd))
        {
            return false;
        }

        // GetWindowText* are potentially blocking operations if `hwnd` is
        // owned by the current process. The APIs will send messages to the window's
        // message loop, and if the message loop is waiting on this operation we will
        // enter a deadlock.
        // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowtexta#remarks
        //
        // To help consumers avoid this, there is a DesktopCaptureOption to ignore
        // windows owned by the current process. Consumers should either ensure that
        // the thread running their message loop never waits on this operation, or use
        // the option to exclude these windows from the source list.
        bool owned_by_current_process = IsWindowOwnedByCurrentProcess(hwnd);
        //if (owned_by_current_process && params->ignore_current_process_windows)
        //{
        //    return TRUE;
        //}

        // Even if consumers request to enumerate windows owned by the current
        // process, we should not call GetWindowText* on unresponsive windows owned by
        // the current process because we will hang. Unfortunately, we could still
        // hang if the window becomes unresponsive after this check, hence the option
        // to avoid these completely.
        if (!owned_by_current_process || IsWindowResponding(hwnd))
        {
            const size_t kTitleLength = 500;
            WCHAR window_title[kTitleLength] = L"";
            if (GetWindowTextLength(hwnd) != 0 &&
                GetWindowText(hwnd, window_title, kTitleLength) > 0)
            {
                title = winrt::to_hstring(window_title);
            }
        }

        // Skip windows when we failed to convert the title or it is empty.
        if (/*params->ignore_untitled &&*/ title.empty())
            return false;

        // Capture the window class name, to allow specific window classes to be
        // skipped.
        //
        // https://docs.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-wndclassa
        // says lpszClassName field in WNDCLASS is limited by 256 symbols, so we don't
        // need to have a buffer bigger than that.
        const size_t kMaxClassNameLength = 256;
        WCHAR class_name[kMaxClassNameLength] = L"";
        const int class_name_length =
            GetClassName(hwnd, class_name, kMaxClassNameLength);
        if (class_name_length < 1)
            return false;

        // Skip Program Manager window.
        if (wcscmp(class_name, L"Progman") == 0)
            return false;

        // Skip Input stuff.
        if (wcscmp(class_name, L"Windows.UI.Core.CoreWindow") == 0)
            return false;

        return IsWindowVisibleOnCurrentDesktop(hwnd);
    }

    uint32_t WindowVisual::GetWindowProcessId(WindowId windowId)
    {
        const static auto GetWindowThreadProcessId = (pGetWindowThreadProcessId)GetProcAddress(GetUser32(), "GetWindowThreadProcessId");

        DWORD process_id;
        GetWindowThreadProcessId((HWND)windowId.Value, &process_id);
        return process_id;
    }

    WindowId WindowVisual::GetCurrentWindowId()
    {
        const static auto GetParent = (pGetParent)GetProcAddress(GetUser32(), "GetParent");

        CoreWindow coreWnd = Window::Current().CoreWindow();
        winrt::com_ptr<ICoreWindowInterop> interop = coreWnd.as<ICoreWindowInterop>();

        HWND destination;
        interop->get_WindowHandle(&destination);

        WindowId windowId;
        windowId.Value = (uint64_t)GetParent(destination);
        return windowId;
    }

    winrt::Telegram::Native::Composition::WindowVisual WindowVisual::Create(WindowId windowId)
    {
        const static auto lDwmpQueryWindowThumbnailSourceSize = (DwmpQueryWindowThumbnailSourceSize)GetProcAddress(GetDwmApi(), MAKEINTRESOURCEA(162));
        const static auto lDwmpCreateSharedThumbnailVisual = (DwmpCreateSharedThumbnailVisual)GetProcAddress(GetDwmApi(), MAKEINTRESOURCEA(147));
        const static auto GetParent = (pGetParent)GetProcAddress(GetUser32(), "GetParent");

        Compositor compositor = Window::Current().Compositor();
        CoreWindow coreWnd = Window::Current().CoreWindow();

        winrt::com_ptr<ICoreWindowInterop> interop = coreWnd.as<ICoreWindowInterop>();

        HWND destination;
        HWND source = (HWND)windowId.Value;
        interop->get_WindowHandle(&destination);

        HRESULT result;

        winrt::com_ptr<IDCompositionDevice3> dcompDevice = compositor.as<IDCompositionDevice3>();
        winrt::com_ptr<IDCompositionVisual2> windowVisual;

        float3 size(320, 320, 1);
        DWM_THUMBNAIL_PROPERTIES thumb;
        result = GetScaledWindowSize(destination, &size, thumb);

        HTHUMBNAIL thumbnail;
        result = lDwmpCreateSharedThumbnailVisual(GetParent(destination), source, 2, &thumb, dcompDevice.get(), windowVisual.put_void(), &thumbnail);

        if (result != S_OK)
        {
            return nullptr;
        }

        auto visual = windowVisual.as<Visual>();
        auto window = winrt::make_self<WindowVisual>(source, thumbnail, visual, size);

        return window.as<winrt::Telegram::Native::Composition::WindowVisual>();
    }

    WindowVisual::WindowVisual(HWND window, HTHUMBNAIL thumbnail, Visual visual, float3 size)
        : m_window(window)
        , m_thumbnail(thumbnail)
        , m_visual(visual)
        , m_size(float2(size.x, size.y))
    {
        m_visual.Scale(float3(size.z));
    }

    WindowVisual::~WindowVisual()
    {
        if (m_thumbnail)
        {
            const static auto lDwmUnregisterThumbnail = (DwmUnregisterThumbnail)GetProcAddress(GetDwmApi(), "DwmUnregisterThumbnail");

            lDwmUnregisterThumbnail(m_thumbnail);
            m_thumbnail = NULL;
        }
    }

    Visual WindowVisual::Child()
    {
        return m_visual;
    }

    float2 WindowVisual::Size()
    {
        return m_size;
    }

    void WindowVisual::Size(float2 value)
    {
        const static auto lDwmUpdateThumbnailProperties = (DwmUpdateThumbnailProperties)GetProcAddress(GetDwmApi(), "DwmUpdateThumbnailProperties");

        float3 size(value.x, value.y, 1);

        DWM_THUMBNAIL_PROPERTIES thumb;
        HRESULT result = GetScaledWindowSize(m_window, &size, thumb);
        result = lDwmUpdateThumbnailProperties(m_thumbnail, &thumb);

        if (result == S_OK)
        {
            m_size = float2(size.x, size.y);
            m_visual.Offset(float3((value - m_size) / 2, 0));
            m_visual.Scale(float3(size.z));
        }
    }

    HRESULT WindowVisual::GetScaledWindowSize(HWND window, float3* size, DWM_THUMBNAIL_PROPERTIES& thumb)
    {
        const static auto lDwmpQueryWindowThumbnailSourceSize = (DwmpQueryWindowThumbnailSourceSize)GetProcAddress(GetDwmApi(), MAKEINTRESOURCEA(162));

        SIZE windowSize{};
        HRESULT result = lDwmpQueryWindowThumbnailSourceSize(window, false, &windowSize);

        if (result != S_OK || (windowSize.cx == 0 && windowSize.cy == 0))
        {
            return result;
        }

        UIElement content = Window::Current().Content();
        if (content.XamlRoot())
        {
            size->z = content.XamlRoot().RasterizationScale() * 2;
        }

        double ratioX = (size->x * size->z) / windowSize.cx;
        double ratioY = (size->y * size->z) / windowSize.cy;
        double ratio = std::min(ratioX, ratioY);

        long width = windowSize.cx * ratio;
        long height = windowSize.cy * ratio;

        thumb = {};
        thumb.dwFlags = DWM_TNP_VISIBLE | DWM_TNP_RECTDESTINATION | DWM_TNP_OPACITY | DWM_TNP_ENABLE3D;
        thumb.opacity = 255;
        thumb.fVisible = TRUE;
        thumb.rcDestination = RECT{ 0, 0, width, height };

        size->z = 1 / size->z;
        size->x = width * size->z;
        size->y = height * size->z;

        return S_OK;
    }
}
