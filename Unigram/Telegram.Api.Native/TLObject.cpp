#include "pch.h"
#include "TLObject.h"
#include "ConnectionManager.h"

using namespace Telegram::Api::Native;
using namespace Telegram::Api::Native::TL;


HRESULT TLObject::Read(ITLBinaryReader* reader)
{
	return ReadBody(static_cast<ITLBinaryReaderEx*>(reader));
}

HRESULT TLObject::Write(ITLBinaryWriter* writer)
{
	return WriteBody(static_cast<ITLBinaryWriterEx*>(writer));
}

std::unordered_map<UINT32, ComPtr<ITLObjectConstructorDelegate>>& TLObject::GetObjectConstructors()
{
	static std::unordered_map<UINT32, ComPtr<ITLObjectConstructorDelegate>> constructors;
	return constructors;
}

HRESULT TLObject::GetObjectConstructor(UINT32 constructor, ComPtr<ITLObjectConstructorDelegate>& delegate)
{
	auto& objectConstructors = GetObjectConstructors();
	auto objectConstructorIterator = objectConstructors.find(constructor);
	if (objectConstructorIterator == objectConstructors.end())
	{
		return E_INVALIDARG;
	}

	delegate = objectConstructorIterator->second;
	return S_OK;
}

HRESULT TLObject::RegisterTLObjecConstructor(UINT32 constructor, ITLObjectConstructorDelegate* delegate)
{
	if (delegate == nullptr)
	{
		return E_POINTER;
	}

	auto& objectConstructors = GetObjectConstructors();
	auto objectConstructorIterator = objectConstructors.find(constructor);
	if (objectConstructorIterator != objectConstructors.end())
	{
		return PLA_E_NO_DUPLICATES;
	}

	objectConstructors.insert(objectConstructorIterator, std::make_pair(constructor, delegate));
	return S_OK;
}

HRESULT TLObject::HandleResponse(MessageContext const* messageContext, ITLObject* messageBody, Connection* connection)
{
	ComPtr<IMessageResponseHandler> responseHandler;
	if (SUCCEEDED(messageBody->QueryInterface(IID_PPV_ARGS(&responseHandler))))
	{
		return responseHandler->HandleResponse(messageContext, connection);
	}
	else
	{
		auto& connectionManager = connection->GetDatacenter()->GetConnectionManager();
		return connectionManager->OnUnprocessedMessageResponse(messageContext, messageBody, connection);
	}
}

HRESULT TLObject::CompleteRequest(INT64 requestMessageId, MessageContext const* messageContext, ITLObject* messageBody, Connection* connection)
{
	auto& connectionManager = connection->GetDatacenter()->GetConnectionManager();
	return connectionManager->CompleteMessageRequest(requestMessageId, messageContext, messageBody, connection);
}


HRESULT TLObjectWithQuery::RuntimeClassInitialize(ITLObject* query)
{
	if (query == nullptr)
	{
		return E_INVALIDARG;
	}

	m_query = query;
	return S_OK;
}

HRESULT TLObjectWithQuery::get_Query(ITLObject** value)
{
	return m_query.CopyTo(value);
}

HRESULT TLObjectWithQuery::HandleResponse(MessageContext const* messageContext, Connection* connection)
{
	return TLObject::HandleResponse(messageContext, m_query.Get(), connection);
}


HRESULT TLUnparsedObject::RuntimeClassInitialize(UINT32 constructor, TLMemoryBinaryReader* reader)
{
	if (reader == nullptr)
	{
		return E_INVALIDARG;
	}

	m_constructor = constructor;
	m_reader = reader;
	return S_OK;
}

HRESULT TLUnparsedObject::RuntimeClassInitialize(UINT32 constructor, UINT32 objectSizeWithoutConstructor, TLMemoryBinaryReader* reader)
{
	if (reader == nullptr)
	{
		return E_INVALIDARG;
	}

	if (objectSizeWithoutConstructor > reader->GetUnconsumedBufferLength())
	{
		return E_BOUNDS;
	}

	HRESULT result;
	ReturnIfFailed(result, MakeAndInitialize<TLMemoryBinaryReader>(&m_reader, objectSizeWithoutConstructor));

	CopyMemory(m_reader->GetBuffer(), reader->GetBufferAtPosition(), m_reader->GetCapacity());

	m_constructor = constructor;
	return S_OK;
}

HRESULT TLUnparsedObject::get_Reader(ITLBinaryReader** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = static_cast<ITLBinaryReaderEx*>(m_reader.Get());
	(*value)->AddRef();
	return S_OK;
}

HRESULT TLUnparsedObject::get_Constructor(UINT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_constructor;
	return S_OK;
}

HRESULT TLUnparsedObject::get_IsLayerRequired(boolean* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = false;
	return S_OK;
}

HRESULT TLUnparsedObject::Read(ITLBinaryReader* reader)
{
	return reader->ReadRawBuffer(m_reader->GetCapacity(), m_reader->GetBuffer());
}

HRESULT TLUnparsedObject::Write(ITLBinaryWriter* writer)
{
	return writer->WriteRawBuffer(m_reader->GetCapacity(), m_reader->GetBuffer());
}