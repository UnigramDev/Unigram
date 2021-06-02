#include "pch.h"
#include "VoipVideoRendererToken.h"

namespace winrt::Unigram::Native::Calls::implementation
{
	VoipVideoRendererToken::VoipVideoRendererToken(std::shared_ptr<rtc::VideoSinkInterface<webrtc::VideoFrame>> sink, int32_t audioSource, hstring endpointId, IVector<GroupCallVideoSourceGroup> sourceGroups, CanvasControl canvasControl)
		: m_sink(sink),
		m_audioSource(audioSource),
		m_endpointId(endpointId),
		m_sourceGroups(sourceGroups),
		m_canvasControl(std::make_shared<CanvasControl>(canvasControl))
	{
	}

	int32_t VoipVideoRendererToken::AudioSource() {
		return m_audioSource;
	}

	hstring VoipVideoRendererToken::EndpointId() {
		return m_endpointId;
	}

	IVector<GroupCallVideoSourceGroup> VoipVideoRendererToken::SourceGroups() {
		return m_sourceGroups;
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
