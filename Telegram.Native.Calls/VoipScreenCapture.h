#pragma once

#include "VoipScreenCapture.g.h"

#include "VoipVideoCapture.h"

#include <winrt/Windows.Graphics.Capture.h>

using namespace winrt::Windows::Graphics::Capture;

namespace winrt::Telegram::Native::Calls::implementation
{
    struct VoipScreenCapture : VoipScreenCaptureT<VoipScreenCapture, winrt::Telegram::Native::Calls::VoipCaptureBase>
    {
        VoipScreenCapture(GraphicsCaptureItem item);
        void Stop();

        void SwitchToDevice(hstring deviceId);
        void SetState(VoipVideoState state);
        void SetPreferredAspectRatio(float aspectRatio);
        void SetOutput(winrt::Telegram::Native::Calls::VoipVideoOutputSink sink);

        std::shared_ptr<tgcalls::VideoCaptureInterface> m_impl = nullptr;

        winrt::event_token FatalErrorOccurred(Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Calls::VoipCaptureBase,
            winrt::Windows::Foundation::IInspectable> const& value);
        void FatalErrorOccurred(winrt::event_token const& token);

        winrt::event_token Paused(Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Calls::VoipScreenCapture,
            bool> const& value);
        void Paused(winrt::event_token const& token);

        static bool IsSupported();

    private:
        bool m_failed{ false };
        winrt::event<Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Calls::VoipCaptureBase,
            winrt::Windows::Foundation::IInspectable>> m_fatalErrorOccurred;
        winrt::event<Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Calls::VoipScreenCapture,
            bool>> m_paused;
    };
}

namespace winrt::Telegram::Native::Calls::factory_implementation
{
    struct VoipScreenCapture : VoipScreenCaptureT<VoipScreenCapture, implementation::VoipScreenCapture>
    {
    };
}
