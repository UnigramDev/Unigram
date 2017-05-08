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
	m_threadpool(nullptr),
	m_threadpoolCleanupGroup(nullptr),
	m_isIpv6Enabled(false)
{
}

ConnectionManager::~ConnectionManager()
{
	if (m_threadpoolCleanupGroup != nullptr)
	{
		CloseThreadpoolCleanupGroupMembers(m_threadpoolCleanupGroup, TRUE, nullptr);
		CloseThreadpoolCleanupGroup(m_threadpoolCleanupGroup);
	}

	if (m_threadpool != nullptr)
	{
		CloseThreadpool(m_threadpool);
	}

	DestroyThreadpoolEnvironment(&m_threadpoolEnvironment);

	WSACleanup();
}

HRESULT ConnectionManager::RuntimeClassInitialize(DWORD minimumThreadCount, DWORD maximumThreadCount)
{
	if (minimumThreadCount == 0 || minimumThreadCount > maximumThreadCount)
	{
		return E_INVALIDARG;
	}

	WSADATA wsaData;
	if (WSAStartup(MAKEWORD(2, 2), &wsaData) != NO_ERROR)
	{
		return WSAGetLastHRESULT();
	}

	InitializeThreadpoolEnvironment(&m_threadpoolEnvironment);

	m_threadpool = CreateThreadpool(nullptr);
	if (m_threadpool == nullptr)
	{
		return GetLastHRESULT();
	}

	SetThreadpoolThreadMaximum(m_threadpool, maximumThreadCount);
	if (!SetThreadpoolThreadMinimum(m_threadpool, minimumThreadCount))
	{
		return GetLastHRESULT();
	}

	m_threadpoolCleanupGroup = CreateThreadpoolCleanupGroup();
	if (m_threadpoolCleanupGroup == nullptr)
	{
		return GetLastHRESULT();
	}

	SetThreadpoolCallbackPool(&m_threadpoolEnvironment, m_threadpool);
	SetThreadpoolCallbackCleanupGroup(&m_threadpoolEnvironment, m_threadpoolCleanupGroup, nullptr);

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

HRESULT ConnectionManager::OnConnectionQuickAckReceived(Connection* connection, INT32 ack)
{
	HRESULT result;
	auto lock = m_criticalSection.Lock();

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


HRESULT ConnectionManager::BoomBaby()
{
	HRESULT result;
	ComPtr<Datacenter> datacenter;
	ReturnIfFailed(result, MakeAndInitialize<Datacenter>(&datacenter, 0));

	ComPtr<Connection> connection;
	ReturnIfFailed(result, datacenter->GetGenericConnection(true, &connection));
	ReturnIfFailed(result, connection->AttachToThreadpool(&m_threadpoolEnvironment));
	ReturnIfFailed(result, connection->Connect());

	return S_OK;
}

void ConnectionManager::OnEventObjectError(EventObject const* eventObject, HRESULT error)
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement EventObject callback error tracing");
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