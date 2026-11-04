#pragma once

#include "Composition/WindowVisual.g.h"

#include "DwmThumbnail.h"

#include <winrt/Windows.UI.h>

#ifdef WINUI3
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Graphics.Imaging.h>
#endif

using namespace winrt::Windows::Foundation::Numerics;
using namespace winrt::Windows::UI;
using namespace winrt::UIComposition;

namespace winrt::Telegram::Native::Composition::implementation
{
    struct WindowVisual : WindowVisualT<WindowVisual>
    {
        static bool IsValid(WindowId windowId, hstring& title);
        static winrt::Telegram::Native::Composition::WindowVisual Create(WindowId windowId, WindowId destinationId, Compositor compositor, float rasterizationScale);

        static uint32_t GetWindowProcessId(WindowId windowId);
        static WindowId GetRootWindowId(WindowId windowId);

#ifdef WINUI3
        static winrt::Windows::Graphics::Imaging::SoftwareBitmap CaptureSnapshot(WindowId windowId, int32_t maxWidth, int32_t maxHeight);
#endif

        WindowVisual(HWND window, HTHUMBNAIL thumbnail, Visual visual, float3 size, float scale);
        ~WindowVisual();

        Visual Child();

        float3 Size();
        void Size(float3 value);

    private:
        static HRESULT GetScaledWindowSize(HWND window, float3* size, DWM_THUMBNAIL_PROPERTIES& thumb);

        HWND m_window;
        HTHUMBNAIL m_thumbnail;

        float2 m_size;
        float m_scale;
        Visual m_visual;
    };
}

namespace winrt::Telegram::Native::Composition::factory_implementation
{
    struct WindowVisual : WindowVisualT<WindowVisual, implementation::WindowVisual>
    {
    };
}
