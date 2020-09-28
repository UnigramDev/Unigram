// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

#pragma once

#include "SignalingDataEmittedEventArgs.g.h"

using namespace winrt::Windows::Foundation::Collections;

namespace winrt::Unigram::Native::Calls::implementation
{
	struct SignalingDataEmittedEventArgs : SignalingDataEmittedEventArgsT<SignalingDataEmittedEventArgs>
	{
		SignalingDataEmittedEventArgs() = default;
		SignalingDataEmittedEventArgs(IVector<uint8_t> data);

		IVector<uint8_t> Data();

	private:
		IVector<uint8_t> m_data{ nullptr };
	};
} // namespace winrt::Unigram::Native::Calls::implementation
