#include "pch.h"
#include "MediaChannelDescriptionsRequestedEventArgs.h"

namespace winrt::Unigram::Native::Calls::implementation
{
    MediaChannelDescriptionsRequestedEventArgs::MediaChannelDescriptionsRequestedEventArgs(IVector<int32_t> ssrcs, MediaChannelDescriptionsRequestedDeferral deferral)
        : m_ssrcs(ssrcs),
        m_deferral(deferral)
    {

    }

    IVector<int32_t> MediaChannelDescriptionsRequestedEventArgs::Ssrcs() {
        return m_ssrcs;
    }

    MediaChannelDescriptionsRequestedDeferral MediaChannelDescriptionsRequestedEventArgs::Deferral() {
        return m_deferral;
    }
}
