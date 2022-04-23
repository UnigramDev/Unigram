#pragma once

#include "VoipScreenCapture.g.h"

#include "VoipVideoCapture.h"

#include <winrt/Windows.Graphics.Capture.h>

using namespace winrt::Windows::Graphics::Capture;

namespace winrt::Unigram::Native::Calls::implementation
{
	struct VoipScreenCapture : VoipScreenCaptureT<VoipScreenCapture, winrt::Unigram::Native::Calls::VoipVideoCapture>
	{
		VoipScreenCapture(GraphicsCaptureItem item);
		~VoipScreenCapture();

		void Close();

		void SwitchToDevice(hstring deviceId);
		void SetState(VoipVideoState state);
		void SetPreferredAspectRatio(float aspectRatio);
		void SetOutput(winrt::Microsoft::Graphics::Canvas::UI::Xaml::CanvasControl canvas, bool enableBlur = true);

		std::shared_ptr<tgcalls::VideoCaptureInterface> m_impl = nullptr;

		winrt::event_token FatalErrorOccurred(Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipScreenCapture,
			winrt::Windows::Foundation::IInspectable> const& value);
		void FatalErrorOccurred(winrt::event_token const& token);

		winrt::event_token Paused(Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipScreenCapture,
			bool> const& value);
		void Paused(winrt::event_token const& token);

	private:
		winrt::event<Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipScreenCapture,
			winrt::Windows::Foundation::IInspectable>> m_fatalErrorOccurred;
		winrt::event<Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipScreenCapture,
			bool>> m_paused;
	};
}

namespace winrt::Unigram::Native::Calls::factory_implementation
{
	struct VoipScreenCapture : VoipScreenCaptureT<VoipScreenCapture, implementation::VoipScreenCapture>
	{
	};
}
