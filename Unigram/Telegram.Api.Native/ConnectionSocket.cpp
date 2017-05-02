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
		return E_NOT_VALID_STATE;
	}

	HRESULT result;
	int wsaLastError;
	sockaddr_storage socketAddress = {};
	if (ipv6)
	{
		auto socketAddressIpv6 = reinterpret_cast<sockaddr_in6*>(&socketAddress);
		socketAddressIpv6->sin6_family = AF_INET6;
		socketAddressIpv6->sin6_port = htons(port);

		if (InetPton(AF_INET6, address.c_str(), &socketAddressIpv6->sin6_addr) != 1)
		{
			wsaLastError = WSAGetLastError();
			CloseSocket(wsaLastError);

			return HRESULT_FROM_WIN32(wsaLastError);
		}
	}
	else
	{
		auto socketAddressIpv4 = reinterpret_cast<sockaddr_in*>(&socketAddress);
		socketAddressIpv4->sin_family = AF_INET;
		socketAddressIpv4->sin_port = htons(port);

		if (InetPton(AF_INET, address.c_str(), &socketAddressIpv4->sin_addr) != 1)
		{
			wsaLastError = WSAGetLastError();
			CloseSocket(wsaLastError);

			return HRESULT_FROM_WIN32(wsaLastError);
		}
	}

	if ((m_socket = socket(socketAddress.ss_family, SOCK_STREAM, 0)) == INVALID_SOCKET)
	{
		wsaLastError = WSAGetLastError();
		CloseSocket(wsaLastError);

		return HRESULT_FROM_WIN32(wsaLastError);
	}

	int noDelay = 1;
	setsockopt(m_socket, IPPROTO_TCP, TCP_NODELAY, reinterpret_cast<char*>(&noDelay), sizeof(int));

	u_long nonBlock = 1;
	if (ioctlsocket(m_socket, FIONBIO, &nonBlock) == SOCKET_ERROR)
	{
		wsaLastError = WSAGetLastError();
		CloseSocket(wsaLastError);

		return HRESULT_FROM_WIN32(wsaLastError);
	}

	m_socketEvent.Attach(WSACreateEvent());
	if (!m_socketEvent.IsValid())
	{
		wsaLastError = WSAGetLastError();
		CloseSocket(wsaLastError);

		return HRESULT_FROM_WIN32(wsaLastError);
	}

	if (FAILED(result = OnSocketCreated()))
	{
		CloseSocket(WSA_OPERATION_ABORTED);

		return result;
	}

	if (WSAEventSelect(m_socket, m_socketEvent.Get(), FD_CONNECT | FD_READ | FD_WRITE | FD_CLOSE) == SOCKET_ERROR)
	{
		wsaLastError = WSAGetLastError();
		CloseSocket(wsaLastError);

		return HRESULT_FROM_WIN32(wsaLastError);
	}

	if (connect(m_socket, reinterpret_cast<sockaddr*>(&socketAddress), ipv6 ? sizeof(sockaddr_in6) : sizeof(sockaddr_in)) == SOCKET_ERROR)
	{
		auto wsaLastError = WSAGetLastError();
		if (wsaLastError != WSAEWOULDBLOCK)
		{
			wsaLastError = WSAGetLastError();
			CloseSocket(wsaLastError);

			return HRESULT_FROM_WIN32(wsaLastError);
		}
	}

	return S_OK;
}

HRESULT ConnectionSocket::CloseSocket()
{
	return CloseSocket(NO_ERROR);
}

HRESULT ConnectionSocket::CloseSocket(int wsaError)
{
	if (m_socket == INVALID_SOCKET)
	{
		return E_NOT_VALID_STATE;
	}

	if (closesocket(m_socket) == SOCKET_ERROR)
	{
		return WSAGetLastHRESULT();
	}

	m_socket = INVALID_SOCKET;

	return OnSocketClosed(wsaError);
}

HRESULT ConnectionSocket::OnEvent(PTP_CALLBACK_INSTANCE callbackInstance)
{
	int wsaLastError;
	WSANETWORKEVENTS networkEvents;
	if (WSAEnumNetworkEvents(m_socket, m_socketEvent.Get(), &networkEvents) == SOCKET_ERROR)
	{
		wsaLastError = WSAGetLastError();

		return HRESULT_FROM_WIN32(wsaLastError);
	}

	if (networkEvents.lNetworkEvents & FD_CLOSE)
	{
		if ((wsaLastError = networkEvents.iErrorCode[FD_CLOSE_BIT]) != NO_ERROR)
		{
		}

		I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement socket close event");
	}

	if (networkEvents.lNetworkEvents & FD_CONNECT)
	{
		if ((wsaLastError = networkEvents.iErrorCode[FD_CONNECT_BIT]) != NO_ERROR)
		{
		}

		I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement socket connection event");
	}

	if (networkEvents.lNetworkEvents & FD_WRITE)
	{
		if ((wsaLastError = networkEvents.iErrorCode[FD_WRITE_BIT]) != NO_ERROR)
		{
		}

		I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement socket write event");
	}

	if (networkEvents.lNetworkEvents & FD_READ)
	{
		if ((wsaLastError = networkEvents.iErrorCode[FD_READ_BIT]) != NO_ERROR)
		{
		}

		I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement socket read event");
	}

	return S_OK;
}