#include "pch.h"
#include "VoipVideoCapture.h"
#if __has_include("VoipVideoCapture.g.cpp")
#include "VoipVideoCapture.g.cpp"
#endif

#include "StaticThreads.h"
#include "platform/uwp/UwpContext.h"

namespace winrt::Unigram::Native::Calls::implementation
{
	VoipVideoCapture::VoipVideoCapture(hstring id)
	{
		m_impl = tgcalls::VideoCaptureInterface::Create(
			tgcalls::StaticThreads::getThreads(),
			string_to_unmanaged(id));
	}

	VoipVideoCapture::~VoipVideoCapture()
	{
		m_impl = nullptr;
	}

	void VoipVideoCapture::Close() {
		m_impl = nullptr;
	}

	void VoipVideoCapture::SwitchToDevice(hstring deviceId) {
		if (m_impl) {
			m_impl->switchToDevice(string_to_unmanaged(deviceId), false);
		}
	}

	void VoipVideoCapture::SetState(VoipVideoState state) {
		if (m_impl) {
			m_impl->setState((tgcalls::VideoState)state);
		}
	}

	void VoipVideoCapture::SetPreferredAspectRatio(float aspectRatio) {
		if (m_impl) {
			m_impl->setPreferredAspectRatio(aspectRatio);
		}
	}

	void VoipVideoCapture::SetOutput(winrt::Microsoft::Graphics::Canvas::UI::Xaml::CanvasControl canvas, bool enableBlur) {
		if (m_impl) {
			if (canvas != nullptr) {
				m_impl->setOutput(std::make_shared<VoipVideoRenderer>(canvas, enableBlur));
			}
			else {
				m_impl->setOutput(nullptr);
			}
		}
	}
} // namespace winrt::Unigram::Native::Calls::implementation
