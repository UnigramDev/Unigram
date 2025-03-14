#include "pch.h"
#include "MediaChannelDescriptionsRequestedEventArgs.h"

namespace winrt::Telegram::Native::Calls::implementation
{
    MediaChannelDescriptionsRequestedEventArgs::MediaChannelDescriptionsRequestedEventArgs(MediaChannelDescriptionsRequestedDeferral deferral)
        : m_deferral(deferral)
    {

    }

    MediaChannelDescriptionsRequestedDeferral MediaChannelDescriptionsRequestedEventArgs::Deferral()
    {
        return m_deferral;
    }
}
