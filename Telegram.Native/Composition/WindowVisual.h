#pragma once

#include "Composition/WindowVisual.g.h"

#include "DwmThumbnail.h"

#include <winrt/Windows.UI.Composition.h>
#include <winrt/Windows.UI.h>

using namespace winrt::Windows::Foundation::Numerics;
using namespace winrt::Windows::UI;
using namespace winrt::Windows::UI::Composition;

namespace winrt::Telegram::Native::Composition::implementation
{
    struct WindowVisual : WindowVisualT<WindowVisual>
    {
        static bool IsValid(WindowId windowId, hstring& title);
        static winrt::Telegram::Native::Composition::WindowVisual Create(WindowId windowId, float rasterizationScale);

        static uint32_t GetWindowProcessId(WindowId windowId);
        static WindowId GetCurrentWindowId();

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
