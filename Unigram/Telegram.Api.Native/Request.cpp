#include "pch.h"
#include "Request.h"
#include "TLObject.h"
#include "Datacenter.h"

using namespace Telegram::Api::Native;


HRESULT MessageRequest::RuntimeClassInitialize(ITLObject* object, INT32 token, ConnectionType connectionType, UINT32 datacenterId, ISendRequestCompletedCallback* sendCompletedCallback,
	IRequestQuickAckReceivedCallback* quickAckReceivedCallback, RequestFlag flags)
{
	if (object == nullptr)
	{
		return E_INVALIDARG;
	}

	m_object = object;
	m_messageToken = token;
	m_connectionType = connectionType;
	m_datacenterId = datacenterId;
	m_sendCompletedCallback = sendCompletedCallback;
	m_quickAckReceivedCallback = quickAckReceivedCallback;
	m_flags = flags;

	ZeroMemory(&m_messageContext, sizeof(MessageContext));

	return S_OK;
}

HRESULT MessageRequest::get_Object(ITLObject** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	return m_object.CopyTo(value);
}

HRESULT MessageRequest::get_MessageContext(MessageContext const** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = &m_messageContext;
	return S_OK;
}

HRESULT MessageRequest::get_RawObject(ITLObject** value)
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

HRESULT MessageRequest::get_MessageToken(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_messageToken;
	return S_OK;
}

HRESULT MessageRequest::get_ConnectionType(ConnectionType* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_connectionType;
	return S_OK;
}

HRESULT MessageRequest::get_DatacenterId(UINT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_datacenterId;
	return S_OK;
}

HRESULT MessageRequest::get_Flags(RequestFlag* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_flags;
	return S_OK;
}