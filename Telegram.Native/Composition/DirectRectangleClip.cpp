#include "pch.h"
#include "DirectRectangleClip.h"

namespace winrt::Telegram::Native::Composition::implementation
{
    float DirectRectangleClip::Left()
    {
        return m_left;
    }

    void DirectRectangleClip::Left(float value)
    {
        m_left = value;
        m_impl->SetLeft(value);
    }

    float DirectRectangleClip::Top()
    {
        return m_top;
    }

    void DirectRectangleClip::Top(float value)
    {
        m_top = value;
        m_impl->SetTop(value);
    }

    float DirectRectangleClip::Right()
    {
        return m_right;
    }

    void DirectRectangleClip::Right(float value)
    {
        m_right = value;
        m_impl->SetRight(value);
    }

    float DirectRectangleClip::Bottom()
    {
        return m_bottom;
    }

    void DirectRectangleClip::Bottom(float value)
    {
        m_bottom = value;
        m_impl->SetBottom(value);
    }



    float DirectRectangleClip::TopLeft()
    {
        return m_topLeft;
    }

    void DirectRectangleClip::TopLeft(float value)
    {
        m_topLeft = value;
        m_impl->SetTopLeftRadiusX(value);
        m_impl->SetTopLeftRadiusY(value);
    }

    float DirectRectangleClip::TopRight()
    {
        return m_topRight;
    }

    void DirectRectangleClip::TopRight(float value)
    {
        m_topRight = value;
        m_impl->SetTopRightRadiusX(value);
        m_impl->SetTopRightRadiusY(value);
    }

    float DirectRectangleClip::BottomRight()
    {
        return m_bottomRight;
    }

    void DirectRectangleClip::BottomRight(float value)
    {
        m_bottomRight = value;
        m_impl->SetBottomRightRadiusX(value);
        m_impl->SetBottomRightRadiusY(value);
    }

    float DirectRectangleClip::BottomLeft()
    {
        return m_bottomLeft;
    }

    void DirectRectangleClip::BottomLeft(float value)
    {
        m_bottomLeft = value;
        m_impl->SetBottomLeftRadiusX(value);
        m_impl->SetBottomLeftRadiusY(value);
    }

    void DirectRectangleClip::Set(float uniform)
    {
        TopLeft(uniform);
        TopRight(uniform);
        BottomRight(uniform);
        BottomLeft(uniform);
    }

    void DirectRectangleClip::Set(float topLeft, float topRight, float bottomRight, float bottomLeft)
    {
        TopLeft(topLeft);
        TopRight(topRight);
        BottomRight(bottomRight);
        BottomLeft(bottomLeft);
    }

    void DirectRectangleClip::SetInset(float uniform)
    {
        Left(uniform);
        Top(uniform);
        Right(uniform);
        Bottom(uniform);
    }

    void DirectRectangleClip::SetInset(float left, float top, float right, float bottom)
    {
        Left(left);
        Top(top);
        Right(right);
        Bottom(bottom);
    }

    void DirectRectangleClip::AnimateTop(Compositor compositor, float from, float to, double duration)
    {
        m_top = to;

        HRESULT hr;
        auto device = CompositionDevice::Current();

        winrt::com_ptr<IDCompositionAnimation> animation;
        hr = device->CreateCubicBezierAnimation(compositor, from, to, duration, animation.put());

        if (SUCCEEDED(hr))
        {
            m_impl->SetTop(animation.get());
        }
        else
        {
            m_impl->SetTop(to);
        }
    }

    void DirectRectangleClip::AnimateBottom(Compositor compositor, float from, float to, double duration)
    {
        m_bottom = to;

        HRESULT hr;
        auto device = CompositionDevice::Current();

        winrt::com_ptr<IDCompositionAnimation> animation;
        hr = device->CreateCubicBezierAnimation(compositor, from, to, duration, animation.put());

        if (SUCCEEDED(hr))
        {
            m_impl->SetBottom(animation.get());
        }
        else
        {
            m_impl->SetBottom(to);
        }
    }

    void DirectRectangleClip::AnimateBottomLeft(Compositor compositor, float from, float to, double duration)
    {
        m_bottomLeft = to;

        HRESULT hr;
        auto device = CompositionDevice::Current();

        winrt::com_ptr<IDCompositionAnimation> animation;
        hr = device->CreateCubicBezierAnimation(compositor, from, to, duration, animation.put());

        if (SUCCEEDED(hr))
        {
            m_impl->SetBottomLeftRadiusX(animation.get());
            m_impl->SetBottomLeftRadiusY(animation.get());
        }
        else
        {
            m_impl->SetBottomLeftRadiusX(to);
            m_impl->SetBottomLeftRadiusY(to);
        }
    }

    void DirectRectangleClip::AnimateBottomRight(Compositor compositor, float from, float to, double duration)
    {
        m_bottomRight = to;

        HRESULT hr;
        auto device = CompositionDevice::Current();

        winrt::com_ptr<IDCompositionAnimation> animation;
        hr = device->CreateCubicBezierAnimation(compositor, from, to, duration, animation.put());

        if (SUCCEEDED(hr))
        {
            m_impl->SetBottomRightRadiusX(animation.get());
            m_impl->SetBottomRightRadiusY(animation.get());
        }
        else
        {
            m_impl->SetBottomRightRadiusX(to);
            m_impl->SetBottomRightRadiusY(to);
        }
    }
}
