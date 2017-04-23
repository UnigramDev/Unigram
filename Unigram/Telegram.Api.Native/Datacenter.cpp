#include "pch.h"
#include <algorithm>
#include "Datacenter.h"

using namespace Telegram::Api::Native;

inline String^ StringFromStdString(std::wstring const& string)
{
	return ref new String(string.c_str(), string.length());
}

Datacenter::Datacenter(uint32 id) :
	m_id(id),
	m_genericConnection(nullptr),
	m_pushConnection(nullptr),
	m_currentIpv4EndpointIndex(0),
	m_currentIpv4DownloadEndpointIndex(0),
	m_currentIpv6EndpointIndex(0),
	m_currentIpv6DownloadEndpointIndex(0)
{
	ZeroMemory(m_downloadConnections, DOWNLOAD_CONNECTIONS_COUNT * sizeof(Connection^));
	ZeroMemory(m_uploadConnections, UPLOAD_CONNECTIONS_COUNT * sizeof(Connection^));
}

uint32 Datacenter::Id::get()
{
	return m_id;
}

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

String^ Datacenter::GetCurrentAddress(DatacenterEndpointType endpointType)
{
	auto lock = m_criticalSection.Lock();
	auto endpoint = GetCurrentEndpoint(endpointType);

	if (endpoint == nullptr)
	{
		return nullptr;
	}

	return StringFromStdString(endpoint->Address);
}

int32 Datacenter::GetCurrentPort(DatacenterEndpointType endpointType)
{
	auto lock = m_criticalSection.Lock();
	auto endpoint = GetCurrentEndpoint(endpointType);

	if (endpoint == nullptr)
	{
		return -1;
	}

	return endpoint->Port;
}

void Datacenter::AddAddressAndPort(String^ address, uint32 port, DatacenterEndpointType endpointType)
{
	auto lock = m_criticalSection.Lock();

	std::vector<DatacenterEndpoint>* endpoints;

	switch (endpointType)
	{
	case DatacenterEndpointType::Ipv4:
		endpoints = &m_ipv4Endpoints;
		break;
	case DatacenterEndpointType::Ipv6:
		endpoints = &m_ipv6Endpoints;
		break;
	case DatacenterEndpointType::Ipv4Download:
		endpoints = &m_ipv4DownloadEndpoints;
		break;
	case DatacenterEndpointType::Ipv6Download:
		endpoints = &m_ipv6DownloadEndpoints;
		break;
	default:
		//throw ref new InvalidArgumentException(L"An invlid value for 'addressType' has been provided");
		return;
	}

	std::wstring newAddress(address->Data(), address->Length());
	auto& endpoint = std::find_if(endpoints->begin(), endpoints->end(), [&newAddress, &port](const DatacenterEndpoint& e)
	{
		return e.Port == port && e.Address.compare(newAddress) == 0;
	});

	if (endpoint != endpoints->end())
	{
		endpoints->push_back({ newAddress, port });
	}
}

void Datacenter::NextAddressOrPort(DatacenterEndpointType endpointType)
{
	auto lock = m_criticalSection.Lock();

	switch (endpointType)
	{
	case DatacenterEndpointType::Ipv4:
		m_currentIpv4EndpointIndex = (m_currentIpv4EndpointIndex + 1) % m_ipv4Endpoints.size();
		break;
	case DatacenterEndpointType::Ipv6:
		m_currentIpv6EndpointIndex = (m_currentIpv6EndpointIndex + 1) % m_ipv6Endpoints.size();
		break;
	case DatacenterEndpointType::Ipv4Download:
		m_currentIpv4DownloadEndpointIndex = (m_currentIpv4DownloadEndpointIndex + 1) % m_ipv4DownloadEndpoints.size();
		break;
	case DatacenterEndpointType::Ipv6Download:
		m_currentIpv6DownloadEndpointIndex = (m_currentIpv6DownloadEndpointIndex + 1) % m_ipv6DownloadEndpoints.size();
		break;
		//default:
		//	throw ref new InvalidArgumentException(L"An invlid value for 'addressType' has been provided");
	}
}

void Datacenter::StoreCurrentAddressAndPort()
{
	auto lock = m_criticalSection.Lock();

	I_WANT_TO_DIE_IS_THE_NEW_TODO
}

void Datacenter::ResetAddressAndPort()
{
	auto lock = m_criticalSection.Lock();

	m_currentIpv4EndpointIndex = 0;
	m_currentIpv6EndpointIndex = 0;
	m_currentIpv4DownloadEndpointIndex = 0;
	m_currentIpv6DownloadEndpointIndex = 0;

	StoreCurrentAddressAndPort();
}

void Datacenter::SuspendConnections()
{
	auto lock = m_criticalSection.Lock();

	if (m_genericConnection != nullptr)
	{
		m_genericConnection->Suspend();
	}

	for (size_t i = 0; i < UPLOAD_CONNECTIONS_COUNT; i++)
	{
		if (m_uploadConnections[i] != nullptr)
		{
			m_uploadConnections[i]->Suspend();
		}
	}

	for (size_t i = 0; i < DOWNLOAD_CONNECTIONS_COUNT; i++)
	{
		if (m_downloadConnections[i] != nullptr)
		{
			m_downloadConnections[i]->Suspend();
		}
	}
}

void Datacenter::RecreateSessions()
{
	auto lock = m_criticalSection.Lock();

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

void Datacenter::Clear()
{
	auto lock = m_criticalSection.Lock();

	//m_authKey = nullptr;
	//m_authKeyId = 0;
	//m_authorized = false;
	//m_serverSalts.clear();

	I_WANT_TO_DIE_IS_THE_NEW_TODO
}

void Datacenter::ClearServerSalts()
{
	auto lock = m_criticalSection.Lock();

	//m_serverSalts.clear();

	I_WANT_TO_DIE_IS_THE_NEW_TODO
}

bool Datacenter::ContainsServerSalt(int64 value)
{
	auto lock = m_criticalSection.Lock();

	/*for (size_t i = 0; i < m_serverSalts.size(); i++)
	{
		if (m_serverSalts[i]->salt == value)
		{
			return true;
		}
	}*/

	I_WANT_TO_DIE_IS_THE_NEW_TODO

		return false;
}

Connection^ Datacenter::GetDownloadConnection(uint32 index, bool create)
{
	if (index >= DOWNLOAD_CONNECTIONS_COUNT)
	{
		//throw ref new OutOfBoundsException();
		return nullptr;
	}

	auto lock = m_criticalSection.Lock();

	/*if (m_authKey == nullptr)
	{
		return nullptr;
	}*/

	if (create)
	{
		EnsureDownloadConnection(index)->Connect();
	}

	return m_downloadConnections[index];

}

Connection^ Datacenter::GetUploadConnection(uint32 index, bool create)
{
	if (index >= UPLOAD_CONNECTIONS_COUNT)
	{
		//throw ref new OutOfBoundsException();
		return nullptr;
	}

	auto lock = m_criticalSection.Lock();

	/*if (m_authKey == nullptr)
	{
		return nullptr;
	}*/

	if (create)
	{
		EnsureUploadConnection(index)->Connect();
	}

	return m_uploadConnections[index];
}

Connection^ Datacenter::GetGenericConnection(bool create)
{
	auto lock = m_criticalSection.Lock();

	/*if (m_authKey == nullptr)
	{
		return nullptr;
	}*/

	if (create)
	{
		EnsureGenericConnection()->Connect();
	}

	return m_genericConnection;
}

Connection^ Datacenter::GetPushConnection(bool create)
{
	auto lock = m_criticalSection.Lock();

	/*if (m_authKey == nullptr)
	{
		return nullptr;
	}*/

	if (create)
	{
		EnsurePushConnection()->Connect();
	}

	return m_pushConnection;
}

Connection^ Datacenter::EnsureDownloadConnection(uint32 index)
{
	if (m_downloadConnections[index] == nullptr)
	{
		m_downloadConnections[index] = ref new Connection(this, ConnectionType::Download);
	}

	return m_downloadConnections[index];
}

Connection^ Datacenter::EnsureUploadConnection(uint32 index)
{
	if (m_uploadConnections[index] == nullptr)
	{
		m_uploadConnections[index] = ref new Connection(this, ConnectionType::Upload);
	}

	return m_uploadConnections[index];
}

Connection^ Datacenter::EnsureGenericConnection()
{
	if (m_genericConnection == nullptr)
	{
		m_genericConnection = ref new Connection(this, ConnectionType::Generic);
	}

	return m_genericConnection;
}

Connection^ Datacenter::EnsurePushConnection()
{
	if (m_pushConnection == nullptr)
	{
		m_pushConnection = ref new Connection(this, ConnectionType::Push);
	}

	return m_pushConnection;
}

Datacenter::DatacenterEndpoint* Datacenter::GetCurrentEndpoint(DatacenterEndpointType endpointType)
{
	size_t currentEndpointIndex;
	std::vector<DatacenterEndpoint>* endpoints;

	switch (endpointType)
	{
	case DatacenterEndpointType::Ipv4:
		currentEndpointIndex = m_currentIpv4EndpointIndex;
		endpoints = &m_ipv4Endpoints;
		break;
	case DatacenterEndpointType::Ipv6:
		currentEndpointIndex = m_currentIpv6EndpointIndex;
		endpoints = &m_ipv6Endpoints;
		break;
	case DatacenterEndpointType::Ipv4Download:
		currentEndpointIndex = m_currentIpv4DownloadEndpointIndex;
		endpoints = &m_ipv4DownloadEndpoints;
		break;
	case DatacenterEndpointType::Ipv6Download:
		currentEndpointIndex = m_currentIpv6DownloadEndpointIndex;
		endpoints = &m_ipv6DownloadEndpoints;
		break;
	default:
		//throw ref new InvalidArgumentException(L"An invlid value for 'addressType' has been provided");
		return nullptr;
	}

	if (currentEndpointIndex >= endpoints->size())
	{
		return nullptr;
	}

	return &(*endpoints)[currentEndpointIndex];
}