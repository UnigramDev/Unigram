#include "pch.h"

// DEFINE_GUID reserves storage for CLSID_YUV420Effect only in the translation unit that
// includes initguid.h ahead of it; every other one just gets the declaration. This is
// that translation unit — it used to be whichever one happened to reach the initguid.h
// that LoopbackCapture.h dragged in.
#include <initguid.h>

#include "VoipVideoOutputSink.h"
#if __has_include("VoipVideoOutputSink.g.cpp")
#include "VoipVideoOutputSink.g.cpp"
#endif

namespace winrt::Telegram::Native::Calls::implementation
{
    VoipVideoOutputSink::VoipVideoOutputSink(CompositionGraphicsDevice const& device, SpriteVisual const& visual, bool mirrored, bool uniformToFill)
        : m_sink(std::make_shared<VoipVideoOutput>(device, visual, mirrored, uniformToFill))
    {
    }

    void VoipVideoOutputSink::Stop()
    {
        m_sink.reset();
        m_sink = nullptr;
    }

    bool VoipVideoOutputSink::IsMirrored()
    {
        return m_sink->m_mirrored;
    }

    void VoipVideoOutputSink::IsMirrored(bool value)
    {
        m_sink->m_mirrored = value;
    }

    int32_t VoipVideoOutputSink::PixelWidth()
    {
        return m_sink->m_pixelWidth;
    }

    int32_t VoipVideoOutputSink::PixelHeight()
    {
        return m_sink->m_pixelHeight;
    }

    std::shared_ptr<VoipVideoOutput> VoipVideoOutputSink::Sink()
    {
        return m_sink;
    }

    winrt::event_token VoipVideoOutputSink::FrameReceived(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipVideoOutputSink,
        winrt::Telegram::Native::Calls::FrameReceivedEventArgs> const& value)
    {
        return m_sink->m_frameReceivedEventSource.add(value);
    }

    void VoipVideoOutputSink::FrameReceived(winrt::event_token const& token)
    {
        m_sink->m_frameReceivedEventSource.remove(token);
    }
}
