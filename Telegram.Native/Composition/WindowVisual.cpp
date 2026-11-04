#include "pch.h"
#include "WindowVisual.h"
#include "Composition/WindowVisual.g.cpp"

#ifdef WINUI3
#include <MemoryBuffer.h>
#include <cmath>
#endif

using namespace winrt::Windows::UI;
using namespace winrt::UIComposition;

#ifndef GA_ROOT
#define GA_ROOT             2
#endif
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

    WindowId WindowVisual::GetRootWindowId(WindowId windowId)
    {
        const static auto GetAncestor = (pGetAncestor)GetProcAddress(GetUser32(), "GetAncestor");

        WindowId root;
        root.Value = windowId.Value ? (uint64_t)GetAncestor((HWND)windowId.Value, GA_ROOT) : 0;
        return root;
    }

#ifdef WINUI3
    namespace
    {
        struct ThreadDpiAwareness
        {
            DPI_AWARENESS_CONTEXT Previous;

            ThreadDpiAwareness(DPI_AWARENESS_CONTEXT context)
                : Previous(SetThreadDpiAwarenessContext(context))
            {
            }

            ~ThreadDpiAwareness()
            {
                if (Previous)
                {
                    SetThreadDpiAwarenessContext(Previous);
                }
            }
        };

        struct MemoryDC
        {
            HDC Value = CreateCompatibleDC(nullptr);

            ~MemoryDC()
            {
                if (Value)
                {
                    DeleteDC(Value);
                }
            }
        };

        // Top-down 32-bit, so a row of bits is a row of pixels from the top.
        struct DibSection
        {
            HBITMAP Value = nullptr;
            uint32_t* Bits = nullptr;

            DibSection(int width, int height)
            {
                BITMAPINFO info{};
                info.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
                info.bmiHeader.biWidth = width;
                info.bmiHeader.biHeight = -height;
                info.bmiHeader.biPlanes = 1;
                info.bmiHeader.biBitCount = 32;
                info.bmiHeader.biCompression = BI_RGB;

                Value = CreateDIBSection(nullptr, &info, DIB_RGB_COLORS, reinterpret_cast<void**>(&Bits), nullptr, 0);
            }

            ~DibSection()
            {
                if (Value)
                {
                    DeleteObject(Value);
                }
            }
        };

        // Declared after the DC and the bitmap it joins, so it is undone before either is deleted.
        struct Selection
        {
            HDC DC;
            HGDIOBJ Previous;

            Selection(HDC dc, HGDIOBJ value)
                : DC(dc)
                , Previous(SelectObject(dc, value))
            {
            }

            ~Selection()
            {
                SelectObject(DC, Previous);
            }
        };
    }

    winrt::Windows::Graphics::Imaging::SoftwareBitmap WindowVisual::CaptureSnapshot(WindowId windowId, int32_t maxWidth, int32_t maxHeight)
    {
        using namespace winrt::Windows::Graphics::Imaging;

        const static auto DwmGetWindowAttribute = (pDwmGetWindowAttribute)GetProcAddress(GetDwmApi(), "DwmGetWindowAttribute");

        HWND window = (HWND)windowId.Value;

        if (maxWidth <= 0 || maxHeight <= 0 || !IsWindow(window) || IsIconic(window))
        {
            return nullptr;
        }

        // The extended frame bounds are physical pixels whoever asks, the window rect is in the
        // calling thread's awareness: per monitor, both are the pixels PrintWindow draws.
        ThreadDpiAwareness awareness(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

        RECT bounds;
        if (!GetWindowRect(window, &bounds))
        {
            return nullptr;
        }

        const int width = bounds.right - bounds.left;
        const int height = bounds.bottom - bounds.top;

        if (width <= 0 || height <= 0)
        {
            return nullptr;
        }

        // PrintWindow draws the whole window rect, the invisible resize borders around the frame
        // included, and those come out black.
        RECT frame;
        if (DwmGetWindowAttribute(window, DWMWA_EXTENDED_FRAME_BOUNDS, &frame, sizeof(frame)) != S_OK || !IntersectRect(&frame, &frame, &bounds))
        {
            frame = bounds;
        }

        OffsetRect(&frame, -bounds.left, -bounds.top);

        const int frameWidth = frame.right - frame.left;
        const int frameHeight = frame.bottom - frame.top;

        const double scale = std::min({ 1.0, double(maxWidth) / frameWidth, double(maxHeight) / frameHeight });
        const int targetWidth = std::max(1, int(std::lround(frameWidth * scale)));
        const int targetHeight = std::max(1, int(std::lround(frameHeight * scale)));

        MemoryDC sourceDC;
        DibSection source(width, height);

        if (!sourceDC.Value || !source.Value)
        {
            return nullptr;
        }

        Selection sourceSelection(sourceDC.Value, source.Value);

        // The flag is what reaches DirectComposition content, the Windows App SDK's own windows
        // among it: without it they come out blank.
        if (!PrintWindow(window, sourceDC.Value, PW_RENDERFULLCONTENT))
        {
            return nullptr;
        }

        MemoryDC targetDC;
        DibSection target(targetWidth, targetHeight);

        if (!targetDC.Value || !target.Value)
        {
            return nullptr;
        }

        Selection targetSelection(targetDC.Value, target.Value);

        // HALFTONE averages the pixels it drops, where the default mode skips them; it leaves the
        // brush origin undefined until it is set again.
        SetStretchBltMode(targetDC.Value, HALFTONE);
        SetBrushOrgEx(targetDC.Value, 0, 0, nullptr);

        if (!StretchBlt(targetDC.Value, 0, 0, targetWidth, targetHeight, sourceDC.Value, frame.left, frame.top, frameWidth, frameHeight, SRCCOPY))
        {
            return nullptr;
        }

        GdiFlush();

        SoftwareBitmap bitmap(BitmapPixelFormat::Bgra8, targetWidth, targetHeight, BitmapAlphaMode::Premultiplied);

        {
            auto buffer = bitmap.LockBuffer(BitmapBufferAccessMode::Write);
            auto plane = buffer.GetPlaneDescription(0);
            auto reference = buffer.CreateReference();

            uint8_t* data;
            uint32_t capacity;
            winrt::check_hresult(reference.as<::Windows::Foundation::IMemoryBufferByteAccess>()->GetBuffer(&data, &capacity));

            // GDI leaves alpha undefined, and a window is opaque.
            for (int y = 0; y < targetHeight; y++)
            {
                auto row = reinterpret_cast<uint32_t*>(data + plane.StartIndex + y * plane.Stride);
                auto pixels = target.Bits + y * targetWidth;

                for (int x = 0; x < targetWidth; x++)
                {
                    row[x] = pixels[x] | 0xFF000000;
                }
            }

            reference.Close();
            buffer.Close();
        }

        return bitmap;
    }
#endif

    winrt::Telegram::Native::Composition::WindowVisual WindowVisual::Create(WindowId windowId, WindowId destinationId, Compositor compositor, float rasterizationScale)
    {
        const static auto lDwmpCreateSharedThumbnailVisual = (DwmpCreateSharedThumbnailVisual)GetProcAddress(GetDwmApi(), MAKEINTRESOURCEA(147));
        const static auto GetParent = (pGetParent)GetProcAddress(GetUser32(), "GetParent");

        HWND source = (HWND)windowId.Value;
        HWND destination = (HWND)destinationId.Value;

        HRESULT result;

        winrt::com_ptr<IDCompositionDevice3> dcompDevice = compositor.as<IDCompositionDevice3>();
        winrt::com_ptr<IDCompositionVisual2> windowVisual;

        float3 size(320, 320, rasterizationScale);
        DWM_THUMBNAIL_PROPERTIES thumb;
        result = GetScaledWindowSize(destination, &size, thumb);

        HTHUMBNAIL thumbnail;
        result = lDwmpCreateSharedThumbnailVisual(GetParent(destination), source, 2, &thumb, dcompDevice.get(), windowVisual.put_void(), &thumbnail);

        if (result != S_OK)
        {
            return nullptr;
        }

        auto visual = windowVisual.as<Visual>();
        auto window = winrt::make_self<WindowVisual>(source, thumbnail, visual, size, rasterizationScale);

        return window.as<winrt::Telegram::Native::Composition::WindowVisual>();
    }

    WindowVisual::WindowVisual(HWND window, HTHUMBNAIL thumbnail, Visual visual, float3 size, float scale)
        : m_window(window)
        , m_thumbnail(thumbnail)
        , m_visual(visual)
        , m_size(float2(size.x, size.y))
        , m_scale(scale)
    {
        m_visual.Scale(float3(size.z));
    }

    WindowVisual::~WindowVisual()
    {
        if (m_thumbnail)
        {
            const static auto lDwmUnregisterThumbnail = (PFN_DwmUnregisterThumbnail)GetProcAddress(GetDwmApi(), "DwmUnregisterThumbnail");

            lDwmUnregisterThumbnail(m_thumbnail);
            m_thumbnail = NULL;
        }
    }

    Visual WindowVisual::Child()
    {
        return m_visual;
    }

    float3 WindowVisual::Size()
    {
        return float3(m_size, m_scale);
    }

    void WindowVisual::Size(float3 value)
    {
        const static auto lDwmUpdateThumbnailProperties = (PFN_DwmUpdateThumbnailProperties)GetProcAddress(GetDwmApi(), "DwmUpdateThumbnailProperties");

        float2 available(value.x, value.y);
        float3 size = value;

        DWM_THUMBNAIL_PROPERTIES thumb;
        HRESULT result = GetScaledWindowSize(m_window, &size, thumb);
        result = lDwmUpdateThumbnailProperties(m_thumbnail, &thumb);

        if (result == S_OK)
        {
            m_size = float2(size.x, size.y);
            m_scale = value.z;
            m_visual.Offset(float3((available - m_size) / 2, 0));
            m_visual.Scale(float3(size.z));
        }
    }

    // size->z carries the rasterization scale of whatever shows the thumbnail on the way in,
    // and the reciprocal of the factor it was rasterized at on the way out.
    HRESULT WindowVisual::GetScaledWindowSize(HWND window, float3* size, DWM_THUMBNAIL_PROPERTIES& thumb)
    {
        const static auto lDwmpQueryWindowThumbnailSourceSize = (DwmpQueryWindowThumbnailSourceSize)GetProcAddress(GetDwmApi(), MAKEINTRESOURCEA(162));

        // Both callers hand thumb straight to DWM without looking at the result, so it has
        // to be safe to pass on when this returns early - a minimized or cloaked window
        // reports no size at all.
        thumb = {};

        SIZE windowSize{};
        HRESULT result = lDwmpQueryWindowThumbnailSourceSize(window, false, &windowSize);

        if (result != S_OK || (windowSize.cx == 0 && windowSize.cy == 0))
        {
            return result;
        }

        size->z *= 2;

        double ratioX = (size->x * size->z) / windowSize.cx;
        double ratioY = (size->y * size->z) / windowSize.cy;
        double ratio = std::min(ratioX, ratioY);

        long width = windowSize.cx * ratio;
        long height = windowSize.cy * ratio;

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
