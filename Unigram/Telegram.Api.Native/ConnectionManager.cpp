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
	m_isIpv6Enabled(false),
	m_currentDatacenterId(0),
	m_timeDelta(0),
	m_lastOutgoingMessageId(0)
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

	auto lock = LockCriticalSection();

	*value = m_connectionState;
	return S_OK;
}

HRESULT ConnectionManager::get_CurrentNetworkType(ConnectionNeworkType* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();

	*value = m_currentNetworkType;
	return S_OK;
}

HRESULT ConnectionManager::get_IsIpv6Enabled(boolean* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();

	*value = m_isIpv6Enabled;
	return S_OK;
}

HRESULT ConnectionManager::get_IsNetworkAvailable(boolean* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();

	*value = true;
	return S_OK;
}

HRESULT ConnectionManager::SendRequest(ITLObject* object, UINT32 datacenterId, ConnectionType connetionType, boolean immediate, INT32* requestToken)
{
	if (object == nullptr || requestToken == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

HRESULT ConnectionManager::CancelRequest(INT32 requestToken, boolean notifyServer)
{
	auto lock = LockCriticalSection();

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

HRESULT ConnectionManager::GetDatacenterById(UINT32 id, IDatacenter** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();

	if (id == DEFAULT_DATACENTER_ID)
	{
		return m_datacenters[m_currentDatacenterId].CopyTo(value);
	}

	auto& datacenter = m_datacenters.find(id);
	if (datacenter == m_datacenters.end())
	{
		return E_INVALIDARG;
	}

	return datacenter->second.CopyTo(value);
}

HRESULT ConnectionManager::OnConnectionOpened(Connection* connection)
{
	HRESULT result;
	auto lock = LockCriticalSection();

	auto datacenter = connection->GetDatacenter();

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

HRESULT ConnectionManager::OnConnectionDataReceived(Connection* connection)
{
	HRESULT result;
	auto lock = LockCriticalSection();

	auto datacenter = connection->GetDatacenter();

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

HRESULT ConnectionManager::OnConnectionQuickAckReceived(Connection* connection, INT32 ack)
{
	HRESULT result;
	auto lock = LockCriticalSection();

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

HRESULT ConnectionManager::OnConnectionClosed(Connection* connection)
{
	HRESULT result;
	auto lock = LockCriticalSection();

	auto datacenter = connection->GetDatacenter();

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}


HRESULT ConnectionManager::BoomBaby(_Out_ IConnection** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	HRESULT result;
	ComPtr<Datacenter> datacenter;
	ReturnIfFailed(result, MakeAndInitialize<Datacenter>(&datacenter, 0));

	ReturnIfFailed(result, datacenter->AddEndpoint(L"192.168.1.1", 80, ConnectionType::Generic, false));

	ComPtr<Connection> connection;
	ReturnIfFailed(result, datacenter->GetGenericConnection(true, &connection));
	ReturnIfFailed(result, connection->AttachToThreadpool(&m_threadpoolEnvironment));
	ReturnIfFailed(result, connection->Connect());

	*value = connection.Detach();
	return S_OK;
}

void ConnectionManager::OnEventObjectError(EventObject const* eventObject, HRESULT error)
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement EventObject callback error tracing");
}

INT64 ConnectionManager::GenerateMessageId()
{
	auto lock = LockCriticalSection();

	auto messageId = static_cast<INT64>(((static_cast<double>(GetCurrentRealTime()) + static_cast<double>(m_timeDelta) * 1000) * 4294967296.0) / 1000.0);
	if (messageId <= m_lastOutgoingMessageId)
	{
		messageId = m_lastOutgoingMessageId + 1;
	}

	while ((messageId % 4) != 0)
	{
		messageId++;
	}

	m_lastOutgoingMessageId = messageId;
	return messageId;
}

HRESULT ConnectionManager::GetInstance(ComPtr<ConnectionManager>& value)
{
	if (ConnectionManagerStatics::s_instance == nullptr)
	{
		HRESULT result;
		ReturnIfFailed(result, MakeAndInitialize<ConnectionManager>(&ConnectionManagerStatics::s_instance));
	}

	value = ConnectionManagerStatics::s_instance;
	return S_OK;
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
	ReturnIfFailed(result, ConnectionManager::GetInstance(connectionManager));

	*value = connectionManager.Detach();
	return S_OK;
}