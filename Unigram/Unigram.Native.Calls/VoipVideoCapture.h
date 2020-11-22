#pragma once

#include "VoipVideoCapture.g.h"
#include "VoipVideoRenderer.h"
#include "Instance.h"
#include "InstanceImpl.h"
#include "VideoCaptureInterface.h"

//using namespace winrt::Windows::Foundation;
//using namespace winrt::Windows::Foundation::Collections;

namespace winrt::Unigram::Native::Calls::implementation
{
	struct VoipVideoCapture : VoipVideoCaptureT<VoipVideoCapture>
	{
		VoipVideoCapture(hstring id);
		~VoipVideoCapture();

		void Close();

		void SwitchToDevice(hstring deviceId);
		void SetState(VoipVideoState state);
		void SetPreferredAspectRatio(float aspectRatio);
		void SetOutput(winrt::Microsoft::Graphics::Canvas::UI::Xaml::CanvasControl canvas);

		void FeedBytes(winrt::Windows::Graphics::Imaging::SoftwareBitmap bitmap);

		std::shared_ptr<tgcalls::VideoCaptureInterface> m_impl = nullptr;

		winrt::Windows::Graphics::Imaging::SoftwareBitmap m_test = nullptr;
		//std::shared_ptr<rtc::VideoSinkInterface<webrtc::VideoFrame>> m_renderer = nullptr;
	private:
	};
} // namespace winrt::Unigram::Native::Calls::implementation

namespace winrt::Unigram::Native::Calls::factory_implementation
{
	struct VoipVideoCapture : VoipVideoCaptureT<VoipVideoCapture, implementation::VoipVideoCapture>
	{
	};
} // namespace winrt::Unigram::Native::Calls::factory_implementation
