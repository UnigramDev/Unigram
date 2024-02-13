#include "pch.h"
#include "VoipVideoCapture.h"
#if __has_include("VoipVideoCapture.g.cpp")
#include "VoipVideoCapture.g.cpp"
#endif

#include "VoipVideoRendererToken.h"

#include "StaticThreads.h"
#include "platform/uwp/UwpContext.h"

namespace winrt::Telegram::Native::Calls::implementation
{
    VoipVideoCapture::VoipVideoCapture(hstring id)
    {
        m_impl = tgcalls::VideoCaptureInterface::Create(
            tgcalls::StaticThreads::getThreads(),
            winrt::to_string(id));
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

    winrt::Telegram::Native::Calls::VoipVideoRendererToken VoipVideoCapture::SetOutput(winrt::Microsoft::Graphics::Canvas::UI::Xaml::CanvasControl canvas, bool enableBlur)
    {
        if (m_impl)
        {
            if (canvas != nullptr)
            {
                auto renderer = std::make_shared<VoipVideoRenderer>(canvas, true, enableBlur);
                m_impl->setOutput(renderer);
                return *winrt::make_self<VoipVideoRendererToken>(renderer, canvas);
            }
            else
            {
                m_impl->setOutput(nullptr);
            }
        }

        return nullptr;
    }
} // namespace winrt::Telegram::Native::Calls::implementation
