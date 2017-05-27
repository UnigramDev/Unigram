#include "pch.h"
#include "TLUnprocessedMessage.h"
#include "TLBinaryReader.h"

using namespace Telegram::Api::Native;
using namespace Telegram::Api::Native::TL;


TLUnprocessedMessage::TLUnprocessedMessage(INT64 messageId, ConnectionType connectionType, ITLObject* object) :
	m_messageId(messageId),
	m_connectionType(connectionType),
	m_object(object)
{
}

TLUnprocessedMessage::~TLUnprocessedMessage()
{
}

HRESULT TLUnprocessedMessage::get_MessageId(INT64* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_messageId;
	return S_OK;
}

HRESULT TLUnprocessedMessage::get_ConnectionType(ConnectionType* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_connectionType;
	return S_OK;
}

HRESULT TLUnprocessedMessage::get_Object(ITLObject** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	return m_object.CopyTo(value);
}