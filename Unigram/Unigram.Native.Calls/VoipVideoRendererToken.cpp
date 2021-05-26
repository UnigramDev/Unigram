#include "pch.h"
#include "VoipVideoRendererToken.h"

namespace winrt::Unigram::Native::Calls::implementation
{
	VoipVideoRendererToken::VoipVideoRendererToken(std::shared_ptr<rtc::VideoSinkInterface<webrtc::VideoFrame>> sink, int32_t audioSource, hstring endpointId, hstring description, CanvasControl canvasControl)
	: m_sink(sink),
	m_audioSource(audioSource),
	m_endpointId(endpointId),
	m_description(description),
	m_canvasControl(std::make_shared<CanvasControl>(canvasControl))
	{
	}

	int32_t VoipVideoRendererToken::AudioSource() {
		return m_audioSource;
	}

	hstring VoipVideoRendererToken::EndpointId() {
		return m_endpointId;
	}

	hstring VoipVideoRendererToken::Description() {
		return m_description;
	}

	bool VoipVideoRendererToken::IsMatch(hstring endpointId, CanvasControl canvasControl)
	{
		return m_endpointId == endpointId && *m_canvasControl == canvasControl;
	}

	void VoipVideoRendererToken::Stop() {
		m_canvasControl.reset();
		m_canvasControl = nullptr;

		m_sink.reset();
		m_sink = nullptr;
	}
}
