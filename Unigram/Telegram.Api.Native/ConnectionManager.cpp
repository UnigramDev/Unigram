#include "pch.h"
#include "ConnectionManager.h"
#include "EventObject.h"
#include "Datacenter.h"
#include "Connection.h"
#include "Helpers\COMHelper.h"

using namespace Telegram::Api::Native;


ActivatableStaticOnlyFactory(ConnectionManagerStatics);


ConnectionManager::ConnectionManager() :
	m_connectionState(ConnectionState::Connecting),
	m_currentNetworkType(ConnectionNeworkType::WiFi),
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

HRESULT ConnectionManager::get_CurrentNetworkType(ConnectionNeworkType* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = m_criticalSection.Lock();

	*value = m_currentNetworkType;
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

HRESULT ConnectionManager::SendRequest(ITLObject* object, UINT32 datacenterId, ConnectionType connetionType, boolean immediate, INT32* requestToken)
{
	if (object == nullptr || requestToken == nullptr)
	{
		return E_POINTER;
	}

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

HRESULT ConnectionManager::CancelRequest(INT32 requestToken, boolean notifyServer)
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

HRESULT ConnectionManager::OnConnectionOpened(Connection* connection)
{
	HRESULT result;
	auto lock = m_criticalSection.Lock();

	auto datacenter = connection->GetDatacenter();

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

HRESULT ConnectionManager::OnConnectionDataReceived(Connection* connection)
{
	HRESULT result;
	auto lock = m_criticalSection.Lock();

	auto datacenter = connection->GetDatacenter();

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

HRESULT ConnectionManager::OnConnectionClosed(Connection* connection)
{
	HRESULT result;
	auto lock = m_criticalSection.Lock();

	auto datacenter = connection->GetDatacenter();

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
	if (value == nullptr)
	{
		return E_POINTER;
	}

	HRESULT result;
	ComPtr<ConnectionManager> connectionManager;
	ReturnIfFailed(result, ConnectionManagerStatics::GetInstance(connectionManager));

	*value = connectionManager.Detach();
	return S_OK;
}

HRESULT ConnectionManagerStatics::GetInstance(ComPtr<ConnectionManager>& value)
{
	if (s_instance == nullptr)
	{
		HRESULT result;
		ReturnIfFailed(result, MakeAndInitialize<ConnectionManager>(&s_instance));
	}
	
	value = s_instance;
	return S_OK;
}