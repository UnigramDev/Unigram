#include "pch.h"
#include "MessageRequest.h"
#include "TLObject.h"
#include "TLTypes.h"
#include "Datacenter.h"
#include "TLUnprocessedMessage.h"

using namespace Telegram::Api::Native;
using namespace Telegram::Api::Native::TL;


HRESULT MessageRequest::RuntimeClassInitialize(ITLObject* object, INT32 token, ConnectionType connectionType, UINT32 datacenterId, ISendRequestCompletedCallback* sendCompletedCallback,
	IRequestQuickAckReceivedCallback* quickAckReceivedCallback, RequestFlag flags)
{
	if (object == nullptr)
	{
		return E_INVALIDARG;
	}

	m_object = object;
	m_token = token;
	m_connectionType = connectionType;
	m_datacenterId = datacenterId;
	m_sendCompletedCallback = sendCompletedCallback;
	m_quickAckReceivedCallback = quickAckReceivedCallback;
	m_flags = flags;
	m_startTime = 0;

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

HRESULT MessageRequest::CreateTransportMessage(TLMessage** message)
{
	ComPtr<ITLObject> object;
	if ((m_flags & RequestFlag::CanCompress) == RequestFlag::CanCompress)
	{
		HRESULT result;
		ReturnIfFailed(result, MakeAndInitialize<TLGZipPacked>(&object, m_object.Get()));
	}
	else
	{
		object = m_object;
	}

	return MakeAndInitialize<TLMessage>(message, m_messageContext.get(), object.Get());
}

HRESULT MessageRequest::OnQuickAckReceived()
{
	if (m_quickAckReceivedCallback == nullptr)
	{
		return S_OK;
	}

	return m_quickAckReceivedCallback->Invoke();
}

HRESULT MessageRequest::OnSendCompleted(MessageContext const* messageContext, ITLObject* messageBody)
{
	if (m_sendCompletedCallback == nullptr)
	{
		return S_OK;
	}

	auto unprocessedMessage = Make<TLUnprocessedMessage>(messageContext->Id, m_connectionType, messageBody);
	return m_sendCompletedCallback->Invoke(unprocessedMessage.Get(), S_OK);
}

void MessageRequest::Reset()
{
	m_startTime = 0;
	m_messageContext.reset();
}