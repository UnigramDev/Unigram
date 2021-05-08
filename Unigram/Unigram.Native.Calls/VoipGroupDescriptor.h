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

        IVoipVideoCapture VideoCapture();
        void VideoCapture(IVoipVideoCapture value);

    private:
        hstring m_audioInputId{ L"" };
        hstring m_audioOutputId{ L"" };
        VoipVideoContentType m_videoContentType{ VoipVideoContentType::Generic };
        IVoipVideoCapture m_videoCapture{ nullptr };
};
}

namespace winrt::Unigram::Native::Calls::factory_implementation
{
    struct VoipGroupDescriptor : VoipGroupDescriptorT<VoipGroupDescriptor, implementation::VoipGroupDescriptor>
    {
    };
}
