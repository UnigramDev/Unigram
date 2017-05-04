#include "pch.h"
#include <Ws2tcpip.h>
#include "ConnectionSocket.h"
#include "EventObject.h"
#include "Helpers\COMHelper.h"

#define BreakIfSocketError(result, method) \
	if((result = method) == SOCKET_ERROR) \
		break

#define BreakIfError(result, method) \
	if((result = method) != NO_ERROR) \
		break

#define READ_BUFFER_SIZE 1024 * 128

using namespace Telegram::Api::Native;


ConnectionSocket::ConnectionSocket() :
	m_socket(INVALID_SOCKET)
{
}

ConnectionSocket::~ConnectionSocket()
{
	CloseSocket(WSA_OPERATION_ABORTED, false);
}

HRESULT ConnectionSocket::ConnectSocket(std::wstring address, UINT16 port, boolean ipv6)
{
	if (m_socket != INVALID_SOCKET)
	{
		return E_NOT_VALID_STATE;
	}

	sockaddr_storage socketAddress = {};
	if (ipv6)
	{
		auto socketAddressIpv6 = reinterpret_cast<sockaddr_in6*>(&socketAddress);
		socketAddressIpv6->sin6_family = AF_INET6;
		socketAddressIpv6->sin6_port = htons(port);

		if (InetPton(AF_INET6, address.c_str(), &socketAddressIpv6->sin6_addr) != 1)
		{
			return WSAGetLastHRESULT();
		}
	}
	else
	{
		auto socketAddressIpv4 = reinterpret_cast<sockaddr_in*>(&socketAddress);
		socketAddressIpv4->sin_family = AF_INET;
		socketAddressIpv4->sin_port = htons(port);

		if (InetPton(AF_INET, address.c_str(), &socketAddressIpv4->sin_addr) != 1)
		{
			return WSAGetLastHRESULT();
		}
	}

	m_socketEvent.Attach(WSACreateEvent());
	if (!m_socketEvent.IsValid())
	{
		return WSAGetLastHRESULT();
	}

	if ((m_socket = socket(socketAddress.ss_family, SOCK_STREAM, 0)) == INVALID_SOCKET)
	{
		return GetLastErrorAndCloseSocket();
	}

	int noDelay = 1;
	setsockopt(m_socket, IPPROTO_TCP, TCP_NODELAY, reinterpret_cast<char*>(&noDelay), sizeof(int));

	HRESULT result;
	if (FAILED(result = OnSocketCreated()))
	{
		CloseSocket(WSA_OPERATION_ABORTED, true);

		return result;
	}

	if (WSAEventSelect(m_socket, m_socketEvent.Get(), FD_CONNECT | FD_READ | FD_WRITE | FD_CLOSE) == SOCKET_ERROR)
	{
		return GetLastErrorAndCloseSocket();
	}

	if (connect(m_socket, reinterpret_cast<sockaddr*>(&socketAddress), ipv6 ? sizeof(sockaddr_in6) : sizeof(sockaddr_in)) == SOCKET_ERROR)
	{
		auto wsaLastError = WSAGetLastError();
		if (wsaLastError != WSAEWOULDBLOCK)
		{
			CloseSocket(wsaLastError, true);

			return HRESULT_FROM_WIN32(wsaLastError);
		}
	}

	return S_OK;
}

HRESULT ConnectionSocket::DisconnectSocket()
{
	if (m_socket == INVALID_SOCKET)
	{
		return E_NOT_VALID_STATE;
	}

	if (shutdown(m_socket, SD_SEND) == SOCKET_ERROR)
	{
		return WSAGetLastHRESULT();
	}

	return S_OK;
}

HRESULT ConnectionSocket::GetLastErrorAndCloseSocket()
{
	auto wsaLastError = WSAGetLastError();

	CloseSocket(wsaLastError, true);

	return HRESULT_FROM_WIN32(wsaLastError);
}

HRESULT ConnectionSocket::CloseSocket(int wsaError, boolean raiseEvent)
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
	m_socketEvent.Close();

	if (raiseEvent)
	{
		return OnSocketClosed(wsaError);
	}

	return S_OK;
}

HRESULT ConnectionSocket::OnSocketEvent(PTP_CALLBACK_INSTANCE callbackInstance, boolean* closed)
{
	int wsaLastError;
	WSANETWORKEVENTS networkEvents;
	if (WSAEnumNetworkEvents(m_socket, m_socketEvent.Get(), &networkEvents) == SOCKET_ERROR)
	{
		*closed = true;
		return GetLastErrorAndCloseSocket();
	}

	if (networkEvents.lNetworkEvents & FD_CLOSE)
	{
		*closed = true;
		return CloseSocket(networkEvents.iErrorCode[FD_CLOSE_BIT], true);
	}

	do
	{
		if (networkEvents.lNetworkEvents & FD_CONNECT)
		{
			BreakIfError(wsaLastError, networkEvents.iErrorCode[FD_CONNECT_BIT]);

			I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement socket connection event");
		}

		if (networkEvents.lNetworkEvents & FD_WRITE)
		{
			BreakIfError(wsaLastError, networkEvents.iErrorCode[FD_WRITE_BIT]);

			std::string requestBuffer("GET /?gfe_rd=cr&ei=GnEKWfHFIczw8Aeh7LDABQ&gws_rd=cr HTTP/1.1\nUser-Agent: Mozilla / 4.0 (compatible; MSIE5.01; Windows NT)\nHost: www.google.com\nAccept-Language: en-us\nConnection: Keep-Alive\n\n");

			int sentBytes;
			if ((sentBytes = send(m_socket, requestBuffer.data(), requestBuffer.size(), 0)) == SOCKET_ERROR)
			{
				if ((wsaLastError = WSAGetLastError()) != WSAEWOULDBLOCK)
				{
					break;
				}
			}

			I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement socket writing");
		}

		if (networkEvents.lNetworkEvents & FD_READ)
		{
			BreakIfError(wsaLastError, networkEvents.iErrorCode[FD_READ_BIT]);

			std::string responseBuffer(READ_BUFFER_SIZE, '\0');

			int receivedBytes;
			int totalReceivedBytes = 0;
			while ((receivedBytes = recv(m_socket, &responseBuffer[totalReceivedBytes], responseBuffer.size() - totalReceivedBytes, 0)) > 0)
			{
				totalReceivedBytes += receivedBytes;
			}

			if (receivedBytes == SOCKET_ERROR && (wsaLastError = WSAGetLastError()) != WSAEWOULDBLOCK)
			{
				break;
			}

			I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement socket reading");
		}

		return S_OK;
	} while (false);

	*closed = true;
	CloseSocket(wsaLastError, true);

	return HRESULT_FROM_WIN32(wsaLastError);
}