#include "pch.h"
#include "BroadcastPartRequestedEventArgs.h"

namespace winrt::Unigram::Native::Calls::implementation
{
	BroadcastPartRequestedEventArgs::BroadcastPartRequestedEventArgs(int32_t scale, int64_t time, BroadcastPartRequestedDeferral deferral)
		: m_scale(scale),
		m_time(time),
		m_deferral(deferral)
	{

	}

	int32_t BroadcastPartRequestedEventArgs::Scale() {
		return m_scale;
	}

	int64_t BroadcastPartRequestedEventArgs::Time() {
		return m_time;
	}

	BroadcastPartRequestedDeferral BroadcastPartRequestedEventArgs::Deferral() {
		return m_deferral;
	}
}
