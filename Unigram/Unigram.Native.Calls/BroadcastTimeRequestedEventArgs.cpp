#include "pch.h"
#include "BroadcastTimeRequestedEventArgs.h"

namespace winrt::Unigram::Native::Calls::implementation
{
	BroadcastTimeRequestedEventArgs::BroadcastTimeRequestedEventArgs(BroadcastTimeRequestedDeferral deferral)
		: m_deferral(deferral)
	{

	}

	BroadcastTimeRequestedDeferral BroadcastTimeRequestedEventArgs::Deferral() {
		return m_deferral;
	}
}
