#include "pch.h"
#include <Ws2tcpip.h>
#include "ConnectionSocket.h"
#include "EventObject.h"
#include "Helpers\COMHelper.h"

using namespace Telegram::Api::Native;


ConnectionSocket::ConnectionSocket() :
	m_socket(INVALID_SOCKET)
{
}

ConnectionSocket::~ConnectionSocket()
{
}

HRESULT ConnectionSocket::OpenSocket(std::wstring address, UINT16 port, boolean ipv6)
{
	if (m_socket != INVALID_SOCKET)
	{
		return E_INVALIDARG;
	}

	HRESULT result;
	sockaddr_storage socketAddress = {};
	if (ipv6)
	{
		auto socketAddressIpv6 = reinterpret_cast<sockaddr_in6*>(&socketAddress);
		socketAddressIpv6->sin6_family = AF_INET6;
		socketAddressIpv6->sin6_port = htons(port);

		if (InetPton(AF_INET6, address.c_str(), &socketAddressIpv6->sin6_addr) != 1)
		{
			result = GetWSALastHRESULT();
			CloseSocket(true);

			return result;
		}
	}
	else
	{
		auto socketAddressIpv4 = reinterpret_cast<sockaddr_in*>(&socketAddress);
		socketAddressIpv4->sin_family = AF_INET6;
		socketAddressIpv4->sin_port = htons(port);

		if (InetPton(AF_INET6, address.c_str(), &socketAddressIpv4->sin_addr) != 1)
		{
			result = GetWSALastHRESULT();
			CloseSocket(true);

			return result;
		}
	}

	if ((m_socket = socket(socketAddress.ss_family, SOCK_STREAM, 0)) == INVALID_SOCKET)
	{
		result = GetWSALastHRESULT();
		CloseSocket(true);

		return result;
	}

	int noDelay = 1;
	setsockopt(m_socket, IPPROTO_TCP, TCP_NODELAY, reinterpret_cast<char*>(&noDelay), sizeof(int));

	u_long nonBlock = 1;
	if (ioctlsocket(m_socket, FIONBIO, &nonBlock) != NO_ERROR)
	{
		result = GetWSALastHRESULT();
		CloseSocket(true);

		return result;
	}

	if (connect(m_socket, reinterpret_cast<sockaddr*>(&socketAddress), ipv6 ? sizeof(sockaddr_in6) : sizeof(sockaddr_in)) != NO_ERROR)
	{
		result = GetWSALastHRESULT();
		CloseSocket(true);

		return result;
	}

	return OnSocketOpened();
}

HRESULT ConnectionSocket::CloseSocket()
{
	return CloseSocket(false);
}

HRESULT ConnectionSocket::CloseSocket(boolean error)
{
	if (m_socket == INVALID_SOCKET)
	{
		return E_INVALIDARG;
	}

	if (closesocket(m_socket) != NO_ERROR)
	{
		return GetWSALastHRESULT();
	}

	m_socket = INVALID_SOCKET;

	return error ? S_OK : OnSocketClosed();
}

HRESULT ConnectionSocket::HandleEvent(EventObjectEventContext const* context)
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement socket event handling");

	return S_OK;
}