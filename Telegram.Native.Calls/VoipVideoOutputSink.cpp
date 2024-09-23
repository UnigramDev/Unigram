#include "pch.h"
#include "VoipVideoOutputSink.h"
#if __has_include("VoipVideoOutputSink.g.cpp")
#include "VoipVideoOutputSink.g.cpp"
#endif

namespace winrt::Telegram::Native::Calls::implementation
{
    VoipVideoOutputSink::VoipVideoOutputSink(SpriteVisual visual, bool mirrored)
        : m_sink(std::make_shared<VoipVideoOutput>(visual, mirrored))
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
