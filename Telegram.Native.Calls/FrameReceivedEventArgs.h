#pragma once

#include "FrameReceivedEventArgs.g.h"

namespace winrt::Telegram::Native::Calls::implementation
{
    struct FrameReceivedEventArgs : FrameReceivedEventArgsT<FrameReceivedEventArgs>
    {
        FrameReceivedEventArgs(int32_t pixelWidth, int32_t pixelHeight);

        int32_t PixelWidth();
        int32_t PixelHeight();
        
        std::atomic<int32_t> m_pixelWidth{ 0 };
        std::atomic<int32_t> m_pixelHeight{ 0 };
    };
}
