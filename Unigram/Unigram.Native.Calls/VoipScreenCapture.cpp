#include "pch.h"
#include "VoipScreenCapture.h"
#if __has_include("VoipScreenCapture.g.cpp")
#include "VoipScreenCapture.g.cpp"
#endif

#include "StaticThreads.h"
#include "platform/uwp/UwpContext.h"

namespace winrt::Unigram::Native::Calls::implementation
{
	VoipScreenCapture::VoipScreenCapture(GraphicsCaptureItem item)
	{
		m_impl = tgcalls::VideoCaptureInterface::Create(
			tgcalls::StaticThreads::getThreads(),
			"GraphicsCaptureItem", true,
			std::make_shared<tgcalls::UwpContext>(item));
		m_impl->setOnFatalError([this] {
			m_fatalErrorOccurred(*this, nullptr);
			});
		m_impl->setOnPause([this](bool paused) {
			m_paused(*this, paused);
			});
	}

	VoipScreenCapture::~VoipScreenCapture()
	{
		m_impl = nullptr;
	}

	void VoipScreenCapture::Close() {
		m_impl = nullptr;
	}

	void VoipScreenCapture::SwitchToDevice(hstring deviceId) {
		if (m_impl) {
			m_impl->switchToDevice(string_to_unmanaged(deviceId), false);
		}
	}

	void VoipScreenCapture::SetState(VoipVideoState state) {
		if (m_impl) {
			m_impl->setState((tgcalls::VideoState)state);
		}
	}

	void VoipScreenCapture::SetPreferredAspectRatio(float aspectRatio) {
		if (m_impl) {
			m_impl->setPreferredAspectRatio(aspectRatio);
		}
	}

	void VoipScreenCapture::SetOutput(winrt::Microsoft::Graphics::Canvas::UI::Xaml::CanvasControl canvas, bool enableBlur) {
		if (m_impl) {
			if (canvas != nullptr) {
				m_impl->setOutput(std::make_shared<VoipVideoRenderer>(canvas, false, enableBlur));
			}
			else {
				m_impl->setOutput(nullptr);
			}
		}
	}

	winrt::event_token VoipScreenCapture::FatalErrorOccurred(Windows::Foundation::TypedEventHandler<
		winrt::Unigram::Native::Calls::VoipScreenCapture,
		winrt::Windows::Foundation::IInspectable> const& value)
	{
		return m_fatalErrorOccurred.add(value);
	}

	void VoipScreenCapture::FatalErrorOccurred(winrt::event_token const& token)
	{
		m_fatalErrorOccurred.remove(token);
	}

	winrt::event_token VoipScreenCapture::Paused(Windows::Foundation::TypedEventHandler<
		winrt::Unigram::Native::Calls::VoipScreenCapture,
		bool> const& value)
	{
		return m_paused.add(value);
	}

	void VoipScreenCapture::Paused(winrt::event_token const& token)
	{
		m_paused.remove(token);
	}
}
