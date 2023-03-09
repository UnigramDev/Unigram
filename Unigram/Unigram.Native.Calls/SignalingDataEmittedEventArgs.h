#pragma once

#include "SignalingDataEmittedEventArgs.g.h"

using namespace winrt::Windows::Foundation::Collections;

namespace winrt::Telegram::Native::Calls::implementation
{
	struct SignalingDataEmittedEventArgs : SignalingDataEmittedEventArgsT<SignalingDataEmittedEventArgs>
	{
		SignalingDataEmittedEventArgs(IVector<uint8_t> data);

		IVector<uint8_t> Data();

	private:
		IVector<uint8_t> m_data{ nullptr };
	};
} // namespace winrt::Telegram::Native::Calls::implementation
