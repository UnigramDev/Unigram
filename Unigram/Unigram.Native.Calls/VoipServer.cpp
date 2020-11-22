#include "pch.h"
#include "VoipServer.h"
#include "VoipServer.g.cpp"

namespace winrt::Unigram::Native::Calls::implementation
{
	VoipServer::VoipServer()
	{
	}

	hstring VoipServer::Host() {
		return m_host;
	}

	void VoipServer::Host(hstring value) {
		m_host = value;
	}

	int32_t VoipServer::Port() {
		return m_port;
	}

	void VoipServer::Port(int32_t value) {
		m_port = value;
	}

	hstring VoipServer::Login() {
		return m_login;
	}

	void VoipServer::Login(hstring value) {
		m_login = value;
	}

	hstring VoipServer::Password() {
		return m_password;
	}

	void VoipServer::Password(hstring value) {
		m_password = value;
	}

	bool VoipServer::IsTurn() {
		return m_isTurn;
	}

	void VoipServer::IsTurn(bool value) {
		m_isTurn = value;
	}

} // namespace winrt::Unigram::Native::Calls::implementation
