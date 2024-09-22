#include "pch.h"
#include "VoipVideoCapture.h"
#if __has_include("VoipVideoCapture.g.cpp")
#include "VoipVideoCapture.g.cpp"
#endif

#include "VoipVideoRendererToken.h"
#include "VoipVideoOutputSink.h"

#include "StaticThreads.h"
#include "platform/uwp/UwpContext.h"

namespace winrt::Telegram::Native::Calls::implementation
{
    VoipVideoCapture::VoipVideoCapture(hstring id)
    {
        m_impl = tgcalls::VideoCaptureInterface::Create(
            tgcalls::StaticThreads::getThreads(),
            winrt::to_string(id));
        m_impl->setOnFatalError([this] {
            m_failed = true;
            m_fatalErrorOccurred(*this, nullptr);
            });
    }

    VoipVideoCapture::~VoipVideoCapture()
    {
        m_impl = nullptr;
    }

    void VoipVideoCapture::Close()
    {
        m_impl = nullptr;
    }

    void VoipVideoCapture::SwitchToDevice(hstring deviceId)
    {
        if (m_impl)
        {
            m_impl->switchToDevice(winrt::to_string(deviceId), false);
        }
    }

    void VoipVideoCapture::SetState(VoipVideoState state)
    {
        if (m_impl)
        {
            m_impl->setState((tgcalls::VideoState)state);
        }
    }

    void VoipVideoCapture::SetPreferredAspectRatio(float aspectRatio)
    {
        if (m_impl)
        {
            m_impl->setPreferredAspectRatio(aspectRatio);
        }
    }

    void VoipVideoCapture::SetOutputV2(winrt::Telegram::Native::Calls::VoipVideoOutputSink sink)
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

    winrt::Telegram::Native::Calls::VoipVideoRendererToken VoipVideoCapture::SetOutput(winrt::Microsoft::Graphics::Canvas::UI::Xaml::CanvasControl canvas, winrt::guid visualId, bool enableBlur)
    {
        if (m_impl)
        {
            if (canvas != nullptr)
            {
                auto renderer = std::make_shared<VoipVideoRenderer>(canvas, true, enableBlur);
                m_impl->setOutput(renderer);
                return *winrt::make_self<VoipVideoRendererToken>(renderer, visualId);
            }
            else
            {
                m_impl->setOutput(nullptr);
            }
        }

        return nullptr;
    }

    winrt::event_token VoipVideoCapture::FatalErrorOccurred(Windows::Foundation::TypedEventHandler<
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

    void VoipVideoCapture::FatalErrorOccurred(winrt::event_token const& token)
    {
        m_fatalErrorOccurred.remove(token);
    }

} // namespace winrt::Telegram::Native::Calls::implementation
