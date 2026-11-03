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
#ifdef WINUI3
        m_impl.Left(value);
#else
        m_impl->SetLeft(value);
#endif
    }

    float DirectRectangleClip2::Top()
    {
        return m_top;
    }

    void DirectRectangleClip2::Top(float value)
    {
        m_top = value;
#ifdef WINUI3
        m_impl.Top(value);
#else
        m_impl->SetTop(value);
#endif
    }

    float DirectRectangleClip2::Right()
    {
        return m_right;
    }

    void DirectRectangleClip2::Right(float value)
    {
        m_right = value;
#ifdef WINUI3
        m_impl.Right(value);
#else
        m_impl->SetRight(value);
#endif
    }

    float DirectRectangleClip2::Bottom()
    {
        return m_bottom;
    }

    void DirectRectangleClip2::Bottom(float value)
    {
        m_bottom = value;
#ifdef WINUI3
        m_impl.Bottom(value);
#else
        m_impl->SetBottom(value);
#endif
    }



    float2 DirectRectangleClip2::TopLeft()
    {
        return m_topLeft;
    }

    void DirectRectangleClip2::TopLeft(float2 value)
    {
        m_topLeft = value;
#ifdef WINUI3
        m_impl.TopLeftRadius(value);
#else
        m_impl->SetTopLeftRadiusX(value.x);
        m_impl->SetTopLeftRadiusY(value.y);
#endif
    }

    float2 DirectRectangleClip2::TopRight()
    {
        return m_topRight;
    }

    void DirectRectangleClip2::TopRight(float2 value)
    {
        m_topRight = value;
#ifdef WINUI3
        m_impl.TopRightRadius(value);
#else
        m_impl->SetTopRightRadiusX(value.x);
        m_impl->SetTopRightRadiusY(value.y);
#endif
    }

    float2 DirectRectangleClip2::BottomRight()
    {
        return m_bottomRight;
    }

    void DirectRectangleClip2::BottomRight(float2 value)
    {
        m_bottomRight = value;
#ifdef WINUI3
        m_impl.BottomRightRadius(value);
#else
        m_impl->SetBottomRightRadiusX(value.x);
        m_impl->SetBottomRightRadiusY(value.y);
#endif
    }

    float2 DirectRectangleClip2::BottomLeft()
    {
        return m_bottomLeft;
    }

    void DirectRectangleClip2::BottomLeft(float2 value)
    {
        m_bottomLeft = value;
#ifdef WINUI3
        m_impl.BottomLeftRadius(value);
#else
        m_impl->SetBottomLeftRadiusX(value.x);
        m_impl->SetBottomLeftRadiusY(value.y);
#endif
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
}
