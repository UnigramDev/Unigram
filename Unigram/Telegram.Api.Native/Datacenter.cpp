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
	ZeroMemory(m_uploadConnections, DOWNLOAD_CONNECTIONS_COUNT * sizeof(Connection^));
}

uint32 Datacenter::Id::get()
{
	return m_id;
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

	size_t currentEndpointIndex;
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
	default:
		//throw ref new InvalidArgumentException(L"An invlid value for 'addressType' has been provided");
	}
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