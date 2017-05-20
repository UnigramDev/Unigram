#include "pch.h"
#include "Request.h"
#include "TLObject.h"

using namespace Telegram::Api::Native;


Request::Request(ITLObject* object, INT32 token, ConnectionType connectionType, UINT32 datacenterId, ISendRequestCompletedCallback* sendCompletedCallback,
	IRequestQuickAckReceivedCallback* quickAckReceivedCallback, RequestFlag flags) :
	m_object(object),
	m_messageId(0),
	m_messageSequenceNumber(0),
	m_messageToken(token),
	m_connectionType(connectionType),
	m_datacenterId(datacenterId),
	m_sendCompletedCallback(sendCompletedCallback),
	m_quickAckReceivedCallback(quickAckReceivedCallback),
	m_flags(flags)
{
}

Request::~Request()
{
}

HRESULT Request::get_Object(_Out_ ITLObject** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	return m_object.CopyTo(value);
}

HRESULT Request::get_RawObject(_Out_ ITLObject** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	ComPtr<ITLObjectWithQuery> objectWithQuery;
	if (SUCCEEDED(m_object.As(&objectWithQuery)))
	{
		return objectWithQuery->get_Query(value);
	}

	return m_object.CopyTo(value);
}

HRESULT Request::get_MessageId(INT64* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_messageId;
	return S_OK;
}

HRESULT Request::get_MessageSequenceNumber(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_messageSequenceNumber;
	return S_OK;
}

HRESULT Request::get_MessageToken(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_messageToken;
	return S_OK;
}

HRESULT Request::get_ConnectionType(ConnectionType* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_connectionType;
	return S_OK;
}

HRESULT Request::get_DatacenterId(UINT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_datacenterId;
	return S_OK;
}

HRESULT Request::get_Flags(RequestFlag* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_flags;
	return S_OK;
}