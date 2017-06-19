#include "pch.h"
#include "ConnectionManager.h"
#include "EventObject.h"
#include "Datacenter.h"
#include "Connection.h"
#include "Helpers\COMHelper.h"

using namespace Telegram::Api::Native;


ActivatableStaticOnlyFactory(ConnectionManagerStatics);


ConnectionManager::ConnectionManager() :
	m_connectionState(ConnectionState::NotInitialized),
	m_working(TRUE),
	m_isIpv6Enabled(false)
{
}

ConnectionManager::~ConnectionManager()
{
	InterlockedExchange8(&m_working, FALSE);

	m_workerThread.Join();

	WSACleanup();
}

HRESULT ConnectionManager::RuntimeClassInitialize()
{
	WSADATA wsaData;
	if (WSAStartup(MAKEWORD(2, 2), &wsaData) != NO_ERROR)
	{
		return GetWSALastHRESULT();
	}

	m_workerThread.Attach(CreateThread(nullptr, 0, ConnectionManager::WorkerThread, reinterpret_cast<LPVOID>(this), 0, nullptr));
	if (!m_workerThread.IsValid())
	{
		return GetLastHRESULT();
	}

	return S_OK;
}

HRESULT ConnectionManager::get_ConnectionState(ConnectionState* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = m_criticalSection.Lock();

	*value = m_connectionState;
	return S_OK;
}

HRESULT ConnectionManager::get_IsIpv6Enabled(boolean* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = m_criticalSection.Lock();

	*value = m_isIpv6Enabled;
	return S_OK;
}

HRESULT ConnectionManager::SendRequest(ITLObject* object, UINT32 datacenterId, ConnectionType connetionType, boolean immediate, UINT32* requestToken)
{
	if (object == nullptr || requestToken == nullptr)
	{
		return E_POINTER;
	}

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

HRESULT ConnectionManager::OnConnectionOpened(Connection* connection)
{
	HRESULT result;
	auto lock = m_criticalSection.Lock();

	ComPtr<IDatacenter> datacenter;
	ReturnIfFailed(result, connection->get_Datacenter(&datacenter));

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

HRESULT ConnectionManager::OnConnectionDataReceived(Connection* connection)
{
	HRESULT result;
	auto lock = m_criticalSection.Lock();

	ComPtr<IDatacenter> datacenter;
	ReturnIfFailed(result, connection->get_Datacenter(&datacenter));

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

HRESULT ConnectionManager::OnConnectionClosed(Connection* connection)
{
	HRESULT result;
	auto lock = m_criticalSection.Lock();

	ComPtr<IDatacenter> datacenter;
	ReturnIfFailed(result, connection->get_Datacenter(&datacenter));

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

DWORD ConnectionManager::WorkerThread(LPVOID parameter)
{
	ComPtr<ConnectionManager> connectionManager = reinterpret_cast<ConnectionManager*>(parameter);

	while (InterlockedAnd8(&connectionManager->m_working, TRUE) == TRUE)
	{
		I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");
	}

	return NO_ERROR;
}


ComPtr<ConnectionManager> ConnectionManagerStatics::s_instance = nullptr;

ConnectionManagerStatics::ConnectionManagerStatics()
{
}

ConnectionManagerStatics::~ConnectionManagerStatics()
{
}

HRESULT ConnectionManagerStatics::get_Instance(IConnectionManager** value)
{
	return ConnectionManagerStatics::GetInstance(value);
}

HRESULT ConnectionManagerStatics::GetInstance(IConnectionManager** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	if (s_instance == nullptr)
	{
		HRESULT result;
		ReturnIfFailed(result, MakeAndInitialize<ConnectionManager>(&s_instance));
	}

	return s_instance.CopyTo(value);
}