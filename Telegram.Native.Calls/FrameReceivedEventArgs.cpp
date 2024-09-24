#include "pch.h"
#include "FrameReceivedEventArgs.h"
#if __has_include("FrameReceivedEventArgs.g.cpp")
#include "FrameReceivedEventArgs.g.cpp"
#endif

namespace winrt::Telegram::Native::Calls::implementation
{
    FrameReceivedEventArgs::FrameReceivedEventArgs(int32_t pixelWidth, int32_t pixelHeight)
        : m_pixelWidth(pixelWidth)
        , m_pixelHeight(pixelHeight)
    {

    }

    int32_t FrameReceivedEventArgs::PixelWidth()
    {
        return m_pixelWidth;
    }

    int32_t FrameReceivedEventArgs::PixelHeight()
    {
        return m_pixelHeight;
    }
}
