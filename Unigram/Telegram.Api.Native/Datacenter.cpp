#include "pch.h"
#include <algorithm>
#include <Ws2tcpip.h>
#include "Datacenter.h"
#include "TLBinaryReader.h"
#include "Connection.h"
#include "Helpers\COMHelper.h"

using namespace Telegram::Api::Native;


Datacenter::Datacenter(UINT32 id) :
	m_id(0),
	m_handshakeState(HandshakeState::None),
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

HRESULT Datacenter::RuntimeClassInitialize(TLBinaryReader* reader)
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement Datacenter initialization from reader");

	return S_OK;
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

	*value = m_handshakeState;
	return S_OK;
}

HRESULT Datacenter::GetCurrentAddress(ConnectionType connectionType, boolean ipv6, HSTRING* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	HRESULT result;
	DatacenterEndpoint* endpoint;
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
	DatacenterEndpoint* endpoint;
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

	if (m_genericConnection != nullptr)
	{
		m_genericConnection->Close();
		m_genericConnection.Reset();
	}

	for (size_t i = 0; i < UPLOAD_CONNECTIONS_COUNT; i++)
	{
		if (m_uploadConnections[i] != nullptr)
		{
			m_uploadConnections[i]->Close();
			m_uploadConnections[i].Reset();
		}
	}

	for (size_t i = 0; i < DOWNLOAD_CONNECTIONS_COUNT; i++)
	{
		if (m_downloadConnections[i] != nullptr)
		{
			m_downloadConnections[i]->Close();
			m_downloadConnections[i].Reset();
		}
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

HandshakeState Datacenter::GetHandshakeState()
{
	auto lock = LockCriticalSection();

	return m_handshakeState;
}

HRESULT Datacenter::AddEndpoint(std::wstring address, UINT32 port, ConnectionType connectionType, boolean ipv6)
{
#if _DEBUG
	ADDRINFOW* addressInfo;
	if (GetAddrInfo(address.data(), nullptr, nullptr, &addressInfo) != NO_ERROR)
	{
		return WS_E_ENDPOINT_NOT_FOUND;
	}

	FreeAddrInfo(addressInfo);
#endif

	std::vector<DatacenterEndpoint>* endpoints;
	auto lock = LockCriticalSection();

	switch (connectionType)
	{
	case ConnectionType::Generic:
	case ConnectionType::Upload:
		if (ipv6)
		{
			endpoints = &m_ipv6Endpoints;
		}
		else
		{
			endpoints = &m_ipv4Endpoints;
		}
		break;
	case ConnectionType::Download:
		if (ipv6)
		{
			endpoints = &m_ipv6DownloadEndpoints;
		}
		else
		{
			endpoints = &m_ipv4DownloadEndpoints;
		}
		break;
	default:
		return E_INVALIDARG;
	}

	if (std::find_if(endpoints->begin(), endpoints->end(), [&](DatacenterEndpoint const& endpoint)
	{
		return endpoint.Address.compare(address) == 0; // && endpoint.Port == port;
	}) != endpoints->end())
	{
		return PLA_E_NO_DUPLICATES;
	}

	endpoints->push_back({ address, port });
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
		m_downloadConnections[index] = Make<Connection>(this, ConnectionType::Download);

		//HRESULT result;
		//ReturnIfFailed(result, connection->Connect());
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
		m_uploadConnections[index] = Make<Connection>(this, ConnectionType::Upload);

		//HRESULT result;
		//ReturnIfFailed(result, connection->Connect());
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
		m_genericConnection = Make<Connection>(this, ConnectionType::Generic);

		//HRESULT result;
		//ReturnIfFailed(result, connection->Connect());
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

HRESULT Datacenter::GetCurrentEndpoint(ConnectionType connectionType, boolean ipv6, DatacenterEndpoint** endpoint)
{
	if (endpoint == nullptr)
	{
		return E_POINTER;
	}

	size_t currentEndpointIndex;
	std::vector<DatacenterEndpoint>* endpoints;
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