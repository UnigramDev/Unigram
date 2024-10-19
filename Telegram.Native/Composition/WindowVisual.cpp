#include "pch.h"
#include "WindowVisual.h"
#include "Composition/WindowVisual.g.cpp"

#include "winrt/Windows.UI.Core.h"
#include "winrt/Windows.UI.Xaml.h"

using namespace winrt::Windows::UI;
using namespace winrt::Windows::UI::Composition;
using namespace winrt::Windows::UI::Core;
using namespace winrt::Windows::UI::Xaml;

namespace winrt::Telegram::Native::Composition::implementation
{
    winrt::Telegram::Native::Composition::WindowVisual WindowVisual::Create(WindowId windowId)
    {
        const static auto lDwmpQueryWindowThumbnailSourceSize = (DwmpQueryWindowThumbnailSourceSize)GetProcAddress(GetDwmApi(), MAKEINTRESOURCEA(162));
        const static auto lDwmpCreateSharedThumbnailVisual = (DwmpCreateSharedThumbnailVisual)GetProcAddress(GetDwmApi(), MAKEINTRESOURCEA(147));

        const static auto user32 = GetModuleHandle(L"user32.dll");
        const static auto GetParent = (GetParentProto)GetProcAddress(user32, "GetParent");

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

    HMODULE WindowVisual::GetDwmApi()
    {
        const static auto dwmapiLib = LoadLibrary(L"dwmapi.dll");
        return dwmapiLib;
    }
}
