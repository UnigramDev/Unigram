#pragma once

#include "VoipVideoCapture.g.h"
#include "VoipVideoRenderer.h"
#include "Instance.h"
#include "InstanceImpl.h"
#include "VideoCaptureInterface.h"

#include <winrt/Windows.Graphics.Capture.h>

using namespace winrt::Windows::Graphics::Capture;

namespace winrt::Unigram::Native::Calls::implementation
{
	struct VoipVideoCapture : VoipVideoCaptureT<VoipVideoCapture>
	{
		VoipVideoCapture(hstring id);
		VoipVideoCapture() = default;
		~VoipVideoCapture();

		void Close();

		void SwitchToDevice(hstring deviceId);
		void SetState(VoipVideoState state);
		void SetPreferredAspectRatio(float aspectRatio);
		void SetOutput(winrt::Microsoft::Graphics::Canvas::UI::Xaml::CanvasControl canvas);

		std::shared_ptr<tgcalls::VideoCaptureInterface> m_impl = nullptr;
	private:
	};
} // namespace winrt::Unigram::Native::Calls::implementation

namespace winrt::Unigram::Native::Calls::factory_implementation
{
	struct VoipVideoCapture : VoipVideoCaptureT<VoipVideoCapture, implementation::VoipVideoCapture>
	{
	};
} // namespace winrt::Unigram::Native::Calls::factory_implementation
