#pragma once

#include "VoipVideoRendererToken.g.h"

#include "VoipVideoRenderer.h"

#include "api/video/video_sink_interface.h"
#include "api/video/video_frame.h"

using namespace winrt::Microsoft::Graphics::Canvas::UI::Xaml;
using namespace winrt::Telegram::Td::Api;
using namespace winrt::Windows::Foundation::Collections;

namespace winrt::Unigram::Native::Calls::implementation
{
	struct VoipVideoRendererToken : VoipVideoRendererTokenT<VoipVideoRendererToken>
	{
		VoipVideoRendererToken(std::shared_ptr<VoipVideoRenderer> sink, int32_t audioSource, hstring endpointId, IVector<GroupCallVideoSourceGroup> sourceGroups, CanvasControl canvasControl);

		int32_t AudioSource();
		hstring EndpointId();
		IVector<GroupCallVideoSourceGroup> SourceGroups();

		winrt::Microsoft::UI::Xaml::Media::Stretch Stretch();
		void Stretch(winrt::Microsoft::UI::Xaml::Media::Stretch value);

		bool IsMirrored();
		void IsMirrored(bool value);

		bool IsMatch(hstring endpointId, CanvasControl canvasControl);

		void Stop();

	private:
		std::shared_ptr<VoipVideoRenderer> m_sink;
		std::shared_ptr<CanvasControl> m_canvasControl;
		int32_t m_audioSource;
		hstring m_endpointId;
		IVector<GroupCallVideoSourceGroup> m_sourceGroups;
	};
}
