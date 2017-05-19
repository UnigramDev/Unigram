#include "pch.h"
#include <algorithm>
#include <Ws2tcpip.h>
#include "Datacenter.h"
#include "TLBinaryReader.h"
#include "TLBinaryWriter.h"
#include "Connection.h"
#include "ConnectionManager.h"
#include "TLProtocolScheme.h"
#include "Helpers\COMHelper.h"

using namespace Telegram::Api::Native;
using namespace Telegram::Api::Native::TL;


Datacenter::Datacenter(UINT32 id) :
	m_id(id),
	m_currentIpv4EndpointIndex(0),
	m_currentIpv4DownloadEndpointIndex(0),
	m_currentIpv6EndpointIndex(0),
	m_currentIpv6DownloadEndpointIndex(0)
{
}

Datacenter::Datacenter() :
	Datacenter(0)
{
}

Datacenter::~Datacenter()
{
}

HRESULT Datacenter::get_Id(UINT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_id;
	return S_OK;
}

HRESULT Datacenter::get_HandshakeState(HandshakeState* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();

	if (m_handshakeContext == nullptr)
	{
		*value = HandshakeState::None;
	}
	else
	{
		*value = m_handshakeContext->State;
	}

	return S_OK;
}

HRESULT Datacenter::get_ServerSalt(INT64* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	HRESULT result;
	ComPtr<ConnectionManager> connectionManager;
	ReturnIfFailed(result, ConnectionManager::GetInstance(connectionManager));

	auto lock = LockCriticalSection();

	INT32 maxOffset = -1;
	INT64 salt = 0;
	std::vector<size_t> saltsToRemove;
	auto timeStamp = connectionManager->GetCurrentTime();

	for (size_t i = 0; i < m_serverSalts.size(); i++)
	{
		auto& serverSalt = m_serverSalts[i];

		if (serverSalt.ValidUntil < timeStamp)
		{
			saltsToRemove.push_back(i);
		}
		else if (serverSalt.ValidSince <= timeStamp && serverSalt.ValidUntil > timeStamp)
		{
			auto currentOffset = std::abs(serverSalt.ValidUntil - timeStamp);
			if (currentOffset > maxOffset)
			{
				maxOffset = currentOffset;
				salt = serverSalt.Salt;
			}
		}
	}

	for (size_t i = 0; i < saltsToRemove.size(); i++)
	{
		m_serverSalts.erase(m_serverSalts.begin() + saltsToRemove[i]);
	}

	*value = salt;
	return S_OK;
}

HRESULT Datacenter::GetCurrentAddress(ConnectionType connectionType, boolean ipv6, HSTRING* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	HRESULT result;
	ServerEndpoint* endpoint;
	ReturnIfFailed(result, GetCurrentEndpoint(connectionType, ipv6, &endpoint));

	return WindowsCreateString(endpoint->Address, value);
}

HRESULT Datacenter::GetCurrentPort(ConnectionType connectionType, boolean ipv6, UINT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	HRESULT result;
	ServerEndpoint* endpoint;
	ReturnIfFailed(result, GetCurrentEndpoint(connectionType, ipv6, &endpoint));

	*value = endpoint->Port;
	return S_OK;
}

HRESULT Datacenter::Close()
{
	auto lock = LockCriticalSection();

	/*if (m_closed)
	{
		return RO_E_CLOSED;
	}*/

	m_genericConnection.Reset();

	for (size_t i = 0; i < UPLOAD_CONNECTIONS_COUNT; i++)
	{
		m_uploadConnections[i].Reset();
	}

	for (size_t i = 0; i < DOWNLOAD_CONNECTIONS_COUNT; i++)
	{
		m_downloadConnections[i].Reset();
	}

	return S_OK;
}

void Datacenter::SwitchTo443Port()
{
	auto lock = LockCriticalSection();

	for (size_t i = 0; i < m_ipv4Endpoints.size(); i++)
	{
		if (m_ipv4Endpoints[i].Port == 443)
		{
			m_currentIpv4EndpointIndex = i;
			break;
		}
	}

	for (size_t i = 0; i < m_ipv4DownloadEndpoints.size(); i++)
	{
		if (m_ipv4DownloadEndpoints[i].Port == 443)
		{
			m_currentIpv4DownloadEndpointIndex = i;
			break;
		}
	}

	for (size_t i = 0; i < m_ipv6Endpoints.size(); i++)
	{
		if (m_ipv6Endpoints[i].Port == 443)
		{
			m_currentIpv6EndpointIndex = i;
			break;
		}
	}

	for (size_t i = 0; i < m_ipv6DownloadEndpoints.size(); i++)
	{
		if (m_ipv6DownloadEndpoints[i].Port == 443)
		{
			m_currentIpv6DownloadEndpointIndex = i;
			break;
		}
	}
}

void Datacenter::RecreateSessions()
{
	auto lock = LockCriticalSection();

	if (m_genericConnection != nullptr)
	{
		m_genericConnection->RecreateSession();
	}

	for (size_t i = 0; i < UPLOAD_CONNECTIONS_COUNT; i++)
	{
		if (m_uploadConnections[i] != nullptr)
		{
			m_uploadConnections[i]->RecreateSession();
		}
	}

	for (size_t i = 0; i < DOWNLOAD_CONNECTIONS_COUNT; i++)
	{
		if (m_downloadConnections[i] != nullptr)
		{
			m_downloadConnections[i]->RecreateSession();
		}
	}
}

void Datacenter::GetSessionsIds(std::vector<INT64>& sessionIds)
{
	auto lock = LockCriticalSection();

	if (m_genericConnection != nullptr)
	{
		sessionIds.push_back(m_genericConnection->GetSessionId());
	}

	for (size_t i = 0; i < UPLOAD_CONNECTIONS_COUNT; i++)
	{
		if (m_uploadConnections[i] != nullptr)
		{
			sessionIds.push_back(m_uploadConnections[i]->GetSessionId());
		}
	}

	for (size_t i = 0; i < DOWNLOAD_CONNECTIONS_COUNT; i++)
	{
		if (m_downloadConnections[i] != nullptr)
		{
			sessionIds.push_back(m_downloadConnections[i]->GetSessionId());
		}
	}
}

void Datacenter::NextEndpoint(ConnectionType connectionType, boolean ipv6)
{
	auto lock = LockCriticalSection();

	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement Datacenter next endpoint switching");
}

void Datacenter::ResetEndpoint()
{
	auto lock = LockCriticalSection();

	m_currentIpv4EndpointIndex = 0;
	m_currentIpv4DownloadEndpointIndex = 0;
	m_currentIpv6EndpointIndex = 0;
	m_currentIpv6DownloadEndpointIndex = 0;

	//StoreCurrentEndpoint();
}

HRESULT Datacenter::AddServerSalt(ServerSalt const& salt)
{
	auto lock = LockCriticalSection();

	if (ContainsServerSalt(salt.Salt, m_serverSalts.size()))
	{
		return PLA_E_NO_DUPLICATES;
	}

	m_serverSalts.push_back(salt);

	std::sort(m_serverSalts.begin(), m_serverSalts.end(), [](ServerSalt const& x, ServerSalt const& y)
	{
		return x.ValidSince < y.ValidSince;
	});

	return S_OK;
}

HRESULT Datacenter::MergeServerSalts(std::vector<ServerSalt> const& salts)
{
	if (salts.empty())
	{
		return S_OK;
	}

	HRESULT result;
	ComPtr<ConnectionManager> connectionManager;
	ReturnIfFailed(result, ConnectionManager::GetInstance(connectionManager));

	auto lock = LockCriticalSection();
	auto serverSaltCount = m_serverSalts.size();
	auto timeStamp = connectionManager->GetCurrentTime();

	for (size_t i = 0; i < salts.size(); i++)
	{
		auto& serverSalt = salts[i];

		if (serverSalt.ValidUntil > timeStamp && !ContainsServerSalt(serverSalt.Salt, serverSaltCount))
		{
			m_serverSalts.push_back(serverSalt);
		}
	}

	if (m_serverSalts.size() > serverSaltCount)
	{
		std::sort(m_serverSalts.begin(), m_serverSalts.end(), [](ServerSalt const& x, ServerSalt const& y)
		{
			return x.ValidSince < y.ValidSince;
		});
	}

	return S_OK;
}

boolean Datacenter::ContainsServerSalt(INT64 salt, size_t count)
{
	for (size_t i = 0; i < count; i++)
	{
		if (m_serverSalts[i].Salt == salt)
		{
			return true;
		}
	}

	return false;
}

boolean Datacenter::ContainsServerSalt(INT64 salt)
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Check if CriticalSection is really required");

	auto lock = LockCriticalSection();
	return ContainsServerSalt(salt, m_serverSalts.size());
}

void Datacenter::ClearServerSalts()
{
	auto lock = LockCriticalSection();

	m_serverSalts.clear();
}

HRESULT Datacenter::AddEndpoint(ServerEndpoint const& endpoint, ConnectionType connectionType, boolean ipv6)
{
#if _DEBUG
	ADDRINFOW* addressInfo;
	if (GetAddrInfo(endpoint.Address.data(), nullptr, nullptr, &addressInfo) != NO_ERROR)
	{
		return WS_E_ENDPOINT_NOT_FOUND;
	}

	FreeAddrInfo(addressInfo);
#endif

	auto lock = LockCriticalSection();

	HRESULT result;
	std::vector<ServerEndpoint>* endpoints;
	ReturnIfFailed(result, GetEndpointsForConnectionType(connectionType, ipv6, &endpoints));

	for (size_t i = 0; i < endpoints->size(); i++)
	{
		if ((*endpoints)[i].Address.compare(endpoint.Address) == 0) // && endpoint.Port == port)
		{
			return PLA_E_NO_DUPLICATES;
		}
	}

	endpoints->push_back(endpoint);
	return S_OK;
}

HRESULT Datacenter::ReplaceEndpoints(std::vector<ServerEndpoint> const& newEndpoints, ConnectionType connectionType, boolean ipv6)
{
#if _DEBUG
	for (size_t i = 0; i < newEndpoints.size(); i++)
	{
		ADDRINFOW* addressInfo;
		if (GetAddrInfo(newEndpoints[i].Address.data(), nullptr, nullptr, &addressInfo) != NO_ERROR)
		{
			return WS_E_ENDPOINT_NOT_FOUND;
		}

		FreeAddrInfo(addressInfo);
	}
#endif

	auto lock = LockCriticalSection();

	HRESULT result;
	std::vector<ServerEndpoint>* endpoints;
	ReturnIfFailed(result, GetEndpointsForConnectionType(connectionType, ipv6, &endpoints));

	*endpoints = newEndpoints;
	return S_OK;
}

HRESULT Datacenter::GetDownloadConnection(UINT32 index, boolean create, Connection** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	if (index >= DOWNLOAD_CONNECTIONS_COUNT)
	{
		return E_BOUNDS;
	}

	auto lock = LockCriticalSection();

	if (m_downloadConnections[index] == nullptr && create)
	{
		auto connection = Make<Connection>(this, ConnectionType::Download);

		HRESULT result;
		ReturnIfFailed(result, connection->Connect());

		m_downloadConnections[index] = connection;
	}

	return m_downloadConnections[index].CopyTo(value);
}

HRESULT Datacenter::GetUploadConnection(UINT32 index, boolean create, Connection** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	if (index >= UPLOAD_CONNECTIONS_COUNT)
	{
		return E_BOUNDS;
	}

	auto lock = LockCriticalSection();

	if (m_uploadConnections[index] == nullptr && create)
	{
		auto connection = Make<Connection>(this, ConnectionType::Upload);

		HRESULT result;
		ReturnIfFailed(result, connection->Connect());

		m_uploadConnections[index] = connection;
	}

	return m_uploadConnections[index].CopyTo(value);
}

HRESULT Datacenter::GetGenericConnection(boolean create, Connection** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();

	if (m_genericConnection == nullptr && create)
	{
		auto connection = Make<Connection>(this, ConnectionType::Generic);

		HRESULT result;
		ReturnIfFailed(result, connection->Connect());

		m_genericConnection = connection;
	}

	return m_genericConnection.CopyTo(value);
}

HRESULT Datacenter::SuspendConnections()
{
	HRESULT result;
	auto lock = LockCriticalSection();

	if (m_genericConnection != nullptr)
	{
		ReturnIfFailed(result, m_genericConnection->Suspend());
	}

	for (size_t i = 0; i < UPLOAD_CONNECTIONS_COUNT; i++)
	{
		if (m_uploadConnections[i] != nullptr)
		{
			ReturnIfFailed(result, m_uploadConnections[i]->Suspend());
		}
	}

	for (size_t i = 0; i < DOWNLOAD_CONNECTIONS_COUNT; i++)
	{
		if (m_downloadConnections[i] != nullptr)
		{
			ReturnIfFailed(result, m_downloadConnections[i]->Suspend());
		}
	}

	return S_OK;
}

HRESULT Datacenter::BeginHandshake(boolean reconnect)
{
	auto lock = LockCriticalSection();

	m_handshakeContext = std::make_unique<HandshakeContext>();

	HRESULT result;
	ComPtr<Connection> genericConnection;
	ReturnIfFailed(result, GetGenericConnection(true, &genericConnection));

	genericConnection->RecreateSession();

	m_handshakeContext->State = HandshakeState::Started;
	m_handshakeContext->Request = Make<TLReqPQ>();

	return SendRequest(m_handshakeContext->Request.Get(), genericConnection.Get());
}

HRESULT Datacenter::GetCurrentEndpoint(ConnectionType connectionType, boolean ipv6, ServerEndpoint** endpoint)
{
	if (endpoint == nullptr)
	{
		return E_POINTER;
	}

	size_t currentEndpointIndex;
	std::vector<ServerEndpoint>* endpoints;
	auto lock = LockCriticalSection();

	switch (connectionType)
	{
	case ConnectionType::Generic:
	case ConnectionType::Upload:
		if (ipv6)
		{
			currentEndpointIndex = m_currentIpv6EndpointIndex;
			endpoints = &m_ipv6Endpoints;
		}
		else
		{
			currentEndpointIndex = m_currentIpv4EndpointIndex;
			endpoints = &m_ipv4Endpoints;
		}
		break;
	case ConnectionType::Download:
		if (ipv6)
		{
			currentEndpointIndex = m_currentIpv6DownloadEndpointIndex;
			endpoints = &m_ipv6DownloadEndpoints;
		}
		else
		{
			currentEndpointIndex = m_currentIpv4DownloadEndpointIndex;
			endpoints = &m_ipv4DownloadEndpoints;
		}
		break;
	default:
		return E_INVALIDARG;
	}

	if (currentEndpointIndex >= endpoints->size())
	{
		return E_BOUNDS;
	}

	*endpoint = &(*endpoints)[currentEndpointIndex];
	return S_OK;
}

HRESULT Datacenter::GetEndpointsForConnectionType(ConnectionType connectionType, boolean ipv6, std::vector<ServerEndpoint>** endpoints)
{
	switch (connectionType)
	{
	case ConnectionType::Generic:
	case ConnectionType::Upload:
		if (ipv6)
		{
			*endpoints = &m_ipv6Endpoints;
		}
		else
		{
			*endpoints = &m_ipv4Endpoints;
		}
		break;
	case ConnectionType::Download:
		if (ipv6)
		{
			*endpoints = &m_ipv6DownloadEndpoints;
		}
		else
		{
			*endpoints = &m_ipv4DownloadEndpoints;
		}
		break;
	default:
		return E_INVALIDARG;
	}

	return S_OK;
}

HRESULT Datacenter::SendRequest(ITLObject* object, Connection* connection)
{
	HRESULT result;
	ComPtr<ConnectionManager> connectionManager;
	ReturnIfFailed(result, ConnectionManager::GetInstance(connectionManager));

	UINT32 objectSize;
	ReturnIfFailed(result, TLObjectSizeCalculator::GetSize(object, &objectSize));

	ComPtr<TLBinaryWriter> writer;
	ReturnIfFailed(result, MakeAndInitialize<TLBinaryWriter>(&writer, 2 * sizeof(INT64) + sizeof(INT32) + objectSize));
	ReturnIfFailed(result, writer->WriteInt64(0));
	ReturnIfFailed(result, writer->WriteInt64(connectionManager->GenerateMessageId()));
	ReturnIfFailed(result, writer->WriteUInt32(objectSize));
	ReturnIfFailed(result, writer->WriteObject(object));

	return connection->SendData(writer->GetBuffer(), writer->GetPosition(), true);
}

HRESULT Datacenter::OnHandshakeConnectionClosed(Connection* connection)
{
	auto lock = LockCriticalSection();

	return S_OK;
}

HRESULT Datacenter::OnHandshakeConnectionConnected(Connection* connection)
{
	auto lock = LockCriticalSection();

	return S_OK;
}