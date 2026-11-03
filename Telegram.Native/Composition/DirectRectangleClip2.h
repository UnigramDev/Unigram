#pragma once

#include "Composition/DirectRectangleClip2.g.h"
#include "CompositionDevice.h"

#pragma push_macro("WINAPI_FAMILY")
#undef WINAPI_FAMILY
#define WINAPI_FAMILY WINAPI_FAMILY_DESKTOP_APP
#define IUnknown ::IUnknown
#include <dcomp.h>
#pragma pop_macro("WINAPI_FAMILY")
#undef IUnknown

using namespace winrt::Windows::Foundation::Numerics;

namespace winrt::Telegram::Native::Composition::implementation
{
    // The Windows App SDK compositor is not DirectComposition, and its objects answer none of
    // dcomp.h's interfaces; its RectangleClip has the per-corner radii this needs. UWP keeps the
    // DirectComposition clip because RectangleClip needs Windows 10 2004, past its minimum.
    struct DirectRectangleClip2 : DirectRectangleClip2T<DirectRectangleClip2>
    {
        friend CompositionDevice;

#ifdef WINUI3
        DirectRectangleClip2(winrt::UIComposition::RectangleClip impl)
        : m_impl(impl) {

        }
#else
        DirectRectangleClip2(winrt::com_ptr<IDCompositionRectangleClip> impl)
        : m_impl(impl) {

        }
#endif

        float Left();
        void Left(float value);

        float Top();
        void Top(float value);

        float Right();
        void Right(float value);

        float Bottom();
        void Bottom(float value);



        float2 TopLeft();
        void TopLeft(float2 value);

        float2 TopRight();
        void TopRight(float2 value);

        float2 BottomRight();
        void BottomRight(float2 value);

        float2 BottomLeft();
        void BottomLeft(float2 value);

        void Set(float2 uniform);
        void Set(float2 topLeft, float2 topRight, float2 bottomRight, float2 bottomLeft);

        void SetInset(float uniform);
        void SetInset(float left, float top, float right, float bottom);

    private:
#ifdef WINUI3
        winrt::UIComposition::RectangleClip m_impl{ nullptr };
#else
        winrt::com_ptr<IDCompositionRectangleClip> m_impl;
#endif

        float m_left = 0;
        float m_top = 0;
        float m_right = 0;
        float m_bottom = 0;

        float2 m_topLeft{};
        float2 m_topRight{};
        float2 m_bottomRight{};
        float2 m_bottomLeft{};
    };
}
