#include "pch.h"
#include "VoipGroupDescriptor.h"
#if __has_include("VoipGroupDescriptor.g.cpp")
#include "VoipGroupDescriptor.g.cpp"
#endif

namespace winrt::Unigram::Native::Calls::implementation
{
	hstring VoipGroupDescriptor::AudioInputId() {
		return m_audioInputId;
	}

	void VoipGroupDescriptor::AudioInputId(hstring value) {
		m_audioInputId = value;
	}

	hstring VoipGroupDescriptor::AudioOutputId() {
		return m_audioOutputId;
	}

	void VoipGroupDescriptor::AudioOutputId(hstring value) {
		m_audioOutputId = value;
	}

	IVoipVideoCapture VoipGroupDescriptor::VideoCapture() {
		return m_videoCapture;
	}

	void VoipGroupDescriptor::VideoCapture(IVoipVideoCapture value) {
		m_videoCapture = value;
	}

	VoipVideoContentType VoipGroupDescriptor::VideoContentType() {
		return m_videoContentType;
	}

	void VoipGroupDescriptor::VideoContentType(VoipVideoContentType value) {
		m_videoContentType = value;
	}

	bool VoipGroupDescriptor::IsNoiseSuppressionEnabled() {
		return m_isNoiseSuppressionEnabled;
	}

	void VoipGroupDescriptor::IsNoiseSuppressionEnabled(bool value) {
		m_isNoiseSuppressionEnabled = value;
	}
}
