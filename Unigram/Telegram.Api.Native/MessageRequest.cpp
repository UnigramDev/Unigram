#include "pch.h"
#include "MessageRequest.h"
#include "TLObject.h"
#include "TLTypes.h"
#include "Datacenter.h"
#include "MessageResponse.h"

using namespace Telegram::Api::Native;
using namespace Telegram::Api::Native::TL;


HRESULT MessageRequest::RuntimeClassInitialize(ITLObject* object, INT32 token, ConnectionType connectionType, INT32 datacenterId, ISendRequestCompletedCallback* sendCompletedCallback,
	IRequestQuickAckReceivedCallback* quickAckReceivedCallback, RequestFlag flags)
{
	/*if (object == nullptr)
	{
		return E_INVALIDARG;
	}*/

	m_object = object;
	m_token = token;
	m_connectionType = connectionType;
	m_datacenterId = datacenterId;
	m_sendCompletedCallback = sendCompletedCallback;
	m_quickAckReceivedCallback = quickAckReceivedCallback;
	m_flags = flags | REQUEST_FLAG_CONNECTION_INDEX;
	m_startTime = 0;
	m_attemptCount = 0;

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

	*value = m_messageContext.get();
	return S_OK;
}

HRESULT MessageRequest::get_Token(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_token;
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

HRESULT MessageRequest::get_DatacenterId(INT32* value)
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

	*value = m_flags & ~(REQUEST_FLAG_INIT_CONNECTION | REQUEST_FLAG_NO_LAYER | REQUEST_FLAG_CONNECTION_INDEX);
	return S_OK;
}

void MessageRequest::Reset(bool resetStartTime)
{
	m_messageContext.reset();
	m_flags = m_flags & ~(REQUEST_FLAG_INIT_CONNECTION | REQUEST_FLAG_CONNECTION_INDEX);

	if (resetStartTime)
	{
		m_startTime = 0;
	}
}