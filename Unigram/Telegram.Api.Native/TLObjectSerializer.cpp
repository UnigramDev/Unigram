#include "pch.h"
#include <memory>
#include "TLObjectSerializer.h"
#include "TLObject.h"
#include "NativeBuffer.h"
#include "Helpers\COMHelper.h"

using namespace Telegram::Api::Native::TL;

ActivatableStaticOnlyFactory(TLObjectSerializerStatics);


HRESULT TLObjectSerializerStatics::Serialize(ITLObject* object, IBuffer** value)
{
	if (object == nullptr)
	{
		return E_INVALIDARG;
	}

	if (value == nullptr)
	{
		return E_POINTER;
	}

	HRESULT result;
	UINT32 objectSize;
	ReturnIfFailed(result, TLObjectSizeCalculator::GetSize(object, &objectSize));

	ComPtr<NativeBuffer> nativeBuffer;
	ReturnIfFailed(result, MakeAndInitialize<NativeBuffer>(&nativeBuffer, objectSize));

	ComPtr<TLMemoryBinaryWriter> binaryWriter;
	ReturnIfFailed(result, MakeAndInitialize<TLMemoryBinaryWriter>(&binaryWriter, nativeBuffer.Get()));
	ReturnIfFailed(result, binaryWriter->WriteObject(object));

	*value = nativeBuffer.Detach();
	return S_OK;
}

HRESULT TLObjectSerializerStatics::Deserialize(IBuffer* buffer, ITLObject** value)
{
	HRESULT result;
	ComPtr<TLMemoryBinaryReader> binaryReader;
	ReturnIfFailed(result, MakeAndInitialize<TLMemoryBinaryReader>(&binaryReader, buffer));

	return binaryReader->ReadObject(value);
}

//HRESULT TLObjectSerializerStatics::Deserialize(UINT32 __bufferSize, BYTE* buffer, ITLBinaryReader** value)
//{
//	if (buffer == nullptr)
//	{
//		return E_INVALIDARG;
//	}
//
//	if (value == nullptr)
//	{
//		return E_POINTER;
//	}
//
//	auto binaryWriter = Make<TLBinaryReader>(buffer, __bufferSize);
//
//	*value = static_cast<ITLBinaryReaderEx*>(binaryWriter.Detach());
//	return S_OK;
//}

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