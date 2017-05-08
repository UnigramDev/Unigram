#include "pch.h"
#include "Datacenter.h"
#include "Connection.h"
#include "Helpers\COMHelper.h"

using namespace Telegram::Api::Native;


Datacenter::Datacenter() :
	m_id(0),
	m_currentIpv4EndpointIndex(0),
	m_currentIpv4DownloadEndpointIndex(0),
	m_currentIpv6EndpointIndex(0),
	m_currentIpv6DownloadEndpointIndex(0)
{
}

Datacenter::~Datacenter()
{
}

HRESULT Datacenter::RuntimeClassInitialize(UINT32 id)
{
	m_id = id;
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
	HRESULT result;
	auto lock = m_criticalSection.Lock();

	/*if (m_closed)
	{
		return RO_E_CLOSED;
	}*/

	if (m_genericConnection != nullptr)
	{
		m_genericConnection->Close();
		m_genericConnection.Reset();
	}

	if (m_pushConnection != nullptr)
	{
		m_pushConnection->Close();
		m_pushConnection.Reset();
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

//HRESULT Datacenter::GetDownloadConnection(UINT32 index, boolean create, IConnection** value)
//{
//	if (value == nullptr)
//	{
//		return E_POINTER;
//	}
//
//	HRESULT result;
//	ComPtr<Connection> connection;
//	ReturnIfFailed(result, GetDownloadConnection(index, create, &connection));
//
//	*value = connection.Detach();
//	return S_OK;
//}
//
//HRESULT Datacenter::GetUploadConnection(UINT32 index, boolean create, IConnection** value)
//{
//	if (value == nullptr)
//	{
//		return E_POINTER;
//	}
//
//	HRESULT result;
//	ComPtr<Connection> connection;
//	ReturnIfFailed(result, GetUploadConnection(index, create, &connection));
//
//	*value = connection.Detach();
//	return S_OK;
//}
//
//HRESULT Datacenter::GetGenericConnection(boolean create, IConnection** value)
//{
//	if (value == nullptr)
//	{
//		return E_POINTER;
//	}
//
//	HRESULT result;
//	ComPtr<Connection> connection;
//	ReturnIfFailed(result, GetGenericConnection(create, &connection));
//
//	*value = connection.Detach();
//	return S_OK;
//}
//
//HRESULT Datacenter::GetPushConnection(boolean create, IConnection** value)
//{
//	if (value == nullptr)
//	{
//		return E_POINTER;
//	}
//
//	HRESULT result;
//	ComPtr<Connection> connection;
//	ReturnIfFailed(result, GetPushConnection(create, &connection));
//
//	*value = connection.Detach();
//	return S_OK;
//}

void Datacenter::SwitchTo443Port()
{
	auto lock = m_criticalSection.Lock();

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

	auto lock = m_criticalSection.Lock();

	if (m_downloadConnections[index] == nullptr && create)
	{
		HRESULT result;
		ComPtr<Connection> connection;
		ReturnIfFailed(result, MakeAndInitialize<Connection>(&m_downloadConnections[index], this, ConnectionType::Download));
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

	auto lock = m_criticalSection.Lock();

	if (m_uploadConnections[index] == nullptr && create)
	{
		HRESULT result;
		ComPtr<Connection> connection;
		ReturnIfFailed(result, MakeAndInitialize<Connection>(&m_uploadConnections[index], this, ConnectionType::Upload));
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

	auto lock = m_criticalSection.Lock();

	if (m_genericConnection == nullptr && create)
	{
		HRESULT result;
		ComPtr<Connection> connection;
		ReturnIfFailed(result, MakeAndInitialize<Connection>(&m_genericConnection, this, ConnectionType::Generic));
		//ReturnIfFailed(result, connection->Connect());
	}

	return m_genericConnection.CopyTo(value);
}

HRESULT Datacenter::GetPushConnection(boolean create, Connection** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = m_criticalSection.Lock();

	if (m_pushConnection == nullptr && create)
	{
		HRESULT result;
		ComPtr<Connection> connection;
		ReturnIfFailed(result, MakeAndInitialize<Connection>(&m_pushConnection, this, ConnectionType::Push));
		//ReturnIfFailed(result, connection->Connect());
	}

	return m_pushConnection.CopyTo(value);
}

void Datacenter::RecreateSessions()
{
	auto lock = m_criticalSection.Lock();

	if (m_genericConnection != nullptr)
	{
		m_genericConnection->RecreateSession();
	}

	/*if (m_pushConnection != nullptr)
	{
		m_pushConnection->RecreateSession();
	}*/

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

HRESULT Datacenter::GetCurrentEndpoint(ConnectionType connectionType, boolean ipv6, DatacenterEndpoint** endpoint)
{
	if (endpoint == nullptr)
	{
		return E_POINTER;
	}

	size_t currentEndpointIndex;
	std::vector<DatacenterEndpoint>* endpoints;
	auto lock = m_criticalSection.Lock();

	switch (connectionType)
	{
	case ConnectionType::Generic:
	case ConnectionType::Upload:
	case ConnectionType::Push:
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