#include "pch.h"
#include "VoipGroupDescriptor.h"
#if __has_include("VoipGroupDescriptor.g.cpp")
#include "VoipGroupDescriptor.g.cpp"
#endif

namespace winrt::Telegram::Native::Calls::implementation
{
    hstring VoipGroupDescriptor::AudioInputId()
    {
        return m_audioInputId;
    }

    void VoipGroupDescriptor::AudioInputId(hstring value)
    {
        m_audioInputId = value;
    }

    hstring VoipGroupDescriptor::AudioOutputId()
    {
        return m_audioOutputId;
    }

    void VoipGroupDescriptor::AudioOutputId(hstring value)
    {
        m_audioOutputId = value;
    }

    VoipCaptureBase VoipGroupDescriptor::VideoCapture()
    {
        return m_videoCapture;
    }

    void VoipGroupDescriptor::VideoCapture(VoipCaptureBase value)
    {
        m_videoCapture = value;
    }

    VoipVideoContentType VoipGroupDescriptor::VideoContentType()
    {
        return m_videoContentType;
    }

    void VoipGroupDescriptor::VideoContentType(VoipVideoContentType value)
    {
        m_videoContentType = value;
    }

    bool VoipGroupDescriptor::IsNoiseSuppressionEnabled()
    {
        return m_isNoiseSuppressionEnabled;
    }

    void VoipGroupDescriptor::IsNoiseSuppressionEnabled(bool value)
    {
        m_isNoiseSuppressionEnabled = value;
    }

    int64_t VoipGroupDescriptor::AudioProcessId()
    {
        return m_audioProcessId;
    }

    void VoipGroupDescriptor::AudioProcessId(int64_t value)
    {
        m_audioProcessId = value;
    }
}
