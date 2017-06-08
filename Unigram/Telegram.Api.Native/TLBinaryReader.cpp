#include "pch.h"
#include "TLBinaryReader.h"
#include "TLObject.h"
#include "NativeBuffer.h"
#include "Helpers\COMHelper.h"

using namespace Telegram::Api::Native;
using namespace Telegram::Api::Native::TL;
using Windows::Storage::Streams::IBufferByteAccess;


TLBinaryReader::TLBinaryReader() :
	m_buffer(nullptr),
	m_position(0),
	m_capacity(0)
{
}

TLBinaryReader::~TLBinaryReader()
{
}

HRESULT TLBinaryReader::RuntimeClassInitialize(IBuffer* underlyingBuffer)
{
	if (underlyingBuffer == nullptr)
	{
		return E_INVALIDARG;
	}

	HRESULT result;
	ComPtr<IBufferByteAccess> bufferByteAccess;
	ReturnIfFailed(result, underlyingBuffer->QueryInterface(IID_PPV_ARGS(&bufferByteAccess)));
	ReturnIfFailed(result, bufferByteAccess->Buffer(&m_buffer));
	ReturnIfFailed(result, underlyingBuffer->get_Capacity(&m_capacity));

	m_underlyingBuffer = underlyingBuffer;
	return S_OK;
}

HRESULT TLBinaryReader::RuntimeClassInitialize(TLBinaryReader* reader, UINT32 length)
{
	if (reader == nullptr)
	{
		return E_INVALIDARG;
	}

	if (reader->m_position + length > reader->m_capacity)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	m_buffer = reader->m_buffer + reader->m_position;
	m_capacity = length;
	m_underlyingBuffer = reader->m_underlyingBuffer;
	return S_OK;
}

HRESULT TLBinaryReader::RuntimeClassInitialize(UINT32 capacity)
{
	HRESULT result;
	ComPtr<NativeBuffer> nativeBuffer;
	ReturnIfFailed(result, MakeAndInitialize<NativeBuffer>(&nativeBuffer, capacity));

	m_buffer = nativeBuffer->GetBuffer();
	m_capacity = nativeBuffer->GetCapacity();
	m_underlyingBuffer = nativeBuffer;
	return S_OK;
}

HRESULT TLBinaryReader::get_Position(UINT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_position;
	return S_OK;
}

HRESULT TLBinaryReader::put_Position(UINT32 value)
{
	if (value > m_capacity)
	{
		return E_BOUNDS;
	}

	m_position = value;
	return S_OK;
}

HRESULT TLBinaryReader::get_UnconsumedBufferLength(UINT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_capacity - m_position;
	return S_OK;
}

HRESULT TLBinaryReader::ReadByte(BYTE* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	if (m_position + sizeof(BYTE) > m_capacity)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	*value = m_buffer[m_position++];
	return S_OK;
}

HRESULT TLBinaryReader::ReadInt16(INT16* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	if (m_position + sizeof(INT16) > m_capacity)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	*value = (m_buffer[m_position] & 0xff) | ((m_buffer[m_position + 1] & 0xff) << 8);

	m_position += sizeof(INT16);
	return S_OK;
}

HRESULT TLBinaryReader::ReadUInt16(UINT16* value)
{
	return ReadInt16(reinterpret_cast<INT16*>(value));
}

HRESULT TLBinaryReader::ReadInt32(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	if (m_position + sizeof(INT32) > m_capacity)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	*value = (m_buffer[m_position] & 0xff) | ((m_buffer[m_position + 1] & 0xff) << 8) |
		((m_buffer[m_position + 2] & 0xff) << 16) | ((m_buffer[m_position + 3] & 0xff) << 24);

	m_position += sizeof(INT32);
	return S_OK;
}

HRESULT TLBinaryReader::ReadUInt32(UINT32* value)
{
	return ReadInt32(reinterpret_cast<INT32*>(value));
}

HRESULT TLBinaryReader::ReadInt64(INT64* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	if (m_position + sizeof(INT64) > m_capacity)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	*value = (m_buffer[m_position] & 0xffLL) | ((m_buffer[m_position + 1] & 0xffLL) << 8LL) |
		((m_buffer[m_position + 2] & 0xffLL) << 16LL) | ((m_buffer[m_position + 3] & 0xffLL) << 24LL) |
		((m_buffer[m_position + 4] & 0xffLL) << 32LL) | ((m_buffer[m_position + 5] & 0xffLL) << 40LL) |
		((m_buffer[m_position + 6] & 0xffLL) << 48LL) | ((m_buffer[m_position + 7] & 0xffLL) << 56LL);

	m_position += sizeof(INT64);
	return S_OK;
}

HRESULT TLBinaryReader::ReadUInt64(UINT64* value)
{
	return ReadInt64(reinterpret_cast<INT64*>(value));
}

HRESULT TLBinaryReader::ReadBoolean(boolean* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	HRESULT result;
	UINT32 constructor;
	ReturnIfFailed(result, ReadUInt32(&constructor));

	if (constructor == 0x997275b5)
	{
		*value = true;
	}
	else if (constructor == 0xbc799737)
	{
		*value = false;
	}
	else
	{
		return E_FAIL;
	}

	return S_OK;
}

HRESULT TLBinaryReader::ReadString(HSTRING* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	HRESULT result;
	UINT32 mbLength;
	LPCCH mbString;
	ReturnIfFailed(result, ReadBuffer2(reinterpret_cast<BYTE const**>(&mbString), &mbLength));

	auto length = MultiByteToWideChar(CP_UTF8, 0, mbString, mbLength, nullptr, 0);

	WCHAR* string;
	HSTRING_BUFFER stringBuffer;
	ReturnIfFailed(result, WindowsPreallocateStringBuffer(length, &string, &stringBuffer));

	MultiByteToWideChar(CP_UTF8, 0, mbString, mbLength, string, length);

	return WindowsPromoteStringBuffer(stringBuffer, value);
}

HRESULT TLBinaryReader::ReadByteArray(UINT32* __valueSize, BYTE** value)
{
	if (__valueSize == nullptr || value == nullptr)
	{
		return E_POINTER;
	}

	HRESULT result;
	UINT32 length;
	BYTE const* buffer;
	ReturnIfFailed(result, ReadBuffer2(&buffer, &length));

	*value = reinterpret_cast<BYTE*>(CoTaskMemAlloc(length));

	CopyMemory(*value, buffer, length);
	return S_OK;
}

HRESULT TLBinaryReader::ReadDouble(double* value)
{
	return ReadInt64(reinterpret_cast<INT64*>(value));
}

HRESULT TLBinaryReader::ReadFloat(float* value)
{
	return ReadInt32(reinterpret_cast<INT32*>(value));
}

HRESULT TLBinaryReader::ReadObject(ITLObject** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	HRESULT result;
	UINT32 constructor;
	ReturnIfFailed(result, ReadUInt32(&constructor));

	if (constructor == 0x56730BCC)
	{
		*value = nullptr;
		return S_OK;
	}
	else
	{
		ComPtr<ITLObject> object;
		ComPtr<ITLObjectConstructorDelegate> constructorDelegate;
		ReturnIfFailed(result, TLObject::GetObjectConstructor(constructor, constructorDelegate));
		ReturnIfFailed(result, constructorDelegate->Invoke(&object));
		ReturnIfFailed(result, object->Read(static_cast<ITLBinaryReaderEx*>(this)));

		*value = object.Detach();
		return S_OK;
	}
}

HRESULT TLBinaryReader::ReadObjectAndConstructor(UINT32 objectSize, UINT32* constructor, ITLObject** value)
{
	if (constructor == nullptr || value == nullptr)
	{
		return E_POINTER;
	}

	if (m_position + objectSize > m_capacity)
	{
		return E_BOUNDS;
	}

	HRESULT result;
	ReturnIfFailed(result, ReadUInt32(constructor));

	objectSize -= sizeof(UINT32);

	if (*constructor == 0x56730BCC)
	{
		*value = nullptr;
		return S_OK;
	}
	else
	{
		ComPtr<ITLObject> object;
		ComPtr<ITLObjectConstructorDelegate> constructorDelegate;
		if (SUCCEEDED(result = TLObject::GetObjectConstructor(*constructor, constructorDelegate)))
		{
			ReturnIfFailed(result, constructorDelegate->Invoke(&object));

			ComPtr<TLBinaryReader> reader;
			ReturnIfFailed(result, MakeAndInitialize<TLBinaryReader>(&reader, this, objectSize));
			ReturnIfFailed(result, object->Read(static_cast<ITLBinaryReaderEx*>(reader.Get())));
		}
		else
		{
			ReturnIfFailed(result, MakeAndInitialize<TLUnparsedObject>(&object, *constructor, objectSize, this));
		}

		m_position += objectSize;

		*value = object.Detach();
		return S_OK;
	}
}

HRESULT TLBinaryReader::ReadBigEndianInt32(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	if (m_position + sizeof(INT32) > m_capacity)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	*value = ((m_buffer[m_position++] & 0xff) << 24) | ((m_buffer[m_position++] & 0xff) << 16) |
		((m_buffer[m_position++] & 0xff) << 8) | (m_buffer[m_position++] & 0xff);
	return S_OK;
}

HRESULT TLBinaryReader::ReadWString(std::wstring& string)
{
	HRESULT result;
	UINT32 mbLength;
	LPCCH mbString;
	ReturnIfFailed(result, ReadBuffer2(reinterpret_cast<BYTE const**>(&mbString), &mbLength));

	auto length = MultiByteToWideChar(CP_UTF8, 0, mbString, mbLength, nullptr, 0);
	string.resize(length);

	MultiByteToWideChar(CP_UTF8, 0, mbString, mbLength, &string[0], length);
	return S_OK;
}

HRESULT TLBinaryReader::ReadRawBuffer(UINT32 __valueSize, BYTE* value)
{
	if (value == nullptr)
	{
		return E_INVALIDARG;
	}

	if (m_position + __valueSize > m_capacity)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	CopyMemory(value, m_buffer + m_position, __valueSize);

	m_position += __valueSize;
	return S_OK;
}

HRESULT TLBinaryReader::ReadBuffer(BYTE* buffer, UINT32 length)
{
	HRESULT result;
	UINT32 sourceLength;
	BYTE const* sourceBuffer;
	ReturnIfFailed(result, ReadBuffer2(&sourceBuffer, &sourceLength));

	CopyMemory(buffer, sourceBuffer, min(length, sourceLength));
	return S_OK;
}

void TLBinaryReader::Reset()
{
	m_position = 0;
}

HRESULT TLBinaryReader::ReadBuffer2(BYTE const** buffer, UINT32* length)
{
	if (m_position + 1 > m_capacity)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	UINT32 sl = 1;
	UINT32 l = m_buffer[m_position++];

	if (l > 253)
	{
		if (m_position + 3 > m_capacity)
		{
			return E_NOT_SUFFICIENT_BUFFER;
		}

		l = m_buffer[m_position] | (m_buffer[m_position + 1] << 8) | (m_buffer[m_position + 2] << 16);
		sl = 4;

		m_position += 3;
	}

	UINT32 padding = (l + sl) % 4;
	if (padding != 0)
	{
		padding = 4 - padding;
	}

	if (m_position + l + padding > m_capacity)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	*length = l;
	*buffer = m_buffer + m_position;

	m_position += l + padding;
	return S_OK;
}

HRESULT TLBinaryReader::ReadRawBuffer2(BYTE const** buffer, UINT32 length)
{
	if (m_position + length > m_capacity)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	*buffer = m_buffer + m_position;

	m_position += length;
	return S_OK;
}

HRESULT TLBinaryReader::SeekCurrent(INT32 bytes)
{
	if (m_position + bytes > m_capacity)
	{
		return E_BOUNDS;
	}

	m_position += bytes;
	return S_OK;
}