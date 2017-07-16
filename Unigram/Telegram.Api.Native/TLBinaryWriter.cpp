#include "pch.h"
#include <memory>
#include "TLObject.h"
#include "TLBinaryWriter.h"
#include "NativeBuffer.h"
#include "Helpers\COMHelper.h"

using namespace Telegram::Api::Native;
using namespace Telegram::Api::Native::TL;
using Windows::Storage::Streams::IBufferByteAccess;


HRESULT TLBinaryWriter::WriteUInt16(UINT16 value)
{
	return WriteInt16(*reinterpret_cast<INT16*>(&value));
}

HRESULT TLBinaryWriter::WriteUInt32(UINT32 value)
{
	return WriteInt32(*reinterpret_cast<INT32*>(&value));
}

HRESULT TLBinaryWriter::WriteUInt64(UINT64 value)
{
	return WriteInt64(*reinterpret_cast<INT64*>(&value));
}

HRESULT TLBinaryWriter::WriteBoolean(boolean value)
{
	if (value)
	{
		return WriteUInt32(0x997275b5);
	}
	else
	{
		return WriteUInt32(0xbc799737);
	}
}

HRESULT TLBinaryWriter::WriteString(HSTRING value)
{
	UINT32 length;
	auto buffer = WindowsGetStringRawBuffer(value, &length);
	return WriteString(buffer, length);
}

HRESULT TLBinaryWriter::WriteByteArray(UINT32 __valueSize, BYTE* value)
{
	return WriteBuffer(value, __valueSize);
}

HRESULT TLBinaryWriter::WriteDouble(double value)
{
	return WriteInt64(*reinterpret_cast<INT64*>(&value));
}

HRESULT TLBinaryWriter::WriteFloat(float value)
{
	return WriteInt32(*reinterpret_cast<INT32*>(&value));
}

HRESULT TLBinaryWriter::WriteObject(ITLObject* value)
{
	if (value == nullptr)
	{
		return WriteUInt32(0x56730BCC);
	}
	else
	{
		HRESULT result;
		UINT32 constructor;
		ReturnIfFailed(result, value->get_Constructor(&constructor));
		ReturnIfFailed(result, WriteUInt32(constructor));

		return value->Write(static_cast<ITLBinaryWriterEx*>(this));
	}
}

HRESULT TLBinaryWriter::WriteWString(std::wstring const& string)
{
	return WriteString(string.data(), static_cast<UINT32>(string.size()));
}

HRESULT TLBinaryWriter::WriteString(LPCWCHAR buffer, UINT32 length)
{
	auto mbLength = WideCharToMultiByte(CP_UTF8, 0, buffer, length, nullptr, 0, nullptr, nullptr);
	auto mbString = std::make_unique<char[]>(mbLength);
	WideCharToMultiByte(CP_UTF8, 0, buffer, length, mbString.get(), mbLength, nullptr, nullptr);

	return WriteBuffer(reinterpret_cast<BYTE*>(mbString.get()), mbLength);
}

HRESULT TLBinaryWriter::WriteVector(UINT32 __valueSize, ITLObject** value)
{
	if (value == nullptr)
	{
		return E_INVALIDARG;
	}

	HRESULT result;
	ReturnIfFailed(result, WriteUInt32(TLVECTOR_CONSTRUCTOR));
	ReturnIfFailed(result, WriteUInt32(__valueSize));

	for (UINT32 i = 0; i < __valueSize; i++)
	{
		ReturnIfFailed(result, WriteObject(value[i]));
	}

	return S_OK;
}


TLMemoryBinaryWriter::TLMemoryBinaryWriter() :
	m_buffer(nullptr),
	m_position(0),
	m_capacity(0)
{
}

TLMemoryBinaryWriter::~TLMemoryBinaryWriter()
{
}

HRESULT TLMemoryBinaryWriter::RuntimeClassInitialize(IBuffer* underlyingBuffer)
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

HRESULT TLMemoryBinaryWriter::RuntimeClassInitialize(TLMemoryBinaryWriter* writer, UINT32 length)
{
	if (writer == nullptr)
	{
		return E_INVALIDARG;
	}

	if (writer->m_position + length > writer->m_capacity)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	m_buffer = writer->m_buffer + writer->m_position;
	m_capacity = length;
	m_underlyingBuffer = writer->m_underlyingBuffer;
	return S_OK;
}

HRESULT TLMemoryBinaryWriter::RuntimeClassInitialize(UINT32 capacity)
{
	HRESULT result;
	ComPtr<NativeBuffer> nativeBuffer;
	ReturnIfFailed(result, MakeAndInitialize<NativeBuffer>(&nativeBuffer, capacity));

	m_buffer = nativeBuffer->GetBuffer();
	m_capacity = nativeBuffer->GetCapacity();
	m_underlyingBuffer = nativeBuffer;
	return S_OK;
}

HRESULT TLMemoryBinaryWriter::get_Position(UINT32* value)
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

HRESULT TLMemoryBinaryWriter::put_Position(UINT32 value)
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

HRESULT TLMemoryBinaryWriter::get_UnstoredBufferLength(UINT32* value)
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

HRESULT TLMemoryBinaryWriter::WriteByte(BYTE value)
{
	if (m_underlyingBuffer == nullptr)
	{
		return RO_E_CLOSED;
	}

	if (m_position + sizeof(BYTE) > m_capacity)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	m_buffer[m_position++] = value;
	return S_OK;
}

HRESULT TLMemoryBinaryWriter::WriteInt16(INT16 value)
{
	if (m_underlyingBuffer == nullptr)
	{
		return RO_E_CLOSED;
	}

	if (m_position + sizeof(INT16) > m_capacity)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	m_buffer[m_position++] = value & 0xff;
	m_buffer[m_position++] = (value >> 8) & 0xff;
	return S_OK;
}

HRESULT TLMemoryBinaryWriter::WriteInt32(INT32 value)
{
	if (m_underlyingBuffer == nullptr)
	{
		return RO_E_CLOSED;
	}

	if (m_position + sizeof(INT32) > m_capacity)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	m_buffer[m_position++] = value & 0xff;
	m_buffer[m_position++] = (value >> 8) & 0xff;
	m_buffer[m_position++] = (value >> 16) & 0xff;
	m_buffer[m_position++] = (value >> 24) & 0xff;

	return S_OK;
}

HRESULT TLMemoryBinaryWriter::WriteInt64(INT64 value)
{
	if (m_underlyingBuffer == nullptr)
	{
		return RO_E_CLOSED;
	}

	if (m_position + sizeof(INT64) > m_capacity)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	m_buffer[m_position++] = value & 0xff;
	m_buffer[m_position++] = (value >> 8) & 0xff;
	m_buffer[m_position++] = (value >> 16) & 0xff;
	m_buffer[m_position++] = (value >> 24) & 0xff;
	m_buffer[m_position++] = (value >> 32) & 0xff;
	m_buffer[m_position++] = (value >> 40) & 0xff;
	m_buffer[m_position++] = (value >> 48) & 0xff;
	m_buffer[m_position++] = (value >> 56) & 0xff;

	return S_OK;
}

HRESULT TLMemoryBinaryWriter::WriteRawBuffer(UINT32 __valueSize, BYTE* value)
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

	CopyMemory(m_buffer + m_position, value, __valueSize);

	m_position += __valueSize;
	return S_OK;
}

HRESULT TLMemoryBinaryWriter::WriteBigEndianInt32(INT32 value)
{
	if (m_underlyingBuffer == nullptr)
	{
		return RO_E_CLOSED;
	}

	if (m_position + sizeof(INT32) > m_capacity)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	m_buffer[m_position++] = (value >> 24) & 0xff;
	m_buffer[m_position++] = (value >> 16) & 0xff;
	m_buffer[m_position++] = (value >> 8) & 0xff;
	m_buffer[m_position++] = value & 0xff;

	return S_OK;
}

HRESULT TLMemoryBinaryWriter::WriteBuffer(BYTE const* buffer, UINT32 length)
{
	if (m_underlyingBuffer == nullptr)
	{
		return RO_E_CLOSED;
	}

	UINT32 padding;

	if (length < 254)
	{
		padding = (length + 1) % 4;
		if (padding != 0)
		{
			padding = 4 - padding;
		}

		if (m_position + 1 + length + padding > m_capacity)
		{
			return E_NOT_SUFFICIENT_BUFFER;
		}

		m_buffer[m_position++] = length;
	}
	else
	{
		padding = (length + 4) % 4;
		if (padding != 0)
		{
			padding = 4 - padding;
		}

		if (m_position + 4 + length + padding > m_capacity)
		{
			return E_NOT_SUFFICIENT_BUFFER;
		}

		m_buffer[m_position++] = 254;
		m_buffer[m_position++] = length & 0xff;
		m_buffer[m_position++] = (length >> 8) & 0xff;
		m_buffer[m_position++] = (length >> 16) & 0xff;
	}

	CopyMemory(m_buffer + m_position, buffer, length);

	m_position += length + padding;
	return S_OK;
}

HRESULT TLMemoryBinaryWriter::SeekCurrent(INT32 bytes)
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

HRESULT TLMemoryBinaryWriter::Reset()
{
	if (m_underlyingBuffer == nullptr)
	{
		return RO_E_CLOSED;
	}

	m_position = 0;
	return S_OK;
}

HRESULT TLMemoryBinaryWriter::Close()
{
	m_position = 0;
	m_capacity = 0;
	m_buffer = nullptr;
	m_underlyingBuffer.Reset();
	return S_OK;
}


HRESULT TLFileBinaryWriter::RuntimeClassInitialize(LPCWSTR fileName, DWORD creationDisposition)
{
	m_file.Attach(CreateFile2(fileName, GENERIC_WRITE, NULL, creationDisposition, nullptr));
	if (!m_file.IsValid())
	{
		return GetLastHRESULT();
	}

	return S_OK;
}

HRESULT TLFileBinaryWriter::get_Position(UINT32* value)
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

HRESULT TLFileBinaryWriter::put_Position(UINT32 value)
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

HRESULT TLFileBinaryWriter::get_UnstoredBufferLength(UINT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	if (!m_file.IsValid())
	{
		return RO_E_CLOSED;
	}

	*value = UINT32_MAX;
	return S_OK;
}

HRESULT TLFileBinaryWriter::WriteByte(BYTE value)
{
	return WriteRawBuffer(sizeof(BYTE), &value);
}

HRESULT TLFileBinaryWriter::WriteInt16(INT16 value)
{
	BYTE buffer[sizeof(INT16)];
	buffer[0] = value & 0xff;
	buffer[1] = (value >> 8) & 0xff;

	return WriteRawBuffer(sizeof(INT16), buffer);
}

HRESULT TLFileBinaryWriter::WriteInt32(INT32 value)
{
	BYTE buffer[sizeof(INT32)];
	buffer[0] = value & 0xff;
	buffer[1] = (value >> 8) & 0xff;
	buffer[2] = (value >> 16) & 0xff;
	buffer[3] = (value >> 24) & 0xff;

	return WriteRawBuffer(sizeof(INT32), buffer);
}

HRESULT TLFileBinaryWriter::WriteInt64(INT64 value)
{
	BYTE buffer[sizeof(INT64)];
	buffer[0] = value & 0xff;
	buffer[1] = (value >> 8) & 0xff;
	buffer[2] = (value >> 16) & 0xff;
	buffer[3] = (value >> 24) & 0xff;
	buffer[4] = (value >> 32) & 0xff;
	buffer[5] = (value >> 40) & 0xff;
	buffer[6] = (value >> 48) & 0xff;
	buffer[7] = (value >> 56) & 0xff;

	return WriteRawBuffer(sizeof(INT64), buffer);
}

HRESULT TLFileBinaryWriter::WriteRawBuffer(UINT32 __valueSize, BYTE* value)
{
	if (!m_file.IsValid())
	{
		return RO_E_CLOSED;
	}

	if (!WriteFile(m_file.Get(), value, __valueSize, nullptr, nullptr))
	{
		return GetLastHRESULT();
	}

	return S_OK;
}

HRESULT TLFileBinaryWriter::WriteBigEndianInt32(INT32 value)
{
	BYTE buffer[sizeof(INT32)];
	buffer[0] = (value >> 24) & 0xff;
	buffer[1] = (value >> 16) & 0xff;
	buffer[2] = (value >> 8) & 0xff;
	buffer[3] = value & 0xff;

	return WriteRawBuffer(sizeof(INT32), buffer);
}

HRESULT TLFileBinaryWriter::WriteBuffer(BYTE const* buffer, UINT32 length)
{
	HRESULT result;
	UINT32 padding;

	if (length < 254)
	{
		padding = (length + 1) % 4;
		if (padding != 0)
		{
			padding = 4 - padding;
		}

		ReturnIfFailed(result, WriteByte(length));
	}
	else
	{
		padding = (length + 4) % 4;
		if (padding != 0)
		{
			padding = 4 - padding;
		}

		BYTE buffer[sizeof(INT32)];
		buffer[0] = 254;
		buffer[1] = length & 0xff;
		buffer[2] = (length >> 8) & 0xff;
		buffer[3] = (length >> 16) & 0xff;

		ReturnIfFailed(result, WriteRawBuffer(sizeof(INT32), buffer));
	}

	ReturnIfFailed(result, WriteRawBuffer(length, const_cast<BYTE*>(buffer)));

	if (padding > 0 && !SetFilePointerEx(m_file.Get(), { padding }, nullptr, FILE_CURRENT))
	{
		return GetLastHRESULT();
	}

	return S_OK;
}

HRESULT TLFileBinaryWriter::Reset()
{
	if (!m_file.IsValid())
	{
		return RO_E_CLOSED;
	}

	if (!SetFilePointerEx(m_file.Get(), { 0 }, nullptr, FILE_BEGIN))
	{
		return GetLastHRESULT();
	}

	return S_OK;
}

HRESULT TLFileBinaryWriter::Close()
{
	m_file.Close();
	return S_OK;
}


TLObjectSizeCalculator::TLObjectSizeCalculator() :
	m_position(0),
	m_length(0)
{
}

TLObjectSizeCalculator::~TLObjectSizeCalculator()
{
}

HRESULT TLObjectSizeCalculator::get_TotalLength(UINT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = max(m_position, m_length);
	return S_OK;
}

HRESULT TLObjectSizeCalculator::get_Position(UINT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_position;
	return S_OK;
}

HRESULT TLObjectSizeCalculator::put_Position(UINT32 value)
{
	if (value > m_position)
	{
		m_length = value;
	}
	else
	{
		m_length = m_position;
	}

	m_position = value;
	return S_OK;
}

HRESULT TLObjectSizeCalculator::get_UnstoredBufferLength(UINT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = UINT32_MAX - max(m_position, m_length);
	return S_OK;
}

HRESULT TLObjectSizeCalculator::WriteByte(BYTE value)
{
	m_position += sizeof(BYTE);
	return S_OK;
}

HRESULT TLObjectSizeCalculator::WriteInt16(INT16 value)
{
	m_position += sizeof(INT16);
	return S_OK;
}

HRESULT TLObjectSizeCalculator::WriteUInt16(UINT16 value)
{
	m_position += sizeof(UINT16);
	return S_OK;
}

HRESULT TLObjectSizeCalculator::WriteInt32(INT32 value)
{
	m_position += sizeof(INT32);
	return S_OK;
}

HRESULT TLObjectSizeCalculator::WriteUInt32(UINT32 value)
{
	m_position += sizeof(UINT32);
	return S_OK;
}

HRESULT TLObjectSizeCalculator::WriteInt64(INT64 value)
{
	m_position += sizeof(INT64);
	return S_OK;
}

HRESULT TLObjectSizeCalculator::WriteUInt64(UINT64 value)
{
	m_position += sizeof(UINT64);
	return S_OK;
}

HRESULT TLObjectSizeCalculator::WriteBoolean(boolean value)
{
	m_position += sizeof(UINT32);
	return S_OK;
}

HRESULT TLObjectSizeCalculator::WriteString(HSTRING value)
{
	UINT32 length;
	auto buffer = WindowsGetStringRawBuffer(value, &length);
	auto mbLength = WideCharToMultiByte(CP_UTF8, 0, buffer, length, nullptr, 0, nullptr, nullptr);
	return WriteBuffer(nullptr, mbLength);
}

HRESULT TLObjectSizeCalculator::WriteByteArray(UINT32 __valueSize, BYTE* value)
{
	return WriteBuffer(value, __valueSize);
}

HRESULT TLObjectSizeCalculator::WriteDouble(double value)
{
	m_position += sizeof(double);
	return S_OK;
}

HRESULT TLObjectSizeCalculator::WriteFloat(float value)
{
	m_position += sizeof(float);
	return S_OK;
}

HRESULT TLObjectSizeCalculator::WriteObject(ITLObject* value)
{
	if (value == nullptr)
	{
		m_position += sizeof(UINT32);
		return S_OK;
	}
	else
	{
		m_position += sizeof(UINT32);
		return value->Write(static_cast<ITLBinaryWriterEx*>(this));
	}
}

HRESULT TLObjectSizeCalculator::WriteVector(UINT32 __valueSize, ITLObject** value)
{
	m_position += 2 * sizeof(UINT32);

	for (UINT32 i = 0; i < __valueSize; i++)
	{
		WriteObject(value[i]);
	}

	return S_OK;
}

HRESULT TLObjectSizeCalculator::WriteRawBuffer(UINT32 __valueSize, BYTE* value)
{
	m_position += __valueSize;
	return S_OK;
}

HRESULT TLObjectSizeCalculator::WriteWString(std::wstring const& string)
{
	auto mbLength = WideCharToMultiByte(CP_UTF8, 0, string.data(), static_cast<UINT32>(string.size()), nullptr, 0, nullptr, nullptr);
	return WriteBuffer(nullptr, mbLength);
}

HRESULT TLObjectSizeCalculator::WriteBigEndianInt32(INT32 value)
{
	m_position += sizeof(INT32);
	return S_OK;
}

HRESULT TLObjectSizeCalculator::WriteBuffer(BYTE const* buffer, UINT32 length)
{
	m_position += TLBinaryWriter::GetByteArrayLength(length);
	return S_OK;
}

HRESULT TLObjectSizeCalculator::Reset()
{
	m_position = 0;
	m_length = 0;
	return S_OK;
}

HRESULT TLObjectSizeCalculator::Close()
{
	m_position = 0;
	m_length = 0;
	return S_OK;
}

HRESULT TLObjectSizeCalculator::GetSize(ITLObject* object, UINT32* value)
{
	UINT32 position;
	UINT32 length;

	static thread_local ComPtr<TLObjectSizeCalculator> instance;
	if (instance == nullptr)
	{
		position = 0;
		length = 0;
		instance = Make<TLObjectSizeCalculator>();
	}
	else
	{
		position = instance->m_position;
		length = instance->m_length;
		instance->Reset();
	}

	HRESULT result;
	if (SUCCEEDED(result = instance->WriteObject(object)))
	{
		*value = max(instance->m_position, instance->m_length);
	}

	instance->m_position = position;
	instance->m_length = length;
	return result;
}
