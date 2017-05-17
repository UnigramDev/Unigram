#include "pch.h"
#include "TLObject.h"

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
	auto& objectConstructor = objectConstructors.find(constructor);
	if (objectConstructor == objectConstructors.end())
	{
		return E_INVALIDARG;
	}

	delegate = objectConstructor->second;
	return S_OK;
}

HRESULT TLObject::RegisterTLObjecConstructor(UINT32 constructor, ITLObjectConstructorDelegate* delegate)
{
	if (delegate == nullptr)
	{
		return E_POINTER;
	}

	auto& objectConstructors = GetObjectConstructors();
	if (objectConstructors.find(constructor) != objectConstructors.end())
	{
		return PLA_E_NO_DUPLICATES;
	}

	objectConstructors[constructor] = delegate;
	return S_OK;
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


TLUnparsedObject::TLUnparsedObject(UINT32 constructor, ITLBinaryReader* reader) :
	m_constructor(constructor),
	m_reader(reader)
{
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

HRESULT TLUnparsedObject::get_Reader(ITLBinaryReader** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	return m_reader.CopyTo(value);
}

HRESULT TLUnparsedObject::get_IsLayerNeeded(boolean* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = false;
	return S_OK;
}