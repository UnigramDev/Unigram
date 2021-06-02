#pragma once

#include "GroupNetworkStateChangedEventArgs.g.h"

namespace winrt::Unigram::Native::Calls::implementation
{
	struct GroupNetworkStateChangedEventArgs : GroupNetworkStateChangedEventArgsT<GroupNetworkStateChangedEventArgs>
	{
		GroupNetworkStateChangedEventArgs(bool isConnected, bool isTransitioningFromBroadcastToRtc);

		bool IsConnected();
		bool IsTransitioningFromBroadcastToRtc();

	private:
		bool m_isConnected;
		bool m_isTransitioningFromBroadcastToRtc;
	};
}
