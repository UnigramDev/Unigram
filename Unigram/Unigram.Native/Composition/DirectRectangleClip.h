#pragma once

#include "Composition/DirectRectangleClip.g.h"
#include "CompositionDevice.h"
#include "dcompex.h"

namespace winrt::Unigram::Native::Composition::implementation
{
    struct DirectRectangleClip : DirectRectangleClipT<DirectRectangleClip>
    {
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

        void AnimateTop(Compositor compositor, float from, float to, double duration);
        void AnimateBottom(Compositor compositor, float from, float to, double duration);

    private:
        winrt::com_ptr<IDCompositionRectangleClip> m_impl;
        winrt::com_ptr<IDCompositionAnimation> m_bottomImpl;
        winrt::com_ptr<IDCompositionAnimation> m_topImpl;

        float m_left;
        float m_top;
        float m_right;
        float m_bottom;

        float m_topLeft;
        float m_topRight;
        float m_bottomRight;
        float m_bottomLeft;
    };
}
