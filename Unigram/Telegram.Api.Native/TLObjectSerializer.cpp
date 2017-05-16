#include "pch.h"
#include <memory>
#include "TLObjectSerializer.h"
#include "Helpers\COMHelper.h"

using namespace Telegram::Api::Native::TL;


ActivatableStaticOnlyFactory(TLObjectSerializerStatics);


HRESULT TLObjectSerializerStatics::Serialize(ITLObject* object, UINT32* __valueSize, BYTE** value)
{
	if (object == nullptr)
	{
		return E_INVALIDARG;
	}

	if (__valueSize == nullptr || value == nullptr)
	{
		return E_POINTER;
	}

	HRESULT result;
	UINT32 objectSize;
	ReturnIfFailed(result, TLObjectSizeCalculator::GetSize(object, &objectSize));

	auto objectBuffer = reinterpret_cast<BYTE*>(CoTaskMemAlloc(objectSize));

	ComPtr<ITLBinaryWriterEx> binaryWriter = Make<TLBinaryWriter>(objectBuffer, objectSize);
	if (FAILED(result = object->Write(binaryWriter.Get())))
	{
		CoTaskMemFree(objectBuffer);
		return result;
	}

	*__valueSize = objectSize;
	*value = objectBuffer;
	return S_OK;
}

HRESULT TLObjectSerializerStatics::Deserialize(UINT32 __bufferSize, BYTE* buffer, ITLBinaryReader** value)
{
	if (buffer == nullptr)
	{
		return E_INVALIDARG;
	}

	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto binaryWriter = Make<TLBinaryReader>(buffer, __bufferSize);
	return binaryWriter.CopyTo(value);
}

HRESULT TLObjectSerializerStatics::GetObjectSize(ITLObject* object, UINT32* value)
{
	if (object == nullptr)
	{
		return E_INVALIDARG;
	}

	if (value == nullptr)
	{
		return E_POINTER;
	}

	return TLObjectSizeCalculator::GetSize(object, value);
}