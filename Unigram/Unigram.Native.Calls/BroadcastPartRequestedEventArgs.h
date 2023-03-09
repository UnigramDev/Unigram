#pragma once

#include "BroadcastPartRequestedEventArgs.g.h"

#include <winrt/Telegram.Td.Api.h>

using namespace winrt::Telegram::Td::Api;

namespace winrt::Telegram::Native::Calls::implementation
{
	struct BroadcastPartRequestedEventArgs : BroadcastPartRequestedEventArgsT<BroadcastPartRequestedEventArgs>
	{
		BroadcastPartRequestedEventArgs(int32_t scale, int64_t time, int32_t channelId, GroupCallVideoQuality videoQuality, BroadcastPartRequestedDeferral deferral);

		int32_t Scale();
		int64_t Time();
		int32_t ChannelId();
		GroupCallVideoQuality VideoQuality();
		BroadcastPartRequestedDeferral Deferral();

	private:
		int32_t m_scale;
		int64_t m_time;
		int32_t m_channelId;
		GroupCallVideoQuality m_videoQuality;
		BroadcastPartRequestedDeferral m_deferral;
	};
}
