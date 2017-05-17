#include "pch.h"
#include "TLUnparsedMessage.h"
#include "TLBinaryReader.h"

using namespace Telegram::Api::Native;
using namespace Telegram::Api::Native::TL;


TLUnparsedMessage::TLUnparsedMessage(INT64 messageId, ConnectionType connectionType, ITLBinaryReader* reader) :
	m_messageId(messageId),
	m_connectionType(connectionType),
	m_reader(reader)
{
}

TLUnparsedMessage::~TLUnparsedMessage()
{
}

HRESULT TLUnparsedMessage::get_MessageId(INT64* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_messageId;
	return S_OK;
}

HRESULT TLUnparsedMessage::get_ConnectionType(ConnectionType* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_connectionType;
	return S_OK;
}

HRESULT TLUnparsedMessage::get_Reader(ITLBinaryReader** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	return m_reader.CopyTo(value);
}