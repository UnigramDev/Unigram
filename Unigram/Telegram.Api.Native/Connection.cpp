#include "pch.h"
#include "Connection.h"
#include "Datacenter.h"
#include "NativeBuffer.h"
#include "TLBinaryWriter.h"
#include "ConnectionManager.h"

#define CONNECTION_MAX_ATTEMPTS 5 

using namespace Telegram::Api::Native;
using namespace Telegram::Api::Native::TL;


Connection::Connection(Datacenter* datacenter, ConnectionType type) :
	m_token(0),
	m_type(type),
	m_datacenter(datacenter),
	m_currentNetworkType(ConnectionNeworkType::WiFi),
	m_failedConnectionCount(0),
	m_connectionAttemptCount(CONNECTION_MAX_ATTEMPTS)
{
	m_reconnectionTimer = Make<Timer>([&]
	{
		return Connect();
	});
}

Connection::~Connection()
{
}

HRESULT Connection::get_Token(UINT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();

	*value = m_token;
	return S_OK;
}

HRESULT Connection::get_Datacenter(IDatacenter** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();
	return m_datacenter.CopyTo(value);
}

HRESULT Connection::get_Type(ConnectionType* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_type;
	return S_OK;
}

HRESULT Connection::get_CurrentNetworkType(ConnectionNeworkType* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();

	*value = m_currentNetworkType;
	return S_OK;
}

HRESULT Connection::get_SessionId(INT64* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = ConnectionSession::GetSessionId();
	return S_OK;
}

HRESULT Connection::Connect()
{
	HRESULT result;
	auto lock = LockCriticalSection();

	ComPtr<ConnectionManager> connectionManager;
	ReturnIfFailed(result, ConnectionManager::GetInstance(connectionManager));

	if (!connectionManager->IsNetworkAvailable())
	{
		connectionManager->OnConnectionClosed(this);

		return HRESULT_FROM_WIN32(ERROR_NO_NETWORK);
	}

	//I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement connection start");

	/*Datacenter::DatacenterEndpoint* endpoint;
	if (FAILED(result = m_datacenter->GetCurrentEndpoint(m_type, ipv6, &endpoint)) && ipv6)
	{
		ipv6 = false;
		ReturnIfFailed(result, m_datacenter->GetCurrentEndpoint(m_type, false, &endpoint));
	}
	else
	{
		return result;
	}

	ReturnIfFailed(result, m_reconnectionTimer->Stop());
	ReturnIfFailed(result, OpenSocket(endpoint->Address, endpoint->Port, ipv6));*/

	ReturnIfFailed(result, m_reconnectionTimer->Stop());
	ReturnIfFailed(result, ConnectionSocket::ConnectSocket(connectionManager.Get(), L"172.217.23.68", 80));
	ReturnIfFailed(result, connectionManager->get_CurrentNetworkType(&m_currentNetworkType));

	return S_OK;
}

HRESULT Connection::Reconnect()
{
	HRESULT result;
	ReturnIfFailed(result, Suspend());

	return Connect();
}

HRESULT Connection::Suspend()
{
	HRESULT result;
	auto lock = LockCriticalSection();

	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement connection suspension");

	ReturnIfFailed(result, m_reconnectionTimer->Stop());

	return S_OK;
}

HRESULT Connection::SendData(BYTE* buffer, UINT32 length, boolean reportAck)
{
	if (buffer == nullptr)
	{
		return E_INVALIDARG;
	}

	LockCriticalSection();

	HRESULT result;
	UINT32 packetBufferLength = 0;
	UINT32 packetLength = length / 4;

	if (packetLength < 0x7f)
	{
		packetBufferLength++;
	}
	else
	{
		packetBufferLength += 4;
	}

	BYTE* packetBufferBytes;
	std::unique_ptr<BYTE[]> packetBuffer;
	if (ConnectionCryptograpy::IsInitialized())
	{
		packetBuffer = std::make_unique<BYTE[]>(packetBufferLength);
		packetBufferBytes = packetBuffer.get();
	}
	else
	{
		packetBufferLength += 64;
		packetBuffer = std::make_unique<BYTE[]>(packetBufferLength);
		packetBufferBytes = packetBuffer.get() + 64;

		ReturnIfFailed(result, ConnectionCryptograpy::Initialize(packetBuffer.get()));
	}

	if (packetLength < 0x7f)
	{
		if (reportAck)
		{
			packetLength |= (1 << 7);
		}

		packetBufferBytes[0] = packetLength & 0xff;

		ConnectionCryptograpy::EncryptBuffer(packetBufferBytes, packetBufferBytes, 1);

		packetBufferBytes += 1;
	}
	else
	{
		packetLength = (packetLength << 8) + 0x7f;

		if (reportAck)
		{
			packetLength |= (1 << 7);
		}

		packetBufferBytes[0] = packetLength & 0xff;
		packetBufferBytes[1] = (packetLength >> 8) & 0xff;
		packetBufferBytes[2] = (packetLength >> 16) & 0xff;
		packetBufferBytes[3] = (packetLength >> 24) & 0xff;

		ConnectionCryptograpy::EncryptBuffer(packetBufferBytes, packetBufferBytes, 4);

		packetBufferBytes += 4;
	}

	ConnectionCryptograpy::EncryptBuffer(buffer, buffer, length);

	ReturnIfFailed(result, ConnectionSocket::SendData(packetBuffer.get(), packetBufferLength));
	return ConnectionSocket::SendData(buffer, length);
}

HRESULT Connection::OnSocketConnected()
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement socket connected event handling");

	//HRESULT result;
	//ComPtr<ConnectionManager> connectionManager;
	//ReturnIfFailed(result, ConnectionManager::GetInstance(connectionManager));
	//ReturnIfFailed(result, connectionManager->OnConnectionOpened(this));

	//std::string requestBuffer("GET /?gfe_rd=cr&ei=GnEKWfHFIczw8Aeh7LDABQ&gws_rd=cr HTTP/1.1\n"
	//	"User-Agent: Mozilla / 4.0 (compatible; MSIE5.01; Windows NT)\nHost: www.google.com\nAccept-Language: en-us\nConnection: Keep-Alive\n\nSTOCAZZO h@çk3r");

	//ReturnIfFailed(result, ConnectionSocket::SendData(reinterpret_cast<const BYTE*>(requestBuffer.data()), static_cast<UINT32>(requestBuffer.size())));
	return S_OK;
}

HRESULT Connection::OnDataReceived(BYTE const* buffer, UINT32 length)
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement socket data received event handling");

	OutputDebugStringA(reinterpret_cast<const char*>(buffer));

	return S_OK;
}

HRESULT Connection::OnSocketDisconnected(int wsaError)
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement socket disconnected event handling");

	WCHAR* errorMessage;
	FormatMessage(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS, NULL, wsaError, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), reinterpret_cast<LPWSTR>(&errorMessage), 0, NULL);
	OutputDebugString(errorMessage);
	LocalFree(errorMessage);

	HRESULT result;
	ReturnIfFailed(result, m_reconnectionTimer->Stop());

	ComPtr<ConnectionManager> connectionManager;
	ReturnIfFailed(result, ConnectionManager::GetInstance(connectionManager));
	ReturnIfFailed(result, connectionManager->OnConnectionClosed(this));

	return S_OK;
}