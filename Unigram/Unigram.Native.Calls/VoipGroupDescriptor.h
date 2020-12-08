#pragma once

#include "VoipGroupDescriptor.g.h"

namespace winrt::Unigram::Native::Calls::implementation
{
    struct VoipGroupDescriptor : VoipGroupDescriptorT<VoipGroupDescriptor>
    {
        VoipGroupDescriptor() = default;

        hstring m_audioInputId{ L"" };
        hstring AudioInputId();
        void AudioInputId(hstring value);

        hstring m_audioOutputId{ L"" };
        hstring AudioOutputId();
        void AudioOutputId(hstring value);
};
}

namespace winrt::Unigram::Native::Calls::factory_implementation
{
    struct VoipGroupDescriptor : VoipGroupDescriptorT<VoipGroupDescriptor, implementation::VoipGroupDescriptor>
    {
    };
}
