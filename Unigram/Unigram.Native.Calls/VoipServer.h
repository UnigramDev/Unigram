// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

#pragma once

#include "VoipServer.g.h"
#include "Instance.h"

namespace winrt::Unigram::Native::Calls::implementation
{
	struct VoipServer : VoipServerT<VoipServer>
	{
		VoipServer();

		hstring m_host;
		hstring Host();
		void Host(hstring value);

		uint16_t m_port;
		uint16_t Port();
		void Port(uint16_t value);

		hstring m_login;
		hstring Login();
		void Login(hstring value);

		hstring m_password;
		hstring Password();
		void Password(hstring value);

		bool m_isTurn;
		bool IsTurn();
		void IsTurn(bool value);
	};
} // namespace winrt::Unigram::Native::Calls::implementation

namespace winrt::Unigram::Native::Calls::factory_implementation
{
	struct VoipServer : VoipServerT<VoipServer, implementation::VoipServer>
	{
	};
} // namespace winrt::Unigram::Native::Calls::factory_implementation
