#include "pch.h"
#include "Connection.h"
#include "Datacenter.h"
#include "ConnectionManager.h"

#define CONNECTION_MAX_ATTEMPTS 5 

using namespace Telegram::Api::Native;


Connection::Connection() :
	m_token(0),
	m_type(ConnectionType::Generic),
	m_currentNetworkType(ConnectionNeworkType::WiFi),
	m_failedConnectionCount(0),
	m_connectionAttemptCount(CONNECTION_MAX_ATTEMPTS)
{
}

Connection::~Connection()
{
}

HRESULT Connection::RuntimeClassInitialize(Datacenter* datacenter, ConnectionType type)
{
	HRESULT result;
	ReturnIfFailed(result, MakeAndInitialize<Timer>(&m_reconnectionTimer, [&]
	{
		return Connect();
	}));

	m_datacenter = datacenter;
	m_type = type;

	return S_OK;
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

HRESULT Connection::get_Datacenter(ABI::Telegram::Api::Native::IDatacenter** value)
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

	*value = GetSessionId();
	return S_OK;
}

HRESULT Connection::Connect()
{
	HRESULT result;
	auto lock = LockCriticalSection();

	ComPtr<ConnectionManager> connectionManager;
	ReturnIfFailed(result, ConnectionManager::GetInstance(connectionManager));

	//I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement connection start");

	boolean ipv6;
	ReturnIfFailed(result, connectionManager->get_IsIpv6Enabled(&ipv6));

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

	ReturnIfFailed(result, ConnectSocket(L"172.217.23.68", 80, false));

	ReturnIfFailed(result, connectionManager->get_CurrentNetworkType(&m_currentNetworkType));

	/*Sleep(10000);

	{
		auto lock = m_criticalSection.Lock();
		ReturnIfFailed(result, DisconnectSocket());
	}

	Sleep(10000);

	{
		auto lock = m_criticalSection.Lock();
		ReturnIfFailed(result, ConnectSocket(L"172.217.23.68", 80, false));
	}*/

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

HRESULT Connection::Close()
{
	HRESULT result;
	auto lock = LockCriticalSection();

	/*if (m_closed)
	{
		return RO_E_CLOSED;
	}*/

	m_datacenter.Reset();

	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement connection disposal to avoid circular reference");

	return S_OK;
}

HRESULT Connection::OnSocketCreated()
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement socket created event handling");

	return S_OK;
}

HRESULT Connection::OnSocketConnected()
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement socket connected event handling");

	HRESULT result;
	std::string requestBuffer("GET /?gfe_rd=cr&ei=GnEKWfHFIczw8Aeh7LDABQ&gws_rd=cr HTTP/1.1\n"
		"User-Agent: Mozilla / 4.0 (compatible; MSIE5.01; Windows NT)\nHost: www.google.com\nAccept-Language: en-us\nConnection: Keep-Alive\n\nSTOCAZZO h@çk3r");

	ReturnIfFailed(result, SendData(reinterpret_cast<const BYTE*>(requestBuffer.data()), static_cast<UINT32>(requestBuffer.size())));
	return S_OK;
}

HRESULT Connection::OnDataReceived(BYTE const* buffer, UINT32 length)
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement socket data received event handling");

	OutputDebugStringA(reinterpret_cast<const char*>(buffer));

	return S_OK;
}

HRESULT Connection::OnSocketDisconnected()
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement socket disconnected event handling");

	return S_OK;
}

HRESULT Connection::OnSocketClosed(int wsaError)
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement socket closed event handling");

	return S_OK;
}