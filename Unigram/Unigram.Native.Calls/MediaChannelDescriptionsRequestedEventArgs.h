#pragma once

#include "MediaChannelDescriptionsRequestedEventArgs.g.h"

using namespace winrt::Windows::Foundation::Collections;

namespace winrt::Unigram::Native::Calls::implementation
{
	struct MediaChannelDescriptionsRequestedEventArgs : MediaChannelDescriptionsRequestedEventArgsT<MediaChannelDescriptionsRequestedEventArgs>
	{
		MediaChannelDescriptionsRequestedEventArgs(IVector<int32_t> ssrcs, MediaChannelDescriptionsRequestedDeferral deferral);

		IVector<int32_t> Ssrcs();
		MediaChannelDescriptionsRequestedDeferral Deferral();

	private:
		IVector<int32_t> m_ssrcs;
		MediaChannelDescriptionsRequestedDeferral m_deferral;
	};
}
