#include "pch.h"
#include "Connection.h"
#include "Datacenter.h"
#include "ConnectionManager.h"

using namespace Telegram::Api::Native;


Connection::Connection() :
	m_token(0),
	m_type(ConnectionType::Generic),
	m_currentNetworkType(ConnectionNeworkType::WiFi)
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

	auto lock = m_criticalSection.Lock();

	*value = m_token;
	return S_OK;
}

HRESULT Connection::get_Datacenter(IDatacenter** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = m_criticalSection.Lock();
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

	auto lock = m_criticalSection.Lock();

	*value = m_currentNetworkType;
	return S_OK;
}

HRESULT Connection::Connect()
{
	HRESULT result;
	auto lock = m_criticalSection.Lock();

	ComPtr<ConnectionManager> connectionManager;
	ReturnIfFailed(result, ConnectionManagerStatics::GetInstance(connectionManager));


	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement connection start");

	boolean ipv6;
	ReturnIfFailed(result, connectionManager->get_IsIpv6Enabled(&ipv6));

	Datacenter::DatacenterEndpoint* endpoint;
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

	ReturnIfFailed(result, OpenSocket(endpoint->Address, endpoint->Port, ipv6));
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
	auto lock = m_criticalSection.Lock();

	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement connection suspension");

	ReturnIfFailed(result, m_reconnectionTimer->Stop());

	return S_OK;
}

HRESULT Connection::OnSocketOpened()
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement socket connection opened handling");

	return S_OK;
}

HRESULT Connection::OnDataReceived()
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement socket connection data received handling");

	return S_OK;
}

HRESULT Connection::OnSocketClosed()
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement socket connection closed handling");

	return S_OK;
}