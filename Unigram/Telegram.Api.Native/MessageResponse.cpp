#include "pch.h"
#include "MessageResponse.h"
#include "TLBinaryReader.h"

using namespace Telegram::Api::Native;
using namespace Telegram::Api::Native::TL;


MessageResponse::MessageResponse(INT64 messageId, ConnectionType connectionType, ITLObject* object) :
	m_messageId(messageId),
	m_connectionType(connectionType),
	m_object(object)
{
}

MessageResponse::~MessageResponse()
{
}

HRESULT MessageResponse::get_MessageId(INT64* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_messageId;
	return S_OK;
}

HRESULT MessageResponse::get_ConnectionType(ConnectionType* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_connectionType;
	return S_OK;
}

HRESULT MessageResponse::get_Object(ITLObject** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	return m_object.CopyTo(value);
}