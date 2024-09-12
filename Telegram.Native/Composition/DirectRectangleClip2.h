#pragma once

#include "Composition/DirectRectangleClip2.g.h"
#include "CompositionDevice.h"
#include "dcompex.h"

using namespace winrt::Windows::Foundation::Numerics;

namespace winrt::Telegram::Native::Composition::implementation
{
    struct DirectRectangleClip2 : DirectRectangleClip2T<DirectRectangleClip2>
    {
        friend CompositionDevice;

        DirectRectangleClip2(winrt::com_ptr<IDCompositionRectangleClip> impl)
        : m_impl(impl) {

        }

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

        void AnimateTop(Compositor compositor, float from, float to, double duration);
        void AnimateBottom(Compositor compositor, float from, float to, double duration);

        void AnimateBottomLeft(Compositor compositor, float2 from, float2 to, double duration);
        void AnimateBottomRight(Compositor compositor, float2 from, float2 to, double duration);

    private:
        winrt::com_ptr<IDCompositionRectangleClip> m_impl;

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
