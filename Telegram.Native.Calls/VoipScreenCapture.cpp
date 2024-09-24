#include "pch.h"
#include "VoipScreenCapture.h"
#if __has_include("VoipScreenCapture.g.cpp")
#include "VoipScreenCapture.g.cpp"
#endif

#include "VoipVideoOutputSink.h"

#include "StaticThreads.h"
#include "platform/uwp/UwpContext.h"

namespace winrt::Telegram::Native::Calls::implementation
{
    VoipScreenCapture::VoipScreenCapture(GraphicsCaptureItem item)
    {
        m_impl = tgcalls::VideoCaptureInterface::Create(
            tgcalls::StaticThreads::getThreads(),
            "GraphicsCaptureItem", true,
            std::make_shared<tgcalls::UwpContext>(item));
        m_impl->setOnFatalError([this] {
            m_failed = true;
            m_fatalErrorOccurred(*this, nullptr);
            });
        m_impl->setOnPause([this](bool paused) {
            m_paused(*this, paused);
            });
    }

    void VoipScreenCapture::Stop()
    {
        m_impl.reset();
        m_impl = nullptr;
    }

    void VoipScreenCapture::SwitchToDevice(hstring deviceId)
    {
        if (m_impl)
        {
            m_impl->switchToDevice(winrt::to_string(deviceId), false);
        }
    }

    void VoipScreenCapture::SetState(VoipVideoState state)
    {
        if (m_impl)
        {
            m_impl->setState((tgcalls::VideoState)state);
        }
    }

    void VoipScreenCapture::SetPreferredAspectRatio(float aspectRatio)
    {
        if (m_impl)
        {
            m_impl->setPreferredAspectRatio(aspectRatio);
        }
    }

    void VoipScreenCapture::SetOutput(winrt::Telegram::Native::Calls::VoipVideoOutputSink sink)
    {
        if (m_impl)
        {
            if (sink != nullptr)
            {
                auto implementation = winrt::get_self<VoipVideoOutputSink>(sink);
                m_impl->setOutput(implementation->Sink());
            }
            else
            {
                m_impl->setOutput(nullptr);
            }
        }
    }

    winrt::event_token VoipScreenCapture::FatalErrorOccurred(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipCaptureBase,
        winrt::Windows::Foundation::IInspectable> const& value)
    {
        auto token = m_fatalErrorOccurred.add(value);

        if (m_failed)
        {
            m_fatalErrorOccurred(*this, nullptr);
        }

        return token;
    }

    void VoipScreenCapture::FatalErrorOccurred(winrt::event_token const& token)
    {
        m_fatalErrorOccurred.remove(token);
    }

    winrt::event_token VoipScreenCapture::Paused(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipScreenCapture,
        bool> const& value)
    {
        return m_paused.add(value);
    }

    void VoipScreenCapture::Paused(winrt::event_token const& token)
    {
        m_paused.remove(token);
    }

    bool VoipScreenCapture::IsSupported()
    {
        try
        {
            return GraphicsCaptureSession::IsSupported();
        }
        catch (winrt::hresult_error const& ex)
        {
            return false;
        }
    }
}
