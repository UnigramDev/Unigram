#pragma once

#include "VoipVideoCapture.g.h"
#include "VoipVideoOutput.h"
#include "Instance.h"
#include "InstanceImpl.h"
#include "VideoCaptureInterface.h"

#include <winrt/Windows.Graphics.Capture.h>

using namespace winrt::Windows::Graphics::Capture;

namespace winrt::Telegram::Native::Calls::implementation
{
    struct VoipVideoCapture : VoipVideoCaptureT<VoipVideoCapture, winrt::Telegram::Native::Calls::VoipCaptureBase>
    {
        VoipVideoCapture(hstring id);
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

    private:
        bool m_failed{ false };
        winrt::event<Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Calls::VoipCaptureBase,
            winrt::Windows::Foundation::IInspectable>> m_fatalErrorOccurred;
    };
} // namespace winrt::Telegram::Native::Calls::implementation

namespace winrt::Telegram::Native::Calls::factory_implementation
{
    struct VoipVideoCapture : VoipVideoCaptureT<VoipVideoCapture, implementation::VoipVideoCapture>
    {
    };
} // namespace winrt::Telegram::Native::Calls::factory_implementation
