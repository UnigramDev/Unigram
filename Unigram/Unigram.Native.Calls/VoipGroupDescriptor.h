#pragma once

#include "VoipGroupDescriptor.g.h"

namespace winrt::Unigram::Native::Calls::implementation
{
	struct VoipGroupDescriptor : VoipGroupDescriptorT<VoipGroupDescriptor>
	{
		VoipGroupDescriptor() = default;

		hstring AudioInputId();
		void AudioInputId(hstring value);

		hstring AudioOutputId();
		void AudioOutputId(hstring value);

		VoipVideoContentType VideoContentType();
		void VideoContentType(VoipVideoContentType value);

		VoipVideoCapture VideoCapture();
		void VideoCapture(VoipVideoCapture value);

		bool IsNoiseSuppressionEnabled();
		void IsNoiseSuppressionEnabled(bool value);

	private:
		hstring m_audioInputId{ L"" };
		hstring m_audioOutputId{ L"" };
		VoipVideoContentType m_videoContentType{ VoipVideoContentType::Generic };
		VoipVideoCapture m_videoCapture{ nullptr };
		bool m_isNoiseSuppressionEnabled{ true };
	};
}

namespace winrt::Unigram::Native::Calls::factory_implementation
{
	struct VoipGroupDescriptor : VoipGroupDescriptorT<VoipGroupDescriptor, implementation::VoipGroupDescriptor>
	{
	};
}
