#include "pch.h"
#include <memory>
#include "TLObjectSerializer.h"
#include "TLObject.h"
#include "NativeBuffer.h"
#include "Helpers\COMHelper.h"

using namespace ABI::Windows::Storage;
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

HRESULT TLObjectSerializerStatics::CreateReaderFromBuffer(IBuffer* buffer, ITLBinaryReader** value)
{
	if (buffer == nullptr)
	{
		return E_INVALIDARG;
	}

	if (value == nullptr)
	{
		return E_POINTER;
	}

	return MakeAndInitialize<TLMemoryBinaryReader>(value, buffer);
}

HRESULT TLObjectSerializerStatics::CreateReaderFromFile(IStorageFile* file, ITLBinaryReader** value)
{
	if (file == nullptr)
	{
		return E_INVALIDARG;
	}

	if (value == nullptr)
	{
		return E_POINTER;
	}

	HRESULT result;
	ComPtr<IStorageItem> storageItem;
	ReturnIfFailed(result, file->QueryInterface(IID_PPV_ARGS(&storageItem)));

	HString fileName;
	ReturnIfFailed(result, storageItem->get_Path(fileName.GetAddressOf()));

	return MakeAndInitialize<TLFileBinaryReader>(value, fileName.GetRawBuffer(nullptr), OPEN_EXISTING);
}

HRESULT TLObjectSerializerStatics::CreateReaderFromFileName(HSTRING fileName, ITLBinaryReader** value)
{
	if (fileName == nullptr)
	{
		return E_INVALIDARG;
	}

	if (value == nullptr)
	{
		return E_POINTER;
	}

	return MakeAndInitialize<TLFileBinaryReader>(value, WindowsGetStringRawBuffer(fileName, nullptr), OPEN_EXISTING);
}

HRESULT TLObjectSerializerStatics::CreateWriterFromBuffer(IBuffer* buffer, ITLBinaryWriter** value)
{
	if (buffer == nullptr)
	{
		return E_INVALIDARG;
	}

	if (value == nullptr)
	{
		return E_POINTER;
	}

	return MakeAndInitialize<TLMemoryBinaryWriter>(value, buffer);
}

HRESULT TLObjectSerializerStatics::CreateWriterFromFile(IStorageFile* file, ITLBinaryWriter** value)
{
	if (file == nullptr)
	{
		return E_INVALIDARG;
	}

	if (value == nullptr)
	{
		return E_POINTER;
	}

	HRESULT result;
	ComPtr<IStorageItem> storageItem;
	ReturnIfFailed(result, file->QueryInterface(IID_PPV_ARGS(&storageItem)));

	HString fileName;
	ReturnIfFailed(result, storageItem->get_Path(fileName.GetAddressOf()));

	return MakeAndInitialize<TLFileBinaryWriter>(value, fileName.GetRawBuffer(nullptr), OPEN_EXISTING);
}

HRESULT TLObjectSerializerStatics::CreateWriterFromFileName(HSTRING fileName, ITLBinaryWriter** value)
{
	if (fileName == nullptr)
	{
		return E_INVALIDARG;
	}

	if (value == nullptr)
	{
		return E_POINTER;
	}

	return MakeAndInitialize<TLFileBinaryWriter>(value, WindowsGetStringRawBuffer(fileName, nullptr), CREATE_ALWAYS);
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