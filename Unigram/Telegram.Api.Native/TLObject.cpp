#include "pch.h"
#include "TLObject.h"
#include "TLBinaryReader.h"
#include "TLBinaryWriter.h"
#include "Helpers\COMHelper.h"

using namespace Telegram::Api::Native;

TLObject::TLObject()
{
}

TLObject::~TLObject()
{
}

HRESULT TLObject::get_Size(UINT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	HRESULT result;
	ComPtr<TLBinarySizeCalculator> binarySizeCalulator;
	ReturnIfFailed(result, TLBinarySizeCalculator::GetInstance(binarySizeCalulator));
	ReturnIfFailed(result, Write(static_cast<ITLBinaryWriterEx*>(binarySizeCalulator.Get())));

	*value = binarySizeCalulator->GetTotalLength();
	return S_OK;
}

HRESULT TLObject::Read(ITLBinaryReader* reader)
{
	if (reader == nullptr)
	{
		return E_POINTER;
	}

	return Read(static_cast<ITLBinaryReaderEx*>(reader));
}

HRESULT TLObject::Write(ITLBinaryWriter* writer)
{
	if (writer == nullptr)
	{
		return E_POINTER;
	}

	return Write(static_cast<ITLBinaryWriterEx*>(writer));
}


TLObjectWithQuery::TLObjectWithQuery()
{
}

TLObjectWithQuery::~TLObjectWithQuery()
{
}

HRESULT TLObjectWithQuery::RuntimeClassInitialize(ITLObject* query)
{
	m_query = query;
	return S_OK;
}

HRESULT TLObjectWithQuery::get_Query(ITLObject** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	return m_query.CopyTo(value);
}


TLInitConnectionObject::TLInitConnectionObject()
{
}

TLInitConnectionObject::~TLInitConnectionObject()
{
}

HRESULT TLInitConnectionObject::RuntimeClassInitialize(ITLObject* query)
{
	return TLObjectWithQuery::RuntimeClassInitialize(query);
}

HRESULT TLInitConnectionObject::Read(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement TLInitConnectionObject read");

	return S_OK;
}

HRESULT TLInitConnectionObject::Write(ITLBinaryWriterEx* writer)
{
	HRESULT result;
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement TLInitConnectionObject write");

	return S_OK;
}