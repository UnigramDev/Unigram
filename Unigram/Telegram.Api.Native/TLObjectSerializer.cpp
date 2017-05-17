#include "pch.h"
#include <memory>
#include "TLObjectSerializer.h"
#include "TLObject.h"
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
	auto binaryWriter = Make<TLBinaryWriter>(objectBuffer, objectSize);
	if (FAILED(result = binaryWriter->WriteObject(object)))
	{
		CoTaskMemFree(objectBuffer);
		return result;
	}

	*__valueSize = objectSize;
	*value = objectBuffer;
	return S_OK;
}

HRESULT TLObjectSerializerStatics::Deserialize(UINT32 __bufferSize, BYTE* buffer, ITLObject** value)
{
	auto binaryReader = Make<TLBinaryReader>(buffer, __bufferSize);
	return binaryReader->ReadObject(value);
}

HRESULT TLObjectSerializerStatics::Deserialize2(UINT32 __bufferSize, BYTE* buffer, ITLBinaryReader** value)
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

	*value = static_cast<ITLBinaryReaderEx*>(binaryWriter.Detach());
	return S_OK;
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

HRESULT TLObjectSerializerStatics::RegisterObjectConstructor(UINT32 constructor, ITLObjectConstructorDelegate* constructorDelegate)
{
	if (constructorDelegate == nullptr)
	{
		return E_INVALIDARG;
	}

	return TLObject::RegisterTLObjecConstructor(constructor, constructorDelegate);
}