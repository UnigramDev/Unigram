#include "pch.h"
#include "DirectRectangleClip2.h"

namespace winrt::Telegram::Native::Composition::implementation
{
    float DirectRectangleClip2::Left()
    {
        return m_left;
    }

    void DirectRectangleClip2::Left(float value)
    {
        m_left = value;
        m_impl->SetLeft(value);
    }

    float DirectRectangleClip2::Top()
    {
        return m_top;
    }

    void DirectRectangleClip2::Top(float value)
    {
        m_top = value;
        m_impl->SetTop(value);
    }

    float DirectRectangleClip2::Right()
    {
        return m_right;
    }

    void DirectRectangleClip2::Right(float value)
    {
        m_right = value;
        m_impl->SetRight(value);
    }

    float DirectRectangleClip2::Bottom()
    {
        return m_bottom;
    }

    void DirectRectangleClip2::Bottom(float value)
    {
        m_bottom = value;
        m_impl->SetBottom(value);
    }



    float2 DirectRectangleClip2::TopLeft()
    {
        return m_topLeft;
    }

    void DirectRectangleClip2::TopLeft(float2 value)
    {
        m_topLeft = value;
        m_impl->SetTopLeftRadiusX(value.x);
        m_impl->SetTopLeftRadiusY(value.y);
    }

    float2 DirectRectangleClip2::TopRight()
    {
        return m_topRight;
    }

    void DirectRectangleClip2::TopRight(float2 value)
    {
        m_topRight = value;
        m_impl->SetTopRightRadiusX(value.x);
        m_impl->SetTopRightRadiusY(value.y);
    }

    float2 DirectRectangleClip2::BottomRight()
    {
        return m_bottomRight;
    }

    void DirectRectangleClip2::BottomRight(float2 value)
    {
        m_bottomRight = value;
        m_impl->SetBottomRightRadiusX(value.x);
        m_impl->SetBottomRightRadiusY(value.y);
    }

    float2 DirectRectangleClip2::BottomLeft()
    {
        return m_bottomLeft;
    }

    void DirectRectangleClip2::BottomLeft(float2 value)
    {
        m_bottomLeft = value;
        m_impl->SetBottomLeftRadiusX(value.x);
        m_impl->SetBottomLeftRadiusY(value.y);
    }

    void DirectRectangleClip2::Set(float2 uniform)
    {
        TopLeft(uniform);
        TopRight(uniform);
        BottomRight(uniform);
        BottomLeft(uniform);
    }

    void DirectRectangleClip2::Set(float2 topLeft, float2 topRight, float2 bottomRight, float2 bottomLeft)
    {
        TopLeft(topLeft);
        TopRight(topRight);
        BottomRight(bottomRight);
        BottomLeft(bottomLeft);
    }

    void DirectRectangleClip2::SetInset(float uniform)
    {
        Left(uniform);
        Top(uniform);
        Right(uniform);
        Bottom(uniform);
    }

    void DirectRectangleClip2::SetInset(float left, float top, float right, float bottom)
    {
        Left(left);
        Top(top);
        Right(right);
        Bottom(bottom);
    }

    void DirectRectangleClip2::AnimateTop(Compositor compositor, float from, float to, double duration)
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

    void DirectRectangleClip2::AnimateBottom(Compositor compositor, float from, float to, double duration)
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

    void DirectRectangleClip2::AnimateBottomLeft(Compositor compositor, float2 from, float2 to, double duration)
    {
        m_bottomLeft = to;

        HRESULT hr;
        auto device = CompositionDevice::Current();

        winrt::com_ptr<IDCompositionAnimation> animationX;
        winrt::com_ptr<IDCompositionAnimation> animationY;
        hr = device->CreateCubicBezierAnimation(compositor, from.x, to.x, duration, animationX.put());
        hr = device->CreateCubicBezierAnimation(compositor, from.y, to.y, duration, animationY.put());

        if (SUCCEEDED(hr))
        {
            m_impl->SetBottomLeftRadiusX(animationX.get());
            m_impl->SetBottomLeftRadiusY(animationY.get());
        }
        else
        {
            m_impl->SetBottomLeftRadiusX(to.x);
            m_impl->SetBottomLeftRadiusY(to.y);
        }
    }

    void DirectRectangleClip2::AnimateBottomRight(Compositor compositor, float2 from, float2 to, double duration)
    {
        m_bottomRight = to;

        HRESULT hr;
        auto device = CompositionDevice::Current();

        winrt::com_ptr<IDCompositionAnimation> animationX;
        winrt::com_ptr<IDCompositionAnimation> animationY;
        hr = device->CreateCubicBezierAnimation(compositor, from.x, to.x, duration, animationX.put());
        hr = device->CreateCubicBezierAnimation(compositor, from.y, to.y, duration, animationY.put());

        if (SUCCEEDED(hr))
        {
            m_impl->SetBottomRightRadiusX(animationX.get());
            m_impl->SetBottomRightRadiusY(animationY.get());
        }
        else
        {
            m_impl->SetBottomRightRadiusX(to.x);
            m_impl->SetBottomRightRadiusY(to.y);
        }
    }
}
