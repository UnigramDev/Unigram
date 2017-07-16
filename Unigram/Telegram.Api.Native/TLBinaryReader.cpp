#include "pch.h"
#include "TLBinaryReader.h"
#include "TLObject.h"
#include "NativeBuffer.h"
#include "Helpers\COMHelper.h"

using namespace Telegram::Api::Native;
using namespace Telegram::Api::Native::TL;
using Windows::Storage::Streams::IBufferByteAccess;


HRESULT TLBinaryReader::ReadUInt16(UINT16* value)
{
	return ReadInt16(reinterpret_cast<INT16*>(value));
}

HRESULT TLBinaryReader::ReadUInt32(UINT32* value)
{
	return ReadInt32(reinterpret_cast<INT32*>(value));
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
	BYTE const* buffer;
	ReturnIfFailed(result, ReadBuffer2(&buffer, __valueSize));

	*value = reinterpret_cast<BYTE*>(CoTaskMemAlloc(*__valueSize));

	CopyMemory(*value, buffer, *__valueSize);
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

HRESULT TLBinaryReader::ReadBuffer(BYTE* buffer, UINT32 length)
{
	HRESULT result;
	UINT32 sourceLength;
	BYTE const* sourceBuffer;
	ReturnIfFailed(result, ReadBuffer2(&sourceBuffer, &sourceLength));

	CopyMemory(buffer, sourceBuffer, min(length, sourceLength));
	return S_OK;
}

HRESULT TLBinaryReader::ReadVector(UINT32* __valueSize, ITLObject*** value)
{
	if (__valueSize == nullptr || value == nullptr)
	{
		return E_POINTER;
	}

	HRESULT result;
	std::vector<ComPtr<ITLObject>> vector;
	ReturnIfFailed(result, ReadTLObjectVector<ITLObject>(static_cast<ITLBinaryReaderEx*>(this), vector));

	auto count = vector.size();
	if ((*value = reinterpret_cast<ITLObject**>(CoTaskMemAlloc(sizeof(ITLObject*) * count))) == nullptr)
	{
		return E_OUTOFMEMORY;
	}

	for (size_t i = 0; i < count; i++)
	{
		(*value)[i] = vector[i].Detach();
	}

	*__valueSize = static_cast<UINT32>(count);
	return S_OK;
}


TLMemoryBinaryReader::TLMemoryBinaryReader() :
	m_buffer(nullptr),
	m_position(0),
	m_capacity(0)
{
}

TLMemoryBinaryReader::~TLMemoryBinaryReader()
{
}

HRESULT TLMemoryBinaryReader::RuntimeClassInitialize(IBuffer* underlyingBuffer)
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

HRESULT TLMemoryBinaryReader::RuntimeClassInitialize(TLMemoryBinaryReader* reader, UINT32 length)
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

HRESULT TLMemoryBinaryReader::RuntimeClassInitialize(UINT32 capacity)
{
	HRESULT result;
	ComPtr<NativeBuffer> nativeBuffer;
	ReturnIfFailed(result, MakeAndInitialize<NativeBuffer>(&nativeBuffer, capacity));

	m_buffer = nativeBuffer->GetBuffer();
	m_capacity = nativeBuffer->GetCapacity();
	m_underlyingBuffer = nativeBuffer;
	return S_OK;
}

HRESULT TLMemoryBinaryReader::get_Position(UINT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	if (m_underlyingBuffer == nullptr)
	{
		return RO_E_CLOSED;
	}

	*value = m_position;
	return S_OK;
}

HRESULT TLMemoryBinaryReader::put_Position(UINT32 value)
{
	if (value > m_capacity)
	{
		return E_BOUNDS;
	}

	if (m_underlyingBuffer == nullptr)
	{
		return RO_E_CLOSED;
	}

	m_position = value;
	return S_OK;
}

HRESULT TLMemoryBinaryReader::get_UnconsumedBufferLength(UINT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	if (m_underlyingBuffer == nullptr)
	{
		return RO_E_CLOSED;
	}

	*value = m_capacity - m_position;
	return S_OK;
}

HRESULT TLMemoryBinaryReader::ReadByte(BYTE* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	if (m_underlyingBuffer == nullptr)
	{
		return RO_E_CLOSED;
	}

	if (m_position + sizeof(BYTE) > m_capacity)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	*value = m_buffer[m_position++];
	return S_OK;
}

HRESULT TLMemoryBinaryReader::ReadInt16(INT16* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	if (m_underlyingBuffer == nullptr)
	{
		return RO_E_CLOSED;
	}

	if (m_position + sizeof(INT16) > m_capacity)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	*value = static_cast<INT16>(m_buffer[m_position]) | (static_cast<INT16>(m_buffer[m_position + 1]) << 8);

	m_position += sizeof(INT16);
	return S_OK;
}

HRESULT TLMemoryBinaryReader::ReadInt32(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	if (m_underlyingBuffer == nullptr)
	{
		return RO_E_CLOSED;
	}

	if (m_position + sizeof(INT32) > m_capacity)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	*value = static_cast<INT32>(m_buffer[m_position]) | (static_cast<INT32>(m_buffer[m_position + 1]) << 8) |
		(static_cast<INT32>(m_buffer[m_position + 2]) << 16) | (static_cast<INT32>(m_buffer[m_position + 3]) << 24);

	m_position += sizeof(INT32);
	return S_OK;
}

HRESULT TLMemoryBinaryReader::ReadInt64(INT64* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	if (m_underlyingBuffer == nullptr)
	{
		return RO_E_CLOSED;
	}

	if (m_position + sizeof(INT64) > m_capacity)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	*value = static_cast<INT64>(m_buffer[m_position]) | (static_cast<INT64>(m_buffer[m_position + 1]) << 8LL) |
		(static_cast<INT64>(m_buffer[m_position + 2]) << 16LL) | (static_cast<INT64>(m_buffer[m_position + 3]) << 24LL) |
		(static_cast<INT64>(m_buffer[m_position + 4]) << 32LL) | (static_cast<INT64>(m_buffer[m_position + 5]) << 40LL) |
		(static_cast<INT64>(m_buffer[m_position + 6]) << 48LL) | (static_cast<INT64>(m_buffer[m_position + 7]) << 56LL);

	m_position += sizeof(INT64);
	return S_OK;
}

HRESULT TLMemoryBinaryReader::ReadObject(ITLObject** value)
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
		if (SUCCEEDED(result = TLObject::GetObjectConstructor(constructor, constructorDelegate)))
		{
			ReturnIfFailed(result, constructorDelegate->Invoke(&object));
			ReturnIfFailed(result, object->Read(static_cast<ITLBinaryReaderEx*>(this)));
		}
		else
		{
			auto objectSize = GetUnconsumedBufferLength();
			ReturnIfFailed(result, MakeAndInitialize<TLUnparsedObject>(&object, constructor, objectSize, this));

			m_position += objectSize;
		}

		*value = object.Detach();
		return S_OK;
	}
}

HRESULT TLMemoryBinaryReader::ReadObjectAndConstructor(UINT32 objectSize, UINT32* constructor, ITLObject** value)
{
	if (constructor == nullptr || value == nullptr)
	{
		return E_POINTER;
	}

	if (m_underlyingBuffer == nullptr)
	{
		return RO_E_CLOSED;
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

			ComPtr<TLMemoryBinaryReader> reader;
			ReturnIfFailed(result, MakeAndInitialize<TLMemoryBinaryReader>(&reader, this, objectSize));
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

HRESULT TLMemoryBinaryReader::ReadBigEndianInt32(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	if (m_underlyingBuffer == nullptr)
	{
		return RO_E_CLOSED;
	}

	if (m_position + sizeof(INT32) > m_capacity)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	*value = (static_cast<INT32>(m_buffer[m_position]) << 24) | (static_cast<INT32>(m_buffer[m_position + 1]) << 16) |
		(static_cast<INT32>(m_buffer[m_position + 2]) << 8) | static_cast<INT32>(m_buffer[m_position + 3]);

	m_position += sizeof(INT32);
	return S_OK;
}

HRESULT TLMemoryBinaryReader::ReadRawBuffer(UINT32 __valueSize, BYTE* value)
{
	if (value == nullptr)
	{
		return E_INVALIDARG;
	}

	if (m_underlyingBuffer == nullptr)
	{
		return RO_E_CLOSED;
	}

	if (m_position + __valueSize > m_capacity)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	CopyMemory(value, m_buffer + m_position, __valueSize);

	m_position += __valueSize;
	return S_OK;
}

HRESULT TLMemoryBinaryReader::Reset()
{
	if (m_underlyingBuffer == nullptr)
	{
		return RO_E_CLOSED;
	}

	m_position = 0;
	return S_OK;
}

HRESULT TLMemoryBinaryReader::Close()
{
	m_position = 0;
	m_capacity = 0;
	m_buffer = nullptr;
	m_underlyingBuffer.Reset();
	return S_OK;
}

HRESULT TLMemoryBinaryReader::ReadBuffer2(BYTE const** buffer, UINT32* length)
{
	if (m_underlyingBuffer == nullptr)
	{
		return RO_E_CLOSED;
	}

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

		l = static_cast<UINT32>(m_buffer[m_position]) | (static_cast<UINT32>(m_buffer[m_position + 1]) << 8) | (static_cast<UINT32>(m_buffer[m_position + 2]) << 16);
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

HRESULT TLMemoryBinaryReader::ReadRawBuffer2(BYTE const** buffer, UINT32 length)
{
	if (m_underlyingBuffer == nullptr)
	{
		return RO_E_CLOSED;
	}

	if (m_position + length > m_capacity)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	*buffer = m_buffer + m_position;

	m_position += length;
	return S_OK;
}

HRESULT TLMemoryBinaryReader::SeekCurrent(INT32 bytes)
{
	if (m_underlyingBuffer == nullptr)
	{
		return RO_E_CLOSED;
	}

	if (m_position + bytes > m_capacity)
	{
		return E_BOUNDS;
	}

	m_position += bytes;
	return S_OK;
}


HRESULT TLFileBinaryReader::RuntimeClassInitialize(LPCWSTR fileName, DWORD creationDisposition)
{
	m_file.Attach(CreateFile2(fileName, GENERIC_READ, NULL, creationDisposition, nullptr));
	if (!m_file.IsValid())
	{
		return GetLastHRESULT();
	}

	return S_OK;
}

HRESULT TLFileBinaryReader::get_Position(UINT32* value)
{
	if (!m_file.IsValid())
	{
		return RO_E_CLOSED;
	}

	LARGE_INTEGER position;
	if (!SetFilePointerEx(m_file.Get(), { 0 }, &position, FILE_CURRENT))
	{
		return GetLastHRESULT();
	}

	*value = position.LowPart;
	return S_OK;
}

HRESULT TLFileBinaryReader::put_Position(UINT32 value)
{
	if (!m_file.IsValid())
	{
		return RO_E_CLOSED;
	}

	if (!SetFilePointerEx(m_file.Get(), { value }, nullptr, FILE_BEGIN))
	{
		return GetLastHRESULT();
	}

	return S_OK;
}

HRESULT TLFileBinaryReader::get_UnconsumedBufferLength(UINT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	if (!m_file.IsValid())
	{
		return RO_E_CLOSED;
	}

	LARGE_INTEGER position;
	if (!SetFilePointerEx(m_file.Get(), { 0 }, &position, FILE_CURRENT))
	{
		return GetLastHRESULT();
	}

	LARGE_INTEGER fileSize;
	if (!GetFileSizeEx(m_file.Get(), &fileSize))
	{
		return GetLastHRESULT();
	}

	*value = static_cast<UINT32>(fileSize.QuadPart - position.QuadPart);
	return S_OK;
}

HRESULT TLFileBinaryReader::ReadByte(BYTE* value)
{
	return ReadRawBuffer(sizeof(BYTE), value);
}

HRESULT TLFileBinaryReader::ReadInt16(INT16* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	HRESULT result;
	BYTE buffer[sizeof(INT16)];
	ReturnIfFailed(result, ReadRawBuffer(sizeof(INT16), buffer));

	*value = static_cast<INT16>(buffer[0]) | (static_cast<INT16>(buffer[1]) << 8);
	return S_OK;
}

HRESULT TLFileBinaryReader::ReadInt32(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	HRESULT result;
	BYTE buffer[sizeof(INT32)];
	ReturnIfFailed(result, ReadRawBuffer(sizeof(INT32), buffer));

	*value = static_cast<INT32>(buffer[0]) | (static_cast<INT32>(buffer[1]) << 8) |
		(static_cast<INT32>(buffer[2]) << 16) | (static_cast<INT32>(buffer[3]) << 24);
	return S_OK;
}

HRESULT TLFileBinaryReader::ReadInt64(INT64* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	HRESULT result;
	BYTE buffer[sizeof(INT64)];
	ReturnIfFailed(result, ReadRawBuffer(sizeof(INT64), buffer));

	*value = static_cast<INT64>(buffer[0]) | (static_cast<INT64>(buffer[1]) << 8LL) |
		(static_cast<INT64>(buffer[2]) << 16LL) | (static_cast<INT64>(buffer[3]) << 24LL) |
		(static_cast<INT64>(buffer[4]) << 32LL) | (static_cast<INT64>(buffer[5]) << 40LL) |
		(static_cast<INT64>(buffer[6]) << 48LL) | (static_cast<INT64>(buffer[7]) << 56LL);
	return S_OK;
}

HRESULT TLFileBinaryReader::ReadObjectAndConstructor(UINT32 objectSize, UINT32* constructor, ITLObject** value)
{
	if (constructor == nullptr || value == nullptr)
	{
		return E_POINTER;
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
		ComPtr<MappedFileBuffer> buffer;
		ReturnIfFailed(result, MakeAndInitialize<MappedFileBuffer>(&buffer, m_file.Get(), objectSize));

		ComPtr<TLMemoryBinaryReader> reader;
		ReturnIfFailed(result, MakeAndInitialize<TLMemoryBinaryReader>(&reader, buffer.Get()));

		ComPtr<ITLObject> object;
		ComPtr<ITLObjectConstructorDelegate> constructorDelegate;
		if (SUCCEEDED(result = TLObject::GetObjectConstructor(*constructor, constructorDelegate)))
		{
			ReturnIfFailed(result, constructorDelegate->Invoke(&object));
			ReturnIfFailed(result, object->Read(static_cast<ITLBinaryReaderEx*>(reader.Get())));
		}
		else
		{
			ReturnIfFailed(result, MakeAndInitialize<TLUnparsedObject>(&object, *constructor, objectSize, reader.Get()));
		}

		if (!SetFilePointerEx(m_file.Get(), { objectSize }, nullptr, FILE_CURRENT))
		{
			return GetLastHRESULT();
		}

		*value = object.Detach();
		return S_OK;
	}
}

HRESULT TLFileBinaryReader::ReadBigEndianInt32(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	HRESULT result;
	BYTE buffer[sizeof(INT32)];
	ReturnIfFailed(result, ReadRawBuffer(sizeof(INT32), buffer));

	*value = (static_cast<INT32>(buffer[0]) << 24) | (static_cast<INT32>(buffer[1]) << 16) |
		(static_cast<INT32>(buffer[2]) << 8) | static_cast<INT32>(buffer[3]);
	return S_OK;
}

HRESULT TLFileBinaryReader::ReadRawBuffer(UINT32 __valueSize, BYTE* value)
{
	if (!m_file.IsValid())
	{
		return RO_E_CLOSED;
	}

	DWORD bytesRead;
	if (!ReadFile(m_file.Get(), value, __valueSize, &bytesRead, nullptr))
	{
		return GetLastHRESULT();
	}

	if (bytesRead < __valueSize)
	{
		return E_BOUNDS;
	}

	return S_OK;
}

HRESULT TLFileBinaryReader::Reset()
{
	if (!m_file.IsValid())
	{
		return RO_E_CLOSED;
	}

	if (!SetFilePointerEx(m_file.Get(), { 0 }, nullptr, FILE_BEGIN))
	{
		return GetLastHRESULT();
	}

	m_buffer.clear();
	return S_OK;
}

HRESULT TLFileBinaryReader::Close()
{
	m_buffer.clear();
	m_file.Close();
	return S_OK;
}

HRESULT TLFileBinaryReader::ReadBuffer2(BYTE const** buffer, UINT32* length)
{
	HRESULT result;
	BYTE lb;
	ReturnIfFailed(result, ReadByte(&lb));

	UINT32 sl = 1;
	UINT32 l = lb;

	if (l > 253)
	{
		BYTE sizeBuffer[3];
		ReturnIfFailed(result, ReadRawBuffer(3, sizeBuffer));

		l = static_cast<UINT32>(sizeBuffer[0]) | (static_cast<UINT32>(sizeBuffer[1]) << 8) | (static_cast<UINT32>(sizeBuffer[2]) << 16);
		sl = 4;
	}

	UINT32 padding = (l + sl) % 4;
	if (padding != 0)
	{
		padding = 4 - padding;
	}

	if (m_buffer.size() < l)
	{
		m_buffer.resize(l);
	}

	ReturnIfFailed(result, ReadRawBuffer(l, m_buffer.data()));

	if (!SetFilePointerEx(m_file.Get(), { padding }, nullptr, FILE_CURRENT))
	{
		return GetLastHRESULT();
	}

	*length = l;
	*buffer = m_buffer.data();
	return S_OK;
}

HRESULT TLFileBinaryReader::ReadRawBuffer2(BYTE const** buffer, UINT32 length)
{
	if (m_buffer.size() < length)
	{
		m_buffer.resize(length);
	}

	HRESULT result;
	ReturnIfFailed(result, ReadRawBuffer(length, m_buffer.data()));

	*buffer = m_buffer.data();
	return S_OK;
}