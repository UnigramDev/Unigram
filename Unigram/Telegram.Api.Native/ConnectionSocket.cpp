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

#define SEND_BUFFER_SIZE 0
#define RECEIVE_BUFFER_SIZE 1024 * 128

using namespace Telegram::Api::Native;


ConnectionSocket::ConnectionSocket() :
	m_socket(INVALID_SOCKET)
{
}

ConnectionSocket::~ConnectionSocket()
{
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

	m_sendBuffer.resize(SEND_BUFFER_SIZE);
	m_receiveBuffer.resize(RECEIVE_BUFFER_SIZE);

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
		CloseSocket(WIN32_FROM_HRESULT(result), true);

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

HRESULT ConnectionSocket::CloseSocket()
{
	return CloseSocket(NO_ERROR, false);
}

HRESULT ConnectionSocket::SendData(BYTE const* buffer, UINT32 length)
{
	if (m_socket == INVALID_SOCKET)
	{
		return E_NOT_VALID_STATE;
	}

	int bytesSent = send(m_socket, reinterpret_cast<const char*>(buffer), length, 0);
	if (bytesSent == SOCKET_ERROR)
	{
		auto wsaLastError = WSAGetLastError();
		if (wsaLastError != WSAEWOULDBLOCK)
		{
			return HRESULT_FROM_WIN32(wsaLastError);
		}
	}
	else if (bytesSent < length)
	{
		auto remainingSize = length - bytesSent;
		auto availableSize = m_sendBuffer.size();

		m_sendBuffer.resize(availableSize + remainingSize);
		CopyMemory(m_sendBuffer.data() + availableSize, buffer, remainingSize);
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
	m_sendBuffer = {};
	m_receiveBuffer = {};

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

			HRESULT result;
			if (FAILED(result = OnSocketConnected()))
			{
				*closed = true;
				CloseSocket(WIN32_FROM_HRESULT(result), true);

				return result;
			}
		}

		if (networkEvents.lNetworkEvents & FD_WRITE)
		{
			BreakIfError(wsaLastError, networkEvents.iErrorCode[FD_WRITE_BIT]);

			auto availableBytes = m_sendBuffer.size();
			if (availableBytes > 0)
			{
				int sentBytes;
				if ((sentBytes = send(m_socket, reinterpret_cast<char*>(m_sendBuffer.data()), availableBytes, 0)) == SOCKET_ERROR)
				{
					if ((wsaLastError = WSAGetLastError()) != WSAEWOULDBLOCK)
					{
						break;
					}
				}

				auto remainingBytes = availableBytes - sentBytes;
				MoveMemory(m_sendBuffer.data(), m_sendBuffer.data() + sentBytes, remainingBytes);

				m_sendBuffer.resize(remainingBytes);
			}
		}

		if (networkEvents.lNetworkEvents & FD_READ)
		{
			BreakIfError(wsaLastError, networkEvents.iErrorCode[FD_READ_BIT]);

			HRESULT result;
			int receivedBytes;
			while ((receivedBytes = recv(m_socket, reinterpret_cast<char*>(m_receiveBuffer.data()), RECEIVE_BUFFER_SIZE, 0)) > 0)
			{
				if (FAILED(result = OnDataReceived(m_receiveBuffer.data(), receivedBytes)))
				{
					*closed = true;
					CloseSocket(WIN32_FROM_HRESULT(result), true);

					return result;
				}
			}

			if (receivedBytes == SOCKET_ERROR && (wsaLastError = WSAGetLastError()) != WSAEWOULDBLOCK)
			{
				break;
			}
		}

		return S_OK;
	} while (false);

	*closed = true;
	CloseSocket(wsaLastError, true);

	return HRESULT_FROM_WIN32(wsaLastError);
}