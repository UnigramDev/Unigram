#pragma once

#include "MediaChannelDescriptionsRequestedEventArgs.g.h"

using namespace winrt::Windows::Foundation::Collections;

namespace winrt::Telegram::Native::Calls::implementation
{
    struct MediaChannelDescriptionsRequestedEventArgs : MediaChannelDescriptionsRequestedEventArgsT<MediaChannelDescriptionsRequestedEventArgs>
    {
        MediaChannelDescriptionsRequestedEventArgs(IVector<uint32_t> audioSourceIds, MediaChannelDescriptionsRequestedDeferral deferral);

        IVector<uint32_t> AudioSourceIds();
        MediaChannelDescriptionsRequestedDeferral Deferral();

    private:
        IVector<uint32_t> m_audioSourceIds;
        MediaChannelDescriptionsRequestedDeferral m_deferral;
    };
}
