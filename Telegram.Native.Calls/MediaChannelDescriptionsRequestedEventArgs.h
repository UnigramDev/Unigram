#pragma once

#include "MediaChannelDescriptionsRequestedEventArgs.g.h"

#include <winrt/Telegram.Td.Api.h>

using namespace winrt::Telegram::Td::Api;

namespace winrt::Telegram::Native::Calls::implementation
{
    struct MediaChannelDescriptionsRequestedEventArgs : MediaChannelDescriptionsRequestedEventArgsT<MediaChannelDescriptionsRequestedEventArgs>
    {
        MediaChannelDescriptionsRequestedEventArgs(MediaChannelDescriptionsRequestedDeferral deferral);

        MediaChannelDescriptionsRequestedDeferral Deferral();

    private:
        MediaChannelDescriptionsRequestedDeferral m_deferral;
    };
}
