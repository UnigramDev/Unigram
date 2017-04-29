#include "pch.h"
#include "Datacenter.h"
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

HRESULT Datacenter::GetCurrentAddress(DatacenterEndpointType endpointType, HSTRING* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	HRESULT result;
	DatacenterEndpoint* endpoint;
	auto lock = m_criticalSection.Lock();

	ReturnIfFailed(result, GetCurrentEndpoint(endpointType, &endpoint));

	return WindowsCreateString(endpoint->Address, value);
}

HRESULT Datacenter::GetCurrentPort(DatacenterEndpointType endpointType, UINT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	HRESULT result;
	DatacenterEndpoint* endpoint;
	auto lock = m_criticalSection.Lock();

	ReturnIfFailed(result, GetCurrentEndpoint(endpointType, &endpoint));

	*value = endpoint->Port;
	return S_OK;
}

HRESULT Datacenter::GetDownloadConnection(UINT32 index, boolean create, IConnection** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = m_criticalSection.Lock();

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

HRESULT Datacenter::GetUploadConnection(UINT32 index, boolean create, IConnection** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = m_criticalSection.Lock();

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

HRESULT Datacenter::GetGenericConnection(boolean create, IConnection** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = m_criticalSection.Lock();

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

HRESULT Datacenter::GetPushConnection(boolean create, IConnection** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = m_criticalSection.Lock();

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

HRESULT Datacenter::GetCurrentEndpoint(DatacenterEndpointType endpointType, DatacenterEndpoint** endpoint)
{
	if (endpoint == nullptr)
	{
		return E_POINTER;
	}

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
		return E_INVALIDARG;
	}

	if (currentEndpointIndex >= endpoints->size())
	{
		return E_BOUNDS;
	}

	*endpoint = &(*endpoints)[currentEndpointIndex];
	return S_OK;
}