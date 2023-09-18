#pragma once

#include "Composition/DirectRectangleClip.g.h"
#include "CompositionDevice.h"
#include "dcompex.h"

namespace winrt::Telegram::Native::Composition::implementation
{
    struct DirectRectangleClip : DirectRectangleClipT<DirectRectangleClip>
    {
        friend CompositionDevice;

        DirectRectangleClip(winrt::com_ptr<IDCompositionRectangleClip> impl)
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



        float TopLeft();
        void TopLeft(float value);

        float TopRight();
        void TopRight(float value);

        float BottomRight();
        void BottomRight(float value);

        float BottomLeft();
        void BottomLeft(float value);

        void Set(float uniform);
        void Set(float topLeft, float topRight, float bottomRight, float bottomLeft);

        void SetInset(float uniform);
        void SetInset(float left, float top, float right, float bottom);

        void AnimateTop(Compositor compositor, float from, float to, double duration);
        void AnimateBottom(Compositor compositor, float from, float to, double duration);

        void AnimateBottomLeft(Compositor compositor, float from, float to, double duration);
        void AnimateBottomRight(Compositor compositor, float from, float to, double duration);

    private:
        winrt::com_ptr<IDCompositionRectangleClip> m_impl;

        float m_left = 0;
        float m_top = 0;
        float m_right = 0;
        float m_bottom = 0;

        float m_topLeft = 0;
        float m_topRight = 0;
        float m_bottomRight = 0;
        float m_bottomLeft = 0;
    };
}
