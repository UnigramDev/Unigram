#pragma once

#include "BroadcastTimeRequestedEventArgs.g.h"

#include <winrt/Telegram.Td.Api.h>

using namespace winrt::Telegram::Td::Api;

namespace winrt::Telegram::Native::Calls::implementation
{
	struct BroadcastTimeRequestedEventArgs : BroadcastTimeRequestedEventArgsT<BroadcastTimeRequestedEventArgs>
	{
		BroadcastTimeRequestedEventArgs(BroadcastTimeRequestedDeferral deferral);

		BroadcastTimeRequestedDeferral Deferral();

	private:
		BroadcastTimeRequestedDeferral m_deferral;
	};
}
